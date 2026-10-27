"""Rebuild the original low-poly art in Blender. Run via Blender MCP or --background --python."""
import bpy, math, os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Models')
os.makedirs(OUT, exist_ok=True)
os.makedirs(os.path.join(ROOT, 'ArtSource'), exist_ok=True)
previous = bpy.context.window.scene if bpy.context.window else bpy.context.scene
scene = bpy.data.scenes.new('IronBastion_AssetStudio')
if bpy.context.window:
    bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
materials = {}
palette = {'Paint':(0.94,0.63,0.16), 'Track':(0.075,0.095,0.105),
 'Metal':(0.24,0.29,0.29), 'Trim':(0.95,0.85,0.53), 'Dark':(0.09,0.14,0.16),
 'Brick':(0.62,0.24,0.13), 'Mortar':(0.33,0.18,0.13), 'Steel':(0.34,0.45,0.47),
 'Leaf':(0.12,0.29,0.23), 'LeafLight':(0.22,0.4,0.28), 'Trunk':(0.28,0.20,0.12),
 'Base':(0.38,0.43,0.38), 'Beacon':(0.35,0.95,0.81)}
for name,c in palette.items():
    mat = bpy.data.materials.new('IB_'+name)
    mat.diffuse_color=(*c,1)
    mat.use_nodes=True
    p=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*c,1)
    p.inputs['Roughness'].default_value=.64
    p.inputs['Metallic'].default_value=.4 if name in ['Metal','Steel','Trim'] else .05
    materials[name]=mat

def finish(obj,name,mat,bevel=0):
    obj.name=name
    obj.data.materials.append(materials[mat])
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=obj.modifiers.new('Soft machined edges','BEVEL'); m.width=bevel; m.segments=2
        bpy.ops.object.modifier_apply(modifier=m.name)
        n=obj.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=n.name)
    return obj

def box(name,loc,scale,mat,bevel=.03):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object; o.dimensions=scale
    return finish(o,name,mat,bevel)

def cylinder(name,loc,radius,depth,mat,vertices=12,rot=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc,rotation=rot)
    return finish(bpy.context.object,name,mat,.014)

def export(name,build):
    before=set(scene.objects)
    build()
    objects=set(scene.objects)-before
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active=next(iter(objects))
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,
        object_types={'MESH'},apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',
        bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    coll=bpy.data.collections.new(name); scene.collection.children.link(coll)
    for o in objects:
        for c in list(o.users_collection): c.objects.unlink(o)
        coll.objects.link(o)
    return objects

def tank():
    for x in [-.57,.57]:
        box('Track assembly',(x,0,.26),(.31,1.64,.44),'Track',.10)
        for y in [-.58,-.29,0,.29,.58]:
            cylinder('Road wheel',(x+(-.16 if x<0 else .16),y,.25),.16,.035,'Metal',12,(0,math.pi/2,0))
        for y in [-.65,-.43,-.21,.01,.23,.45,.67]:
            box('Tread rib',(x,y,.47),(.33,.085,.045),'Metal',.008)
        box('Fender',(x,0,.52),(.37,1.74,.10),'Paint',.035)
    box('Armored hull',(0,0,.48),(.96,1.42,.38),'Paint',.12)
    box('Engine deck',(0,.47,.70),(.69,.36,.08),'Dark',.02)
    for x in [-.24,-.12,0,.12,.24]: box('Vent',(x,.47,.75),(.035,.29,.025),'Metal',.004)
    cylinder('Turret ring',(0,-.12,.74),.43,.13,'Metal')
    cylinder('Octagonal turret',(0,-.15,.89),.44,.33,'Paint',8)
    cylinder('Commander hatch',(.12,-.08,1.08),.17,.06,'Trim')
    box('Gun mantlet',(0,-.55,.86),(.3,.2,.24),'Paint',.04)
    cylinder('Cannon barrel',(0,-.93,.87),.075,.73,'Metal',10,(math.pi/2,0,0))
    box('Muzzle brake',(0,-1.31,.87),(.19,.21,.16),'Dark',.025)
    for x in [-.36,.36]: box('Headlamp',(x,-.74,.61),(.13,.045,.11),'Trim',.02)
    box('Recognition stripe',(-.2,-.20,1.06),(.07,.40,.02),'Trim',.002)
    cylinder('Antenna',(-.3,.36,1.13),.012,.62,'Dark',6)

def brick():
    box('Mortar core',(0,0,.39),(.94,.94,.78),'Mortar',.025)
    for z in [.14,.40,.66]:
        for x in [-.245,.245]:
            box('Baked brick',(x,-.01,z),(.46,.97,.22),'Brick',.028)

def steel():
    box('Concrete core',(0,0,.49),(1.91,1.91,.98),'Dark',.05)
    box('Steel casing',(0,0,.54),(1.85,1.85,.91),'Steel',.08)
    box('Recessed top',(0,0,1.01),(1.51,1.51,.035),'Metal',.025)
    for x in [-.72,.72]:
        for y in [-.72,.72]: cylinder('Rivet',(x,y,1.04),.075,.05,'Trim',8)
    for x in [-.63,.63]: box('Reinforcement',(x,0,1.035),(.12,1.48,.045),'Steel',.01)

def hq():
    box('Foundation',(0,0,.16),(1.8,1.8,.32),'Dark',.10)
    box('Bunker',(0,.1,.56),(1.5,1.3,.75),'Base',.18)
    box('Roof',(0,.1,1.01),(1.7,1.45,.16),'Metal',.07)
    box('Door',(0,-.57,.52),(.46,.06,.6),'Dark',.03)
    box('Signal stripe',(0,-.61,.83),(1.12,.035,.10),'Beacon',.015)
    cylinder('Signal core',(0,.1,1.16),.22,.23,'Beacon',8)
    cylinder('Mast',(.55,.3,1.48),.035,.95,'Trim',8)
    box('Flag',(.74,.3,1.75),(.38,.035,.24),'Paint',.01)
    for x in [-.48,.48]:
        box('Wing emblem',(x,-.64,.54),(.28,.06,.11),'Trim',.02)

def tree():
    cylinder('Trunk',(0,0,.43),.14,.86,'Trunk',7)
    for z,r in [(.9,.74),(1.3,.58),(1.68,.38)]:
        bpy.ops.mesh.primitive_cone_add(vertices=7,radius1=r,radius2=0,depth=.91,location=(0,0,z))
        finish(bpy.context.object,'Pine canopy','Leaf' if z<1.1 else 'LeafLight')

models={name:export(name,fn) for name,fn in [('Tank',tank),('Brick',brick),('Steel',steel),('Headquarters',hq),('Pine',tree)]}
# Arrange the editable source as an asset sheet, while the FBX files stay origin-centered.
for i,(name,objects) in enumerate(models.items()):
    for o in objects: o.location.x+=(i-2)*3.1
bpy.data.libraries.write(os.path.join(ROOT,'ArtSource','IronBastion.blend'),{scene},fake_user=True)
if bpy.context.window: bpy.context.window.scene=previous
result={'models':list(models),'fbx_directory':OUT,'source':os.path.join(ROOT,'ArtSource','IronBastion.blend'),'polygons':sum(len(o.data.polygons) for group in models.values() for o in group)}
print(result)
