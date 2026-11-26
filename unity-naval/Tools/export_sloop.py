"""Export Small_Sloop_R06.blend into the fleet used by the single-player game.

blender -b Small_Sloop_R06.blend --python-exit-code 1 -P Tools/export_sloop.py -- <Unity project root>
"""
import json
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from fleet_mesh import ShipMesh, boarding_ladder, ladder_name, source_identity

source = Path(bpy.data.filepath)
assert source.name == "Small_Sloop_R06.blend" and bpy.app.background
design = source.parents[1]
sys.path.insert(0, str(design))
from ship_drawing import Ship

spec = json.loads((design / "drawings/R03/model-spec.json").read_text(encoding="utf8"))
shape = Ship(spec)
hatch_aft, hatch_fore, _, hatch_edge = spec["deck_fittings"]["grating_bounds_xy"]
opening_half = hatch_edge - .07
cabin_floor = 1.04
cabin_aft, cabin_fore, cabin_half = 6.36, 10.74, 1.42
out = Path(sys.argv[sys.argv.index("--") + 1]) / "Assets/Naval/Resources/Fleet/Sloop"
out.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes["01 全船"]
assert scene["模型修订"] == "R06"
bpy.context.window.scene = scene
bpy.context.view_layer.update()
baseline = -spec["keel_lower_z_midship"]
ship = ShipMesh(offset=(0, baseline, 0))


def direction(blender):
    return (-blender[1], blender[2], blender[0])


def position(blender):
    return (-blender[1], blender[2] + baseline, blender[0])


def vector(values):
    return {axis: round(float(value), 5) for axis, value in zip("xyz", values)}


def marker(label, location, heading=(0, 0, 1)):
    return dict(name=label, position=vector(location), forward=vector(heading))


def split_export(obj, sections):
    """Retain evaluated bevels, source materials and normals when splitting moving parts."""
    deps = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(deps)
    full = bpy.data.meshes.new_from_object(evaluated, depsgraph=deps)
    chosen = [set() for _ in sections]
    for poly in full.polygons:
        for i, (_, _, predicate) in enumerate(sections):
            if predicate(poly, full):
                chosen[i].add(poly.index)
                break
        else:
            raise ValueError(obj.name)
    for (group, pivot, _), keep in zip(sections, chosen):
        assert keep, group
        mesh = full.copy()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
        bm.to_mesh(mesh)
        bm.free()
        part = bpy.data.objects.new("Export subset", mesh)
        scene.collection.objects.link(part)
        part.matrix_world = obj.matrix_world
        bpy.context.view_layer.update()
        ship.add_object(part, group, pivot=pivot)
        bpy.data.objects.remove(part, do_unlink=True)
        bpy.data.meshes.remove(mesh)
    bpy.data.meshes.remove(full)


def flag_variants(flag):
    selector = next(mod for mod in flag.modifiers if mod.type == 'NODES')
    menu = next(socket for socket in selector.node_group.interface.items_tree
                if getattr(socket, "socket_type", "") == "NodeSocketMenu")
    for group, label in (("FlagsPirate", "海盗旗"), ("FlagsBritish", "英国国旗")):
        getattr(selector.properties.inputs, menu.identifier).value = label
        flag.update_tag()
        bpy.context.view_layer.update()
        ship.add_object(flag, group, uv_layer="Cloth UV")


def export_visuals():
    for obj in list(scene.objects):
        name = obj.name
        if obj.type not in ("MESH", "CURVE") or obj.hide_render or name == "摄影地面":
            continue
        if name == "燕尾短旗":
            flag_variants(obj)
        elif obj.get("role") == "boarding_ladder":
            ship.add_object(obj, ladder_name(obj))
        elif name.startswith("舵轮"):
            ship.add_object(obj, "Wheel", pivot=position(obj.matrix_world.translation))
        elif name == "手压水泵":
            pivot = obj.matrix_world @ Vector((.145, -.035, 1.087))
            def lever_face(poly, mesh):
                material = mesh.materials[poly.material_index].name
                if any(pigment in material for pigment in ("927049", "bf9864")):
                    return True
                # Outer segment of the iron toe forward of the stationary through-pin.
                return ("锻铁" in material and poly.center.x > .18 and poly.center.z > .99)
            split_export(obj, [("PumpLever0", position(pivot), lever_face),
                               ("PumpBody", (0, 0, 0), lambda p, m: True)])
        elif name.startswith(("主帆", "艏帆")) and name != "主帆桅环":
            main = name.startswith("主帆")
            head = spec["rig"]["main_sail"]["peak"] if main else spec["rig"]["jib"]["head"]
            ship.add_object(obj, "SailMain" if main else "SailJib", pivot=position(head))
        elif name.startswith(("左舷舷墙", "右舷舷墙")):
            side = "Port" if name.startswith("左") else "Starboard"
            split_export(obj, [
                ("INNER_" + side, (0, 0, 0), lambda p, m: m.materials[p.material_index].name == "舷墙内板"),
                ("HULL_" + side, (0, 0, 0), lambda p, m: True)])
        elif name.startswith(("左舷 · ", "右舷 · ")):
            ship.add_object(obj, "HULL_" + ("Port" if name.startswith("左") else "Starboard"))
        elif name.startswith(("艉封板", "横艉舷墙")):
            ship.add_object(obj, "HULL_Transom")
        elif obj.get("舷别"):
            ship.add_object(obj, "DeckGuns")
        elif obj.type == "CURVE":
            ship.add_object(obj, "Cordage")
        elif name == "格栅舱口":
            # The source planks and hull backing already have an opening here.
            # Export the grating secured open, with a usable companionway below.
            continue
        elif name.startswith("贯通露天甲板"):
            ship.add_object(obj, "WeatherDeck")
        else:
            ship.add_object(obj, "TimberAndFittings")


class Collision:
    def __init__(self):
        self.vertices = []
        self.triangles = []

    def quad(self, corners, outward):
        first = len(self.vertices)
        self.vertices.extend(corners)
        for indices in ((0, 1, 2), (0, 2, 3)):
            a, b, c = (Vector(corners[i]) for i in indices)
            normal = (b - a).cross(c - a)
            if normal.length_squared < 1e-12:
                continue
            if normal.dot(Vector(outward)) < 0:
                indices = indices[::-1]
            self.triangles.append(tuple(first + i for i in indices))

    def box(self, xlo, xhi, ylo, yhi, zlo, zhi):
        def ring(y):
            return [(xlo, y, zlo), (xhi, y, zlo), (xhi, y, zhi), (xlo, y, zhi)]
        bottom, top = ring(ylo), ring(yhi)
        self.quad(top, (0, 1, 0))
        for i, normal in enumerate(((0, 0, -1), (1, 0, 0), (0, 0, 1), (-1, 0, 0))):
            j = (i + 1) % 4
            self.quad((bottom[i], bottom[j], top[j], top[i]), normal)

    def write(self, name):
        ship.add_geometry("Collision_" + name, self.vertices, self.triangles)


def export_companionway():
    """Build the game's small amidships compartment inside the untouched R06 hull."""
    floor, sides, ramp = Collision(), Collision(), Collision()
    deck_top = lambda z: baseline + shape.surface(z, 0)
    stair_start, stair_end = hatch_aft + .16, hatch_fore - .09
    landing_end = hatch_fore + .018
    stair_high = deck_top(landing_end)
    stair_half = .45
    floor.box(-cabin_half,cabin_half,cabin_floor-.055,cabin_floor,cabin_aft,cabin_fore)
    ramp.quad([(-stair_half,cabin_floor,stair_start), (stair_half,cabin_floor,stair_start),
               (stair_half,stair_high,stair_end), (-stair_half,stair_high,stair_end)],(0,1,0))
    ramp.quad([(-stair_half,stair_high,stair_end),(stair_half,stair_high,stair_end),
               (stair_half,stair_high,landing_end),(-stair_half,stair_high,landing_end)],(0,1,0))
    # A continuous collision skin covers the plank joints. The separate visual
    # boards carry the same colours and timber shader as the R06 weather deck.
    wood = [Collision() for _ in range(4)]
    def board(x0,x1,y0,y1,z0,z1,index):
        part = wood[index % len(wood)]
        part.box(x0,x1,y0,y1,z0,z1)
        part.quad([(x0,y0,z0),(x1,y0,z0),(x1,y0,z1),(x0,y0,z1)], (0,-1,0))

    for i in range(12):
        x = -cabin_half + 2*cabin_half*i/12
        joint = cabin_aft + 1.0 + .43*(i%5)
        for n,(lo,hi) in enumerate(((cabin_aft,joint),(joint,cabin_fore))):
            board(x+.002,x+2*cabin_half/12-.002,cabin_floor-.055,cabin_floor,lo+.002,hi-.002,i+n)
    # Side and end walls terminate the short walkable hold before the hull
    # narrows. Their tops stay just inside the underside of the weather deck.
    for i in range(8):
        lo = cabin_floor + i*.22
        hi = lo + .219
        for side in (-1,1):
            x = side*cabin_half
            board(x-.035,x+.035,lo,hi,cabin_aft,cabin_fore,i+side)
        for z in (cabin_aft,cabin_fore):
            board(-cabin_half,cabin_half,lo,hi,z-.035,z+.035,i)
    for x in (-cabin_half,cabin_half):
        sides.box(x-.035,x+.035,cabin_floor,2.80,cabin_aft,cabin_fore)
    for z in (cabin_aft,cabin_fore):
        sides.box(-cabin_half,cabin_half,cabin_floor,2.80,z-.035,z+.035)

    for i in range(9):
        a = stair_start + (stair_end-stair_start)*i/9
        b = stair_start + (stair_end-stair_start)*(i+1)/9
        top = cabin_floor + (stair_high-cabin_floor)*(i+1)/9
        board(-stair_half,stair_half,top-.10,top,a,b,i)
    board(-stair_half,stair_half,stair_high-.08,stair_high,stair_end,landing_end,1)
    for sign in (-1,1):
        x = sign*(stair_half-.05)
        vertices = [(x+dx, y-dy, z) for z,y in ((stair_start,cabin_floor),(stair_end,stair_high))
                    for dx,dy in ((-.035,.04),(.035,.04),(.035,.20),(-.035,.20))]
        for i,normal in enumerate(((0,1,0),(1,0,0),(0,-1,0),(-1,0,0))):
            j=(i+1)%4
            wood[0].quad([vertices[i],vertices[j],vertices[4+j],vertices[4+i]],normal)

    rim = deck_top((hatch_aft+hatch_fore)*.5)
    for sign in (-1,1):
        x = sign*(opening_half+.035)
        board(x-.035,x+.035,rim-.065,rim+.095,hatch_aft,hatch_fore,1)
        sides.box(x-.035,x+.035,rim-.065,rim+.095,hatch_aft,hatch_fore)
    # Lash the timber lattice upright to its port coaming, clear of both landings.
    x = -hatch_edge + .02
    lid_low, lid_high = rim+.095, rim+1.13
    for y in (lid_low,lid_high-.055):
        board(x-.023,x+.023,y,y+.055,hatch_aft,hatch_fore,0)
    for z in (hatch_aft,hatch_fore-.055):
        board(x-.023,x+.023,lid_low,lid_high,z,z+.055,0)
    for i in range(1,8):
        z = hatch_aft+(hatch_fore-hatch_aft)*i/8
        board(x-.018,x+.002,lid_low,lid_high,z-.022,z+.022,2)
    for i in range(1,6):
        y = lid_low+(lid_high-lid_low)*i/6
        board(x+.002,x+.022,y-.022,y+.022,hatch_aft,hatch_fore,3)
    sides.box(x-.023,x+.023,lid_low,lid_high,hatch_aft,hatch_fore)
    palette = list(scene.objects["贯通露天甲板 · 纵向铺板"].data.materials)
    for index,part in enumerate(wood):
        # Use the normal evaluated mesh export so new timber has the model's
        # grain UVs, flat face normals and material pigment in the game.
        mesh = bpy.data.meshes.new("Companionway timber")
        mesh.from_pydata([(z,-x,y-baseline) for x,y,z in part.vertices],[],
                         [tuple(reversed(tri)) for tri in part.triangles])
        mesh.materials.append(palette[index])
        obj = bpy.data.objects.new("Companionway timber",mesh)
        scene.collection.objects.link(obj)
        bpy.context.view_layer.update()
        ship.add_object(obj,"Companionway")
        bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.meshes.remove(mesh)
    floor.write("CabinFloor")
    sides.write("CabinWalls")
    ramp.write("Companionway")


def export_collision():
    deck, shell, furniture = Collision(), Collision(), Collision()
    stations = sorted(set(shape.xs + [hatch_aft, hatch_fore, cabin_aft, cabin_fore]))
    for a, b in zip(stations, stations[1:]):
        in_hatch = hatch_aft <= a and b <= hatch_fore
        for row in range(8):
            corners = []
            for z, ratio in ((a, row/4-1), (a, (row+1)/4-1),
                             (b, (row+1)/4-1), (b, row/4-1)):
                x = ratio * shape.breadth(z)
                if in_hatch:
                    x = (-1 if row<4 else 1)*(opening_half+abs(ratio)*(shape.breadth(z)-opening_half))
                corners.append((x, baseline + shape.surface(z, x), z))
            deck.quad(corners, (0, 1, 0))
            if cabin_aft <= a and b <= cabin_fore:
                deck.quad([(x,y-.045,z) for x,y,z in corners], (0,-1,0))
        for side in (-1, 1):
            def hull(z, fraction):
                width, y = shape.half_section(z, fraction)
                return (side * width, y + baseline, z)
            profile = [f for f, _ in spec["section_profile"]]
            for lo, hi in zip(profile, profile[1:]):
                shell.quad((hull(a, lo), hull(b, lo), hull(b, hi), hull(a, hi)), (side, 0, 0))
            def rail(z, t, inner):
                if inner:
                    # Match the timber and cap, including the low gun notches.
                    x = max(0, shape.breadth(z) * (1-.02*t) - .06)
                    return side*x, baseline+shape.deck(z)+(shape.wall_height(z)+.04)*t, z
                return position(shape.wall(z, -side, t))
            for inner in (True, False):
                shell.quad((rail(a,0,inner), rail(b,0,inner), rail(b,1,inner), rail(a,1,inner)),
                           (-side if inner else side,0,0))
    # Both sides of the transom stop incoming shots and the sailor at the stern.
    for lo, hi in zip(spec["section_profile"], spec["section_profile"][1:]):
        w0, y0 = shape.half_section(0, lo[0])
        w1, y1 = shape.half_section(0, hi[0])
        shell.quad([(-w0, baseline+y0, 0), (w0, baseline+y0, 0),
                    (w1, baseline+y1, 0), (-w1, baseline+y1, 0)], (0, 0, -1))
    w, y = shape.breadth(0), baseline + shape.deck(0)
    for z, top, normal in ((0,.55,-1), (.065,.59,1)):
        shell.quad([(-w,y,z),(w,y,z),(w,y+top,z),(-w,y+top,z)], (0,0,normal))

    # Low broad features follow the actual fittings.
    for name, inset in (("木质货物箱",0),("舵台 · 舵柱",.015),
                        ("左艏系缆桩",0),("右艏系缆桩",0)):
        obj = scene.objects[name]
        pts = [position(obj.matrix_world @ Vector(v)) for v in obj.bound_box]
        (xl,xh), (yl,yh), (zl,zh) = [(min(p[i] for p in pts), max(p[i] for p in pts)) for i in range(3)]
        furniture.box(xl+inset,xh-inset,yl,yh,zl+inset,zh-inset)
    for z, half, height in ((10.2,.185,3.3), (11.8,.15,1.02)):
        y = baseline + shape.surface(z,0)
        furniture.box(-half,half,y,y+height,z-half,z+half)
    for z in shape.guns:
        for side in (-1, 1):
            xl, xh = sorted(side*(shape.breadth(z)-inset) for inset in (.92, .10))
            y = baseline + shape.deck(z)
            furniture.box(xl,xh,y,y+.30,z-.23,z+.23)
    deck.write("WeatherDeck")
    shell.write("HullAndBulwarks")
    furniture.write("DeckFittings")


def hit(planks, z, y, side, inside):
    origin = Vector((z, 0 if inside else side * 4, y))
    ray = Vector((0, side if inside else -side, 0))
    hits = []
    for obj in planks:
        inverse = obj.matrix_world.inverted()
        found, at, normal, _ = obj.ray_cast(inverse @ origin, inverse.to_3x3() @ ray)
        if found:
            at = obj.matrix_world @ at
            normal = (inverse.transposed().to_3x3() @ normal).normalized()
            assert normal.dot(ray) < -.5, (obj.name, z, inside)
            hits.append(((at-origin).length, position(at), direction(normal)))
    if not hits:
        raise ValueError((z, y, side, inside))
    return min(hits)[1:]


def measurements():
    guns, inside, outside, stands = [], [], [], []
    for side, prefix in ((1,"P"),(-1,"S")):
        for i, z in enumerate(shape.guns):
            # Breech interaction marker ahead of the player's chest, below the muzzle.
            gun = (z, side*(shape.breadth(z)-1.01), shape.deck(z)+.32)
            guns.append(marker(prefix+str(i+1), position(gun), (-side,0,0)))
    # Keep the original drill pair at the ends. The added bays clear the gun
    # carriages at 3.8, 6.65, 11.85 and 14.5 m along the open deck.
    bays = ((1,5.0,-1), (2,8.0,1), (3,10.8,-1),
            (5,2.7,-1), (6,8.55,-1), (7,10.4,1), (4,13.4,1))
    for index, z, side in bays:
        prefix = "左舷" if side == 1 else "右舷"
        walls = [obj for obj in scene.objects if obj.name.startswith(prefix + "舷墙")]
        boards = [obj for obj in scene.objects if obj.name.startswith(prefix + " · ")]
        inner, normal = hit(walls,z,shape.deck(z)+.28,side,True)
        inside.append(marker("Inner breach "+str(index), inner, normal))
        # Avoid the narrow caulking seam passing this forward quarter at y=1.65.
        outside.append(marker("Outer breach "+str(index), *hit(boards,z,1.69,side,False)))
        # From the open deck the low repair mark competes with the higher jib
        # sheet and breeches for the nearest interaction to the sailor's chest.
        foot_x = inner[0] + side * .48
        stands.append(marker("Repair stand "+str(index), (foot_x, baseline+shape.surface(z,foot_x), z), (-side,0,0)))
    deck = baseline + min(shape.deck(z) for z in shape.xs)
    crown = baseline + max(shape.surface(z,0) for z in shape.xs)
    max_height = baseline + max(shape.deck(z) + shape.wall_height(z) for z in shape.xs)
    widths = []
    for row in range(51):
        y = row * max_height / 50 - baseline
        for col in range(62):
            z = col * shape.length / 61
            bottom, edge, width = shape.bottom(z), shape.deck(z), shape.breadth(z)
            if y < bottom or edge <= bottom:
                value = 0
            elif y >= edge:
                value = width * (1 - .02 * min(1, (y-edge)/.55))
            else:
                value = width * shape.section_width((y-bottom)/(edge-bottom))
            widths.append(round(value, 4))
    helm = position(scene.objects["舵轮 · 八柄"].matrix_world.translation)
    pump = scene.objects["手压水泵"].matrix_world.translation
    # The jib sheet meets the deck beside the mast. The forward starboard bitt
    # is the anchor cable station on this open boat without a capstan drum.
    return dict(length=shape.length, beam=spec["max_beam_at_deck_edge"], dryDraft=1.65, dryHeight=.45,
                lowerDeck=deck, mainDeck=deck, upperDeck=deck, deckCrown=round(crown,5),
                holdAft=.08, holdFore=17.92, holdCeiling=deck-.16, liningInset=.06,
                lowerAft=0, lowerFore=shape.length, maxHullHeight=round(max_height,5), widths=widths,
                spawn=marker("Aft watch",position((1.25,-.45,shape.surface(1.25,-.45)))),
                helm=marker("Helm", (helm[0],helm[1],helm[2]-.10)),
                sails=marker("Jib sheet",position((10.86,.922,3.085))),
                capstan=marker("Anchor cable bitt",position((16.08,-.42,shape.surface(16.2,.42)+.43))),
                pumps=[marker("Pump handle",position(pump+Vector((-.73,-.035,1.24))))],
                wheelAxis=vector((0,0,1)),pumpAxis=vector((1,0,0)),
                guns=guns,inside=inside,outside=outside,repairStands=stands)


data = measurements()
data["boardingLadders"] = [boarding_ladder(obj, scene.objects[("左" if obj["side"] > 0 else "右") + "舷扶手沿条"],
                                           shape.surface, baseline)
                           for obj in scene.objects if obj.get("role") == "boarding_ladder"]
assert len(data["boardingLadders"]) == 2
data.update(source_identity(source))
export_visuals()
export_collision()
export_companionway()
ship.write(out / "Model.r13mesh")
(out / "Rig.json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf8")
print("R06 sloop exported", out, len(data["guns"]), "guns,", len(data["inside"]), "breach pairs")
