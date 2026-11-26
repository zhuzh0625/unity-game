"""Export the R06 rig without writing to the source .blend.

blender --background --factory-startup --python-exit-code 1 --python Tools/export_deckhand.py -- path/to/Deckhand_R06.blend path/to/medicine-bottle-v2.blend
"""
from math import cos, pi, radians, sin, sqrt, tau
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector


# DeckhandImporter slices this continuous take into matching 25-sample loops at 24 fps.
POSES = (
    ("Idle", 0, 0), ("Run", 1, 0), ("HelmSmall", 2, 0), ("HelmMedium", 2, 1),
    ("GunSmall", 3, 0), ("GunMediumUpper", 3, 1), ("GunMediumLower", 3, 2),
    ("PumpSmall", 4, 0), ("PumpMedium", 4, 1), ("Repair", 5, 0), ("Swim", 6, 0),
    ("Prone", 7, 0),
)

ATTACK_START = len(POSES) * 25
RIGHT_ARM = ("UpperArm.R", "Forearm.R", "Hand.R")
AIM_DIRECTIONS = ((.16, .985, -.045), (.02, .985, .11), (.01, 1, -.04))
# Active section of the R06 pistol preview's motion.csv, frames 8..45 at 25 fps.
# Keep its authored pose indices here so exporting a copy of the .blend also works.
PISTOL_POSES = (
    0, 0, 1, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 11, 12, 12, 12, 12, 12,
    12, 12, 12, 12, 12, 12, 12, 12, 11, 10, 9, 7, 6, 5, 3, 2, 1, 0, 0,
)
X, Z = Vector((1, 0, 0)), Vector((0, 0, 1))
Y = Vector((0, 1, 0))
SLASH_START = ATTACK_START + 37
SLASH_END = 45
MEDICINE_START = SLASH_START + 2 * (SLASH_END + 1)
SWEEP_FIRST, SWEEP_LAST = 6, 35
CARRY = Quaternion(X, radians(-14)) @ Quaternion(Y, radians(90))


def rotation_to(rig, name, direction):
    bone = rig.data.bones[name]
    return (bone.tail_local - bone.head_local).normalized().rotation_difference(Vector(direction).normalized())


def smooth(t):
    t = max(0., min(t, 1.))
    return t*t*(3-2*t)


def arm_pose(rig, side, shoulder, wrist, pole, palm):
    upper, fore, hand = [part + "." + side for part in ("UpperArm", "Forearm", "Hand")]
    axis = (wrist - shoulder).normalized()
    a, b = (rig.data.bones[n].length for n in (upper, fore))
    reach = min(a+b-.00001, max(abs(a-b)+.00001, (wrist-shoulder).length))
    along = (a*a-b*b+reach*reach)/(2*reach)
    bend = (pole-axis*pole.dot(axis)).normalized()
    elbow = shoulder + axis*along + bend*sqrt(max(0., a*a-along*along))
    return {upper: rotation_to(rig, upper, elbow-shoulder),
            fore: rotation_to(rig, fore, shoulder+axis*reach-elbow),
            hand: rotation_to(rig, hand, palm)}


def slash_pose(rig, frame, sign):
    # render_sweeps.py defines the independent v3 arm solves and cutting edge.
    # The study's 25-fps frames 6..36 use smoothstep across its 21 radial keys.
    # Evaluate the same continuous path at the FBX frame rate.
    weight = smooth(frame/SWEEP_FIRST) * (1-smooth((frame-SWEEP_LAST)/(SLASH_END-SWEEP_LAST)))
    t = smooth((frame-SWEEP_FIRST)/(SWEEP_LAST-SWEEP_FIRST))
    angle = radians(sign * (-64+128*t))
    radial = Vector((sin(angle), cos(angle), 0))
    edge = sign * Vector((cos(angle), -sin(angle), 0))
    grip = Vector((.082,.115,1.235)) + .395*radial
    torso = Quaternion(Z, -.28*angle)
    pivot = rig.data.bones["Spine"].head_local
    def shoulder(side):
        return pivot + torso @ (rig.data.bones["UpperArm."+side].head_local-pivot)
    rotations = {"Spine": torso}
    rotations.update(arm_pose(rig, "R", shoulder("R"), grip-.052*radial, Vector((.82,-.49,-.85)), radial))
    left_wrist = Vector((-.31-.035*sin(angle), -.015-.045*sin(angle), 1.01))
    rotations.update(arm_pose(rig, "L", shoulder("L"), left_wrist, Vector((-.66,-.64,-.4)), Vector((-.14,.45,-.88))))
    rotations = {name: Quaternion().slerp(turn, weight) for name,turn in rotations.items()}
    blade = Matrix((edge, radial, sign*Z)).transposed().to_quaternion()
    return rotations, rotations["Hand.R"].inverted() @ CARRY.slerp(blade, weight)


def medicine_pose(rig):
    # Match the concept's low, upright carry, with the wrist across the lower chest.
    shoulder = rig.data.bones["UpperArm.R"].head_local
    return arm_pose(rig, "R", shoulder, Vector((.128, .225, 1.10)),
                    Vector((.80, -.58, -1.25)), Vector((-.88, .44, .08)))


def add_attacks_and_medicine(rig, medicine):
    aims = [rotation_to(rig, name, direction) for name, direction in zip(RIGHT_ARM, AIM_DIRECTIONS)]
    frames = []
    for frame in range(37):
        # The actor moves on preview frames 8..45; the remaining frames preview the smoke.
        u = PISTOL_POSES[round((len(PISTOL_POSES) - 1) * frame / 36)] / 12
        frames.append({name: Quaternion().slerp(aim, u) for name, aim in zip(RIGHT_ARM, aims)})
    blade_rotations = []
    for sign in (-1, 1):
        for frame in range(SLASH_END + 1):
            pose, blade_rotation = slash_pose(rig, frame, sign)
            frames.append(pose)
            blade_rotations.append(blade_rotation)
    frames.extend((medicine, medicine))

    # Extend the source's existing per-channel driver tables. Never feed the
    # new samples into any frame of the original twelve station/locomotion clips.
    for curve in rig.animation_data.drivers:
        bone_path, field = curve.data_path.rsplit(".", 1)
        bone = rig.path_resolve(bone_path).bone
        rest = bone.matrix_local.to_quaternion()
        curve.driver.expression = f"frame+1000 if frame>={ATTACK_START} else ({curve.driver.expression})"
        for i, rotations in enumerate(frames):
            value = 0.
            if field == "rotation_quaternion":
                parent = rotations.get(bone.parent.name, Quaternion()) if bone.parent else Quaternion()
                # Unspecified children inherit their parent's world-space turn.
                world = rotations.get(bone.name, parent)
                local = rest.inverted() @ parent.inverted() @ world @ rest
                value = local[curve.array_index]
            key = curve.keyframe_points.insert(ATTACK_START + 1000 + i, value, options={"FAST"})
            key.interpolation = "LINEAR"
        curve.update()
    return aims[-1], blade_rotations


def weapon_mesh(name, pieces):
    vertices, faces, indices, materials = [], [], [], []
    for title, color, profile, thickness, offset in pieces:
        base, count = len(vertices), len(profile)
        if sum(a[0]*b[1] - b[0]*a[1] for a, b in zip(profile, profile[1:] + profile[:1])) < 0:
            profile = list(reversed(profile))
        vertices.extend((offset+x, y, z) for x in (-thickness/2, thickness/2) for y, z in profile)
        sides = [(i, (i+1)%count, (i+1)%count+count, i+count) for i in range(count)]
        polygons = [tuple(reversed(range(count))), tuple(range(count, count*2)), *sides]
        faces.extend(tuple(base+i for i in face) for face in polygons)
        indices.extend([len(materials)] * len(polygons))
        mat = bpy.data.materials.get(title) or bpy.data.materials.new(title)
        mat.diffuse_color = (*color, 1)
        mat.use_nodes = True
        shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        shader.inputs["Base Color"].default_value = (*color, 1)
        shader.inputs["Roughness"].default_value = .77
        materials.append(mat)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    for mat in materials:
        mesh.materials.append(mat)
    for polygon, index in zip(mesh.polygons, indices):
        polygon.material_index = index
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def cutlass_mesh():
    # Mesh sections, silver bevel and three turned fittings from render_sweeps.py.
    sections = ((.080,.000,.044),(.17,.003,.057),(.31,.004,.071),(.46,-.002,.080),
                (.56,-.016,.074),(.637,-.043,.050),(.692,-.075,.024),(.725,-.099,.002))
    vertices, faces, indices = [], [], []
    for y, c, w in sections:
        for x, z in ((c-.48*w,.004),(c+.20*w,.004),(c+.52*w,0),(c+.20*w,-.004),(c-.48*w,-.004)):
            vertices.append((x,y,z))
    for i in range(len(sections)-1):
        for j, slot in enumerate((0,1,1,0,2)):
            k,l = i*5+j, i*5+(j+1)%5
            faces.append((k,l,l+5,k+5))
            indices.append(slot)
    faces.extend((tuple(reversed(range(5))),tuple(range(len(vertices)-5,len(vertices)))))
    indices.extend((2,2))
    for a,b,rx,rz,slot in ((-.088,.066,.023,.026,3),(-.10,-.077,.029,.029,4),(.060,.085,.066,.051,4)):
        base = len(vertices)
        for y,r in ((a,.87),(a+.004,1),(b-.004,1),(b,.86)):
            vertices.extend((r*rx*cos(tau*j/10),y,r*rz*sin(tau*j/10)) for j in range(10))
        for ring in range(3):
            for j in range(10):
                k,l = base+10*ring+j, base+10*ring+(j+1)%10
                faces.append((k,k+10,l+10,l))
        faces.extend((tuple(range(base,base+10)),tuple(reversed(range(base+30,base+40)))))
        indices.extend([slot]*32)

    mesh = bpy.data.meshes.new("Cutlass")
    mesh.from_pydata(vertices, [], faces)
    for name, color, metal in (
        ("Satin slate steel",(.19,.25,.29),.27), ("Silver edge marking",(.67,.75,.77),.19),
        ("Steel spine",(.11,.15,.18),.24), ("Dark grip",(.13,.062,.026),0), ("Muted brass",(.48,.27,.085),.30)
    ):
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*color,1)
        mat.use_nodes = True
        shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        shader.inputs["Base Color"].default_value = (*color,1)
        shader.inputs["Metallic"].default_value = metal
        shader.inputs["Roughness"].default_value = .55
        mesh.materials.append(mat)
    for poly,slot in zip(mesh.polygons, indices):
        poly.material_index = slot
    mesh.update()
    blade = bpy.data.objects.new("Cutlass", mesh)
    bpy.context.scene.collection.objects.link(blade)
    return blade


def socket(name, parent, position):
    node = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(node)
    node.parent = parent
    node.location = position
    return node


def hand_props(rig, aim, blade_rotations):
    wood, iron, brass = (.145,.070,.031), (.056,.059,.059), (.42,.28,.10)
    # The pistol uses the four silhouette layers of the v4 actor render.
    pistol = weapon_mesh("Pistol", (
        ("Walnut", wood, [(-.083,-.066),(-.058,-.077),(.003,-.007),(.17,.004),(.216,.012),(.222,.035),(-.013,.039),(-.054,.019),(-.072,-.012)], .046, 0),
        ("Gunmetal", iron, [(-.029,.027),(.230,.027),(.230,.055),(.209,.061),(-.021,.058)], .037, 0),
        ("Slate", (.105,.108,.105), [(-.023,.032),(-.046,.079),(-.034,.094),(-.020,.088),(-.009,.063),(.010,.042)], .011, .022),
        ("Brass", brass, [(.162,.017),(.174,.018),(.174,.047),(.162,.047)], .042, 0),
    ))
    hand = rig.data.bones["Hand.R"]
    # Bone-parented FBX props keep their local grip during every old and new clip.
    parent_rest = hand.matrix_local @ Matrix.Translation((0, hand.length, 0))
    grip = hand.head_local.lerp(hand.tail_local, .60)
    pistol.parent, pistol.parent_type, pistol.parent_bone = rig, "BONE", hand.name
    rest_grip = grip + aim.inverted() @ Vector((.012,.002,.002))
    pistol.matrix_basis = parent_rest.inverted() @ Matrix.Translation(rest_grip) @ aim.inverted().to_matrix().to_4x4()
    muzzle = socket("PistolMuzzle", pistol, (0,.230,.041))

    pivot = socket("CutlassGrip", rig, (0,0,0))
    pivot.parent_type, pivot.parent_bone = "BONE", hand.name
    grip = hand.head_local + .052*(hand.tail_local-hand.head_local).normalized()
    pivot.matrix_basis = parent_rest.inverted() @ Matrix.Translation(grip)
    blade = cutlass_mesh()
    blade.parent = pivot
    blade.rotation_mode = "QUATERNION"
    last = CARRY
    def key(frame, rotation):
        nonlocal last
        rotation = rotation.copy()
        rotation.make_compatible(last)
        blade.rotation_quaternion = rotation
        blade.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        last = rotation
    key(0, CARRY)
    key(24, CARRY)
    for phase in range(25):
        bpy.context.scene.frame_set(25+phase)
        hand_turn = rig.pose.bones[hand.name].matrix.to_quaternion() @ hand.matrix_local.to_quaternion().inverted()
        # The carry drawing preserves the original swinging arm and shows a low,
        # forward-pointing blade. Compensate the wrist pitch as the arm moves.
        carry_run = Quaternion(X, radians(4+6*cos(tau*phase/24))) @ Quaternion(Y, radians(90))
        key(25+phase, hand_turn.inverted() @ carry_run)
    key(50, CARRY)
    key(SLASH_START-1, CARRY)
    for frame, rotation in enumerate(blade_rotations, SLASH_START):
        key(frame, rotation)
    tip = socket("CutlassTip", blade, (-.099,.725,0))
    edge = socket("CutlassEdge", blade, (.10,0,0))
    face = socket("CutlassFace", blade, (0,0,.10))
    return pistol, muzzle, blade, tip, pivot, edge, face


def medicine_prop(rig, source, pose):
    with bpy.data.libraries.load(str(source), link=False) as (available, loaded):
        assert all(name in available.objects for name in ("Bottle", "Cork")), source
        loaded.objects = ["Bottle", "Cork"]
    bottle, cork = loaded.objects
    assert cork.parent == bottle
    for obj, name in ((bottle, "MedicineBottle"), (cork, "MedicineBottleCork")):
        obj.name = obj.data.name = name
        bpy.context.scene.collection.objects.link(obj)

    material = bottle.data.materials[0]
    material.name = "MedicineBottleSurface"
    palette = next(node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE")
    assert palette.packed_file and tuple(palette.size) == (128, 128)

    hand = rig.data.bones["Hand.R"]
    parent_rest = hand.matrix_local @ Matrix.Translation((0, hand.length, 0))
    turn = pose[hand.name].inverted()
    grip = hand.head_local.lerp(hand.tail_local, .55)
    rest_base = grip + turn @ Vector((-.022, .020, -.031))
    # The marked face is -Y in the prop source. Turn it toward the character's front.
    label = Quaternion(Z, pi - radians(12))
    bottle.parent, bottle.parent_type, bottle.parent_bone = rig, "BONE", hand.name
    bottle.matrix_basis = parent_rest.inverted() @ Matrix.Translation(rest_base) @ (turn @ label).to_matrix().to_4x4()
    return bottle, cork, palette


def export(source, medicine_source):
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    scene = bpy.context.scene
    assert (scene.render.fps, scene.render.fps_base) == (24, 1.0)
    scene.name = "DeckhandTake"
    rig = next(o for o in scene.objects if o.type == "ARMATURE")
    body = next(o for o in scene.objects if o.name.startswith("Sailor"))
    tool = next(o for o in scene.objects if o.name.startswith("Mallet"))
    rig.name, body.name, tool.name = "DeckhandRig", "Body", "RepairHammer"
    body.data.name, tool.data.name = body.name, tool.name
    assert len(rig.data.bones) == 15
    assert sum(len(o.data.polygons) for o in (body, tool)) == 979
    stations = rig.id_properties_ui("Station").as_dict()["items"]
    assert tuple(item[0] for item in stations[6:]) == ("SWIM", "PRONE")

    # Retain the right-hand grip constraint and mallet rotation driver. Visibility
    # belongs to gameplay, so keep unit scale on every exported frame.
    for axis in range(3):
        tool.driver_remove("scale", axis)
    tool.scale = (1, 1, 1)

    # Retain all bone drivers, replacing only their shared clock and station inputs.
    rig.animation_data.action = None
    for index, (_, station, fit) in enumerate(POSES):
        start = index * 25
        rig["Station"], rig["Fit"] = station, fit
        for prop in ('["Station"]', '["Fit"]'):
            rig.keyframe_insert(data_path=prop, frame=start)
        for phase in (0, 24):
            rig["Cycle"] = float(phase)
            rig.keyframe_insert(data_path='["Cycle"]', frame=start + phase)
    action = rig.animation_data.action
    curves = action.layers[0].strips[0].channelbag(action.slots[0]).fcurves
    for curve in curves:
        for key in curve.keyframe_points:
            key.interpolation = "LINEAR" if curve.data_path == '["Cycle"]' else "CONSTANT"

    medicine = medicine_pose(rig)
    aim, blade_rotations = add_attacks_and_medicine(rig, medicine)
    props = hand_props(rig, aim, blade_rotations)
    bottle, cork, palette = medicine_prop(rig, medicine_source, medicine)
    scene.frame_start, scene.frame_end = 0, MEDICINE_START + 1
    # Default FBX conversion maps Blender +Y onto -Z. Turn the rig around
    # before conversion so Unity's positive Z is the character's forward.
    rig.rotation_euler.z += pi
    # Blender skips object animation on meshes directly parented to an
    # armature. Export the constrained mallet as a sibling so the motion
    # of the right-hand grip and the swing are both written to the take.
    tool.parent = None
    tool.rotation_euler.z += pi
    for index, (name, _, _) in enumerate(POSES):
        start = index * 25
        joints = []
        rotations = []
        swim_roots = []
        for frame in range(start, start + 25):
            scene.frame_set(frame)
            joints.append([bone.head.copy() for bone in rig.pose.bones])
            rotations.append([bone.matrix.to_quaternion() for bone in rig.pose.bones])
            if name == "Swim":
                swim_roots.append(rig.pose.bones["Pelvis"].head.copy())
            if name == "Repair":
                hand = rig.pose.bones["Hand.R"]
                grip = rig.matrix_world @ hand.head.lerp(hand.tail, 0.5)
                assert (tool.matrix_world.translation - grip).length < 1e-5
        seam = max((a - b).length for a, b in zip(joints[0], joints[-1]))
        travel = max((a - b).length for row in joints for a, b in zip(joints[0], row))
        assert seam < 0.0003, (name, seam)
        assert min(abs(a.dot(b)) for a, b in zip(rotations[0], rotations[-1])) > 0.99999, name
        if name == "Swim":
            # Keep gameplay responsible for horizontal movement at the waterline.
            assert travel > 0.25, (name, travel)
            assert max((root - swim_roots[0]).xy.length for root in swim_roots) < 0.0003
        elif name == "Prone":
            assert travel < 0.0003, (name, travel)
        elif name != "Idle":
            assert travel > 0.014, (name, travel)
        print(f"{name}: loop error {seam:.6f} m, joint travel {travel:.4f} m")
    pistol, muzzle, cutlass, tip, grip, edge, face = props
    scene.frame_set(ATTACK_START + 22)
    barrel = pistol.matrix_world.to_quaternion() @ Vector((0, 1, 0))
    forward = rig.matrix_world.to_quaternion() @ Vector((0, 1, 0))
    assert barrel.dot(forward) > .999, ("pistol aim", barrel, forward)
    assert (muzzle.matrix_world.translation - pistol.matrix_world.translation).length > .23
    for start, sign in ((SLASH_START, -1), (SLASH_START+SLASH_END+1, 1)):
        previous = None
        for offset in range(SWEEP_FIRST, SWEEP_LAST+1):
            scene.frame_set(start+offset)
            local = rig.matrix_world.inverted() @ cutlass.matrix_world
            t = smooth((offset-SWEEP_FIRST)/(SWEEP_LAST-SWEEP_FIRST))
            angle = radians(sign*(-64+128*t))
            radial = Vector((sin(angle), cos(angle), 0))
            leading_edge = sign*Vector((cos(angle),-sin(angle),0))
            expected_grip = Vector((.082,.115,1.235)) + .395*radial
            actual_tip = rig.matrix_world.inverted() @ tip.matrix_world.translation
            expected_tip = expected_grip + .725*radial - .099*leading_edge
            assert (local.translation-expected_grip).length < .0001, ("grip", start, offset)
            assert (actual_tip-expected_tip).length < .0001, ("tip", start, offset, actual_tip, expected_tip)
            assert (local.to_quaternion() @ Z).dot(sign*Z) > .99999, ("flat", start, offset)
            assert (local.to_quaternion() @ X).dot(leading_edge) > .99999, ("edge direction", start, offset)
            if previous is not None:
                assert (actual_tip-previous).normalized().dot(leading_edge) > .98, ("edge leading", start, offset)
            previous = actual_tip
        print(f"v3 slash {sign:+d}: all 30 sweep samples match authored grip, tip, broad face and leading edge")
    scene.frame_set(MEDICINE_START)
    local_bottle = rig.matrix_world.inverted() @ bottle.matrix_world
    label = local_bottle.to_quaternion() @ -Y
    hand = rig.pose.bones["Hand.R"]
    grip = hand.head.lerp(hand.tail, .55)
    assert (local_bottle.translation + .035 * Z - grip).length < .04
    assert label.dot(Y) > .95 and (local_bottle.to_quaternion() @ Z).dot(Z) > .999
    print("Medicine hold: hand-anchored bottle, upright front label, original packed palette")
    scene.frame_set(0)
    destination = Path(__file__).resolve().parents[1] / "Assets/Naval/Resources/Crew/Deckhand_R06.fbx"
    destination.parent.mkdir(parents=True, exist_ok=True)
    label_texture = destination.with_name("MedicineBottleLabel.png")
    label_texture.write_bytes(palette.packed_file.data)
    palette.filepath = str(label_texture)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (rig, body, tool, *props, bottle, cork):
        obj.select_set(True)
    # The FBX exporter evaluates the dependency graph on each integer frame,
    # including the rig's driven bones and the independently animated prop.
    bpy.ops.export_scene.fbx(
        filepath=str(destination), use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, use_mesh_modifiers=False, use_custom_props=False, path_mode="RELATIVE",
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_use_all_bones=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    )
    print(f"Exported {destination}")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    source = Path(args[0]).resolve()
    medicine_source = (Path(args[1]).resolve() if len(args) > 1 else
                       source.parent.parent / "drawings/medicine-poses/medicine-bottle-v2.blend")
    export(source, medicine_source)
