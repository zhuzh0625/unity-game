"""在原模型源文件中添加侧视开口陷阱模块；保留其他 Blender 场景。"""
import bpy
from pathlib import Path

root_path = Path(__file__).resolve().parents[1]
studio = bpy.data.scenes['CloudSprout_ArtStudio']
previous_scene = bpy.context.window.scene
if bpy.data.objects.get('SpikePitVisual'):
    raise RuntimeError('SpikePitVisual 已存在，请编辑现有模型，避免重复覆盖。')
bpy.context.window.scene = studio
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
root = bpy.data.objects.new('SpikePitVisual', None)
studio.collection.objects.link(root)

def box(name, position, size, material, bevel=.025):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.parent = root
    obj.location = (position[0], -position[2], position[1])
    obj.scale = (size[0], size[2], size[1])
    obj.data.materials.append(bpy.data.materials[material])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    modifier = obj.modifiers.new('Soft stone edges', 'BEVEL')
    modifier.width = bevel
    modifier.segments = 2
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj

# Open toward the side camera. The dark rear wall separates pale spikes from foliage.
box('Pit dark back wall', (0,-.58,1.15), (1,1.16,.20), 'CS_Ink')
box('Pit bottom', (0,-1.18,0), (1,.20,2.6), 'CS_Rock')
box('Pit front bottom rim', (0,-1.14,-1.27), (1,.16,.16), 'CS_Ink')
for x in [-.47,.47]:
    box('Pit side wall', (x,-.57,0), (.06,1.14,2.6), 'CS_Rock', .015)
    box('Pit edge stone', (x,-.025,0), (.10,.15,2.70), 'CS_Rock', .018)
    box('Pit warning cap', (x,.047,-.05), (.07,.035,2.68), 'CS_Gold', .008)

bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
for obj in root.children_recursive:
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
out = root_path / 'Assets/CloudSprout/Models/SpikePit.fbx'
bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True,
    object_types={'EMPTY','MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False)
root.location = (5,15,0)
bpy.ops.wm.save_as_mainfile(filepath=str(root_path/'ArtSource/CloudSprout.blend'), copy=True)
bpy.context.window.scene = previous_scene
result = {'asset':str(out), 'root':root.name, 'source':str(root_path/'ArtSource/CloudSprout.blend')}
