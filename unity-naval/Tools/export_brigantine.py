"""Run Blender -b Medium_Brigantine_R06.blend --python-exit-code 1 -P Tools/export_brigantine.py -- <Unity project>."""
from collections import Counter
import json
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.dont_write_bytecode = True
source = Path(bpy.data.filepath)
assert source.name == "Medium_Brigantine_R06.blend" and bpy.app.background
sys.path[:0] = [str(Path(__file__).parent), str(source.parents[1])]
from fleet_mesh import ShipMesh, boarding_ladder, ladder_name, source_identity
from layout_r03 import SternLayout
from model_hull import end_x, width

out = Path(sys.argv[sys.argv.index("--") + 1]) / "Assets/Naval/Resources/Fleet/Brigantine"
out.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes["01 全船"]
assert scene["模型修订"] == "R06"
bpy.context.window.scene = scene
bpy.context.view_layer.update()
design = SternLayout()
spec = design.s
base = -min(row[3] for row in spec["stations"]) + .10  # underside of keel timber
ship = ShipMesh((0, base, 0))
objects = [o for o in scene.objects if o.type in {"MESH", "CURVE"} and o.name != "展示地面" and not o.hide_render]
origin = (0., 0., 0.)


def direction(point):
    x, y, z = point
    return (-y, z, x)


def unity(point):
    x, y, z = direction(point)
    return (x, base + y, z)


def vector(value):
    return {k: round(float(v), 5) for k, v in zip("xyz", value)}


def mark(name, point, facing=(1, 0, 0)):
    return dict(name=name, position=vector(unity(point)), forward=vector(direction(facing)))


def bounds(obj):
    corners = np.array([obj.matrix_world @ Vector(p) for p in obj.bound_box])
    return corners.min(0), corners.max(0)


def partition_shell(obj):
    """Keep the authored loop UV island on each of the thirteen R04 lining objects."""
    original = obj.data
    lining = original.materials.find("内壳 · 错缝橡木板")
    assert lining >= 0 and original.uv_layers.get("内壳木板 UV")
    for inside, group in ((False, "HULL_Shell"), (True, "INNER_LINING_Oak")):
        mesh = original.copy()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        remove = [face for face in bm.faces if (face.material_index == lining) != inside]
        bmesh.ops.delete(bm, geom=remove, context="FACES")
        bm.to_mesh(mesh)
        bm.free()
        part = bpy.data.objects.new("Export shell", mesh)
        scene.collection.objects.link(part)
        part.matrix_world = obj.matrix_world.copy()
        bpy.context.view_layer.update()
        ship.add_object(part, group, uv_layer="内壳木板 UV" if inside else None)
        bpy.data.objects.remove(part, do_unlink=True)
        bpy.data.meshes.remove(mesh)


def gun_marker(obj):
    side = int(obj["side"])
    ids = {i for face in obj.data.polygons if "铜身" in obj.data.materials[face.material_index].name
           for i in face.vertices}
    points = np.array([obj.matrix_world @ obj.data.vertices[i].co for i in ids])
    breech = points[points[:, 1] * side < min(points[:, 1] * side) + .12]
    center = (breech.min(0) + breech.max(0)) / 2
    center[1] = min(points[:, 1] * side) * side - side * .09
    return mark(obj.name, center, (0, side, 0))


def export_ship():
    sail_names = {"主桅 · 斜桁纵帆": "SailMainGaff", "艏部 · 三角帆": "SailJib",
                  "前桅 · 横主帆": "SailForeCourse", "前桅 · 横顶帆": "SailForeTopsail"}
    materials = bpy.data.materials["内壳 · 错缝橡木板"]
    pigment = next(n.image for n in materials.node_tree.nodes
                   if n.type == "TEX_IMAGE" and n.image.colorspace_settings.name == "sRGB")
    (out / "HullLining.png").write_bytes(pigment.packed_file.data)
    hulls = {"gunport_shell", "underbody", "bulwark", "cabin_wall", "helm_cheek", "transom", "transom_lower"}
    guns, flags = [], []
    panes = Counter()
    for obj in objects:
        role = obj.get("role")
        if role in hulls:
            partition_shell(obj)
            continue
        if role == "pennant":
            flags.append(obj)
            continue
        pivot = origin
        cloth = obj if role == "sail" else obj.parent if obj.parent and obj.parent.get("role") == "sail" else None
        if cloth:
            a, b = bounds(cloth)
            pivot = unity(((a[0]+b[0])/2, (a[1]+b[1])/2, b[2]))
            group = sail_names[cloth.name]
        elif role in ("wheel", "pump_handle"):
            group = "Wheel" if role == "wheel" else "PumpLever0"
            pivot = unity(obj.matrix_world.translation)
        elif role == "repair":
            group = "INNER_RepairBoards"
        elif role == "boarding_ladder":
            group = ladder_name(obj)
        elif role == "glazing":
            assert obj.data.materials[0].name == "艉货舱 · 清透窗玻璃"
            panes[obj["face"]] += 1
            group = "ClearGlazing"
        elif role in ("station", "station_base"):
            group = "Battery" + obj["deck"].title()
            if role == "station":
                guns.append(gun_marker(obj))
        elif role in ("rigging", "rigging_blocks", "mast", "spars", "mast_foot"):
            group = "StandingRig"
        elif role in ("floor", "floor_back"):
            group = "Deck" + str(round(float(obj["z"])*10))
        else:
            group = "ShipFittings"
        ship.add_object(obj, group, pivot)

    assert len(guns) == 18 and len(flags) == 2
    assert panes == {"port": 2, "starboard": 2, "stern": 3}
    main = next(obj for obj in flags if obj.modifiers.get("旗帜样式"))
    modifier = main.modifiers["旗帜样式"]
    menu = next(socket for socket in modifier.node_group.interface.items_tree if socket.name == "旗面")
    for group, choice, inks in (("FlagsPirate", "海盗旗", {"bone", "binding", "black"}),
                                ("FlagsBritish", "英国国旗", {"red", "linen", "navy", "binding"})):
        getattr(modifier.properties.inputs, menu.identifier).value = choice
        main.update_tag()
        bpy.context.view_layer.update()
        for obj in flags:
            evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            mesh = evaluated.to_mesh()
            assert {mesh.materials[p.material_index].name.split(" | ")[-1] for p in mesh.polygons} == inks
            evaluated.to_mesh_clear()
            ship.add_object(obj, group)
    return guns


class Collision:
    def __init__(self):
        self.vertices = []
        self.triangles = []

    def quad(self, points, normal):
        vertices = np.asarray([unity(p) for p in points])
        indices = [0, 1, 2, 3]
        if np.dot(np.cross(vertices[1]-vertices[0], vertices[2]-vertices[0]), direction(normal)) < 0:
            indices.reverse()
        a, b, c, d = [len(self.vertices)+i for i in indices]
        self.vertices.extend(vertices)
        self.triangles.extend(((a, b, c), (a, c, d)))

    def barrier(self, start, end, height):
        a, b = np.asarray(start), np.asarray(end)
        up = np.array((0, 0, height))
        normal = np.cross(b-a, up)
        # A rail is solid from either corridor, including the descending side of a hatch.
        size = np.linalg.norm(normal)
        if size < 1e-7:
            return
        inset = normal / size * .025
        self.quad([a+inset, b+inset, b+inset+up, a+inset+up], normal)
        self.quad([a-inset, b-inset, b-inset+up, a-inset+up], -normal)

    def box(self, a, b):
        x0, y0, z0 = a
        x1, y1, z1 = b
        p = [(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0)]
        top = [(x,y,z1) for x,y,_ in p]
        self.quad(top, (0,0,1))
        for i, normal in enumerate(((0,-1,0),(1,0,0),(0,1,0),(-1,0,0))):
            j = (i+1) % 4
            self.quad([p[i],p[j],top[j],top[i]], normal)

    def flush(self, group):
        ship.add_geometry("Collision_" + group, self.vertices, self.triangles)


def export_walkways():
    rails, ramps, fixtures = Collision(), Collision(), Collision()
    for obj in objects:
        role = obj.get("role")
        if role == "floor_back":
            # The continuous backing has exactly the artist's stair openings. Raise its top
            # to the tread level so the controller glides across the decorative plank seams.
            clone = obj.copy()
            scene.collection.objects.link(clone)
            clone.location.z += .049
            bpy.context.view_layer.update()
            ship.add_object(clone, "Collision_Decks")
            bpy.data.objects.remove(clone, do_unlink=True)
        elif role in ("cabin_front","open_doorframe","cabin_wall","transom","gunport_shell","bulwark","bulwark_cap","overhead"):
            ship.add_object(obj, "Collision_Shell")
        elif role in ("chest", "pump", "mast_below", "mast_foot", "station", "helm_stand"):
            a, b = bounds(obj)
            if role == "station":
                a[2] = spec["decks"][obj["deck"]]
                # Preserve the sight line from the battery aisle to the breech.
                if obj["side"] > 0:
                    a[1] += .10
                else:
                    b[1] -= .10
            fixtures.box(a, b)
    main = spec["decks"]["upper"]
    roof = spec["decks"]["helm_platform"]
    st = design.stairs["lower"]
    a, b = st["low_xy"][0], st["high_xy"][0]
    w = st["width"]/2
    ramps.quad([(a,-w,0),(b,-w,main),(b,w,main),(a,w,0)], (0,0,1))
    for sign in (-1,1):
        rails.barrier((a,sign*w,0), (b,sign*w,main), .65)
        rails.barrier((a-.03,sign*(w+.04),main), (b-.10,sign*(w+.04),main), .71)
    rails.barrier((a-.03,-w,main),(a-.03,w,main),.71)
    for key in ("helm_port","helm_starboard"):
        st = design.stairs[key]
        inside, outside = design.flight_edge(st,False), design.flight_edge(st,True)
        for i in range(len(inside)-1):
            corners = [inside[i],inside[i+1],outside[i+1],outside[i]]
            ramps.quad([(x,y,main+h) for x,y,h in corners], (0,0,1))
        for line in (inside,outside):
            for (x,y,h),(xx,yy,hh) in zip(line,line[1:]):
                rails.barrier((x,y,main+h),(xx,yy,main+hh),.66)
    contour = design.shell_contour(1)
    for (x,y),(xx,yy) in zip(contour,contour[1:]+contour[:1]):
        if abs(x-xx)<.001 and abs(x-design.room["shell"]["front_x"])<.001:
            w = spec["helm_stair_shell"]["inner_edge_abs_y"]
            y, yy = -w, w
        rails.barrier((x,y,roof),(xx,yy,roof),.66)
    rails.flush("Rails")
    ramps.flush("Stairs")
    fixtures.flush("Fixtures")


def ray_surface(obj, pos, ray):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    inv = evaluated.matrix_world.inverted()
    hit, point, normal, _ = evaluated.ray_cast(inv @ Vector(pos), inv.to_3x3() @ Vector(ray))
    assert hit, (obj.name, pos)
    normal = (inv.to_3x3().transposed() @ normal).normalized()
    if normal.dot(Vector(ray)) > 0:
        normal.negate()
    return evaluated.matrix_world @ point, normal


def leak_markers():
    inside, outside, stands = [], [], []
    # One repair bay in each clear space between the lower battery carriages.
    # Preserve the original marks and the first/last pair used by the drill.
    bays = ((1,5.6,1), (2,8.4,-1), (3,11.2,1), (4,14.,-1),
            (5,16.8,1), (6,16.8,-1), (7,19.6,1),
            (9,5.6,-1), (10,8.4,1), (11,11.2,-1), (12,14.,1), (8,19.6,-1))
    for index, x, side in bays:
        shell = next(o for o in objects if o.get("role") == "gunport_shell" and o["side"] == side)
        backing = next((o for o in objects if o.get("role") == "repair" and o["side"] == side and abs(o["x"]-x)<.01),shell)
        h = .68 if backing != shell else .98
        name = "Breach%02d" % index
        for dest, target, y, look in ((inside,backing,0,side),(outside,shell,5*side,-side)):
            point, normal = ray_surface(target, (x,y,h),(0,look,0))
            assert abs(normal.y)>.80, (x, normal)
            dest.append(mark(name, point, normal))
            if dest is inside:
                foot = (x, point.y - side*.85, spec["decks"]["lower"])
                stands.append(mark("Repair stand %02d" % index, foot, (0,side,0)))
    return inside, outside, stands


def breadth_table(max_height):
    # Intersect the design loft at each world station. Its transom and stem both
    # rake, so the design station parameter and the world longitudinal metre differ.
    length = design.length
    u = np.linspace(0, length, 601)
    result = []
    for y in np.linspace(0,max_height,51):
        h = min(y-base,spec["decks"]["upper"])
        x = [end_x(p,max(0,h)) for p in u]
        if h >= 0:
            half = [width(design,p,h) for p in u]
        else:
            bottom = np.array([design.bottom(p) for p in u])
            fraction = np.clip(1 - h/np.minimum(bottom,-.00001),0,1)
            half = np.array([design.sides["lower"](p) for p in u]) * np.sin(fraction*np.pi/2)**.65
        result.extend(np.interp(np.linspace(0,length,62),x,half,left=0,right=0).round(4).tolist())
    return result


guns = export_ship()
export_walkways()
inside, outside, stands = leak_markers()
max_height = base + spec["decks"]["helm_platform"]
wheel = next(o for o in objects if o.get("role") == "wheel")
data = dict(length=design.length, beam=spec["beam"], dryDraft=base+.24, dryHeight=base-.04,
            lowerDeck=base, mainDeck=base+2.5, upperDeck=base+2.5, deckCrown=base+2.5,
            holdAft=.83, holdFore=24., holdCeiling=base-.105, liningInset=.115,
            lowerAft=.80, lowerFore=24., maxHullHeight=max_height, widths=breadth_table(max_height),
            spawn=mark("舵台值更",(*design.room["helm_stand_xy"],spec["decks"]["helm_platform"])),
            helm=mark("舵轮",wheel.matrix_world.translation),
            sails=mark("右舷调帆索",(10.90,-3.18,3.12),(0,-1,0)),
            capstan=mark("艏斜桅系索处",(25.456,0,3.228)),
            pumps=[mark("舱底手压泵",(8.62,0,1.43))],
            guns=guns, inside=inside, outside=outside, repairStands=stands,
            wheelAxis=vector(direction((1,0,0))), pumpAxis=vector(direction((0,1,0))))
data.update(source_identity(source))
caps = {obj["side"]: obj for obj in objects if obj.get("role") == "bulwark_cap"}
data["boardingLadders"] = [boarding_ladder(obj, caps[obj["side"]], lambda x, y: spec["decks"]["upper"], base)
                           for obj in objects if obj.get("role") == "boarding_ladder"]
assert len(data["boardingLadders"]) == 4
assert len(data["widths"])==62*51 and set(("Wheel","PumpLever0","FlagsPirate","FlagsBritish"))<=ship.groups.keys()
ship.write(out/"Model.r13mesh")
(out/"Rig.json").write_text(json.dumps(data,ensure_ascii=False,separators=(",",":")),encoding="utf-8")
print(f"Brigantine R06: {len(objects)} source objects, {len(ship.groups)} groups, {len(guns)} guns, "
      f"{len(inside)} paired raycast breaches, stair heights {base:.2f}/{base+2.5:.2f}/{max_height:.2f} m.")
