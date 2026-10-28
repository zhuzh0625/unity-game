"""Run inside the connected Blender instance using Blender MCP.

Creates an isolated scene, saves the authored source outside Unity Assets,
and exports only the model collection as FBX. Existing scenes are preserved.
"""
import bpy
import math
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion

project = Path(r"D:\workspace\game\unity-demo")
blend_path = project / "ArtSource/CollectorCrystal.blend"
fbx_path = project / "Assets/CollectorDemo/Models/CollectorCrystal.fbx"
if blend_path.exists() or fbx_path.exists():
    raise RuntimeError("Crystal output exists; inspect before overwriting.")
existing_scene = bpy.data.scenes.get("CollectorCrystal_Studio")
if existing_scene and len(existing_scene.objects):
    raise RuntimeError("Crystal scene already has objects; inspect instead of rebuilding.")
if bpy.context.mode != 'OBJECT':
    raise RuntimeError("Switch Blender to Object mode before creating the asset.")

original_scene = 'Scene' if existing_scene else bpy.context.scene.name
scene = existing_scene or bpy.data.scenes.new("CollectorCrystal_Studio")
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
collection = bpy.data.collections.get("CollectorCrystal_Export") or bpy.data.collections.new("CollectorCrystal_Export")
if collection.name not in scene.collection.children:
    scene.collection.children.link(collection)
bpy.context.window.scene = scene

def material(name, color, metallic, roughness):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return m

materials = [
    material('Crystal_Amber', (1.0, .48, .055), .35, .24),
    material('Crystal_Honey', (1.0, .72, .18), .28, .20),
    material('Crystal_Shadow', (.58, .16, .018), .40, .28),
    material('Crystal_Tip', (1.0, .91, .49), .22, .20),
    material('Crystal_Band', (.055, .095, .12), .80, .25),
    material('Crystal_Trim', (.24, .95, .70), .45, .22),
]
root = bpy.data.objects.new('CollectorCrystal', None)
collection.objects.link(root)

def mesh_object(name, verts, faces, slots, transform=Matrix.Identity(4)):
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata([tuple(transform @ Vector(v)) for v in verts], [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    for mat in materials:
        mesh.materials.append(mat)
    for face, slot in zip(mesh.polygons, slots):
        face.material_index = slot
        face.use_smooth = False
    # Basic cylindrical UVs are included even though this model uses material colors.
    uv = mesh.uv_layers.new(name='UVMap')
    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = ((math.atan2(co.y, co.x)/(2*math.pi)) % 1, (co.z+.85)/1.8)
    return obj

def crystal(name, transform):
    n = 6
    rings = [(-.40, .25), (-.15, .38), (.30, .38), (.50, .27)]
    verts = []
    for z, radius in rings:
        for i in range(n):
            theta = math.tau*i/n + math.pi/6
            verts.append((radius*math.cos(theta), radius*math.sin(theta), z))
    bottom = len(verts); verts.append((0, 0, -.78))
    top = len(verts); verts.append((.035, 0, .96))
    faces = []; slots = []
    for i in range(n):
        j = (i+1)%n
        faces.append((bottom,j,i)); slots.append(2 if i%2 else 0)
    for k in range(len(rings)-1):
        for i in range(n):
            j = (i+1)%n
            faces.append((k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i))
            slots.append([0,1,0,2,0,1][i] if k!=2 else (3 if i%2 else 1))
    for i in range(n):
        faces.append(((len(rings)-1)*n+i,(len(rings)-1)*n+(i+1)%n,top))
        slots.append(3 if i%2 else 1)
    return mesh_object(name, verts, faces, slots, transform)

main = crystal('Core_Faceted', Matrix.Identity(4))
crystal('Shard_Left', Matrix.Translation((-.37,0,-.18)) @ Matrix.Rotation(math.radians(-24),4,'Y') @ Matrix.Diagonal((.46,.46,.53,1)))
crystal('Shard_Right', Matrix.Translation((.37,.03,-.24)) @ Matrix.Rotation(math.radians(29),4,'Y') @ Matrix.Diagonal((.39,.39,.43,1)))

def band(name, low, high, inner, outer, slot):
    n=6; verts=[]; faces=[]
    for z,r in [(low,outer),(high,outer),(low,inner),(high,inner)]:
        for i in range(n):
            a=math.tau*i/n+math.pi/6
            verts.append((r*math.cos(a),r*math.sin(a),z))
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),
                      (n+i,n+j,3*n+j,3*n+i),(j,i,2*n+i,2*n+j)])
    return mesh_object(name,verts,faces,[slot]*len(faces))

band('Hex_ContainmentBand',-.20,-.08,.355,.425,4)
band('Energy_Inlay',-.14,-.11,.422,.435,5)

# A separate scene makes the .blend source self-contained without altering the user's scene.
scene.world = bpy.data.worlds.new('CrystalStudioWorld')
scene.world.color = (.10,.10,.10)
bpy.context.view_layer.update()
for obj in scene.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = main
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.type = 'SOLID'
            area.spaces.active.shading.color_type = 'MATERIAL'
            area.spaces.active.shading.light = 'STUDIO'
            area.spaces.active.shading.show_cavity = True
            area.spaces.active.region_3d.view_location = Vector((0,0,.08))
            area.spaces.active.region_3d.view_distance = 3.7
            area.spaces.active.region_3d.view_rotation = Quaternion((.88,.32,.18,.30)).normalized()

blend_path.parent.mkdir(parents=True, exist_ok=True)
fbx_path.parent.mkdir(parents=True, exist_ok=True)
bpy.data.libraries.write(str(blend_path), {scene}, path_remap='RELATIVE_ALL', fake_user=True, compress=True)
export_status = bpy.ops.export_scene.fbx(
    filepath=str(fbx_path), use_selection=True, object_types={'EMPTY','MESH'},
    global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z', axis_up='Y', bake_space_transform=False,
    use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
    bake_anim=False, add_leaf_bones=False, path_mode='AUTO')
meshes = [o for o in collection.objects if o.type=='MESH']
for obj in meshes:
    obj.data.calc_loop_triangles()
result = {
    'scene': scene.name, 'previous_scene_preserved': original_scene,
    'blender_version': bpy.app.version_string,
    'blend': str(blend_path), 'fbx': str(fbx_path), 'export_status': list(export_status),
    'mesh_objects': len(meshes), 'vertices': sum(len(o.data.vertices) for o in meshes),
    'triangles': sum(len(o.data.loop_triangles) for o in meshes),
    'fbx_bytes': fbx_path.stat().st_size,
    'mesh_names': [o.name for o in meshes]
}
