"""Original Coast Rush assets. Run inside Blender, or blender --background --python this_file.
All geometry is authored in metres, +Y forward / +Z up, exported as FBX.
The user's original scene is retained; only the named CoastRushSource scene is regenerated.
"""
import bpy
import math
import os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'CoastRush', 'Models')
os.makedirs(OUT, exist_ok=True)
scene = bpy.data.scenes.get('CoastRushSource')
if scene and len(scene.objects):
    raise RuntimeError('CoastRushSource already exists; open a fresh Blender session to regenerate safely.')
scene = scene or bpy.data.scenes.new('CoastRushSource')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.engine = 'BLENDER_EEVEE'

def mat(name, color, metal=0, rough=.55):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    node = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Metallic'].default_value = metal
    node.inputs['Roughness'].default_value = rough
    return m

paint = mat('CarPaint', (.95,.24,.075), .38, .28)
navy = mat('Carbon', (.025,.055,.075), .2, .38)
glass = mat('Glass', (.045,.19,.24), .62, .17)
rubber = mat('TireRubber', (.025,.03,.033), .05, .9)
alloy = mat('WheelAlloy', (.72,.79,.81), .8, .22)
light = mat('Headlight', (.83,.97,1), .2, .18)
brake = mat('BrakeLight', (1,.08,.025), .1, .25)
ivory = mat('Ivory', (.89,.83,.66))
white = mat('Chalk', (.94,.95,.87))
roof = mat('Terracotta', (.68,.20,.11))
teal = mat('ShutterTeal', (.045,.39,.43))
wood = mat('PalmBark', (.39,.25,.13))
leaf = mat('PalmLeaf', (.12,.39,.15))
leaflight = mat('PalmLeafLight', (.28,.54,.19))
stone = mat('WarmStone', (.62,.52,.36))
roadmat = mat('RoadAsphalt', (.12,.17,.19))
railmat = mat('GuardrailSilver', (.56,.64,.66), .45)

def group(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    r = bpy.data.objects.new(name, None)
    c.objects.link(r)
    return c,r

def adopt(obj, collection, parent, material):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)
    if material: obj.data.materials.append(material)
    if parent: obj.parent = parent
    return obj

def cube(name, pos, size, material, collection, parent, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    o = bpy.context.object
    o.name=name
    o.scale=size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    adopt(o,collection,parent,material)
    if bevel:
        b=o.modifiers.new('Soft manufactured edges','BEVEL'); b.width=bevel; b.segments=2
        o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
    return o

def mesh(name, verts, faces, material, collection, parent):
    m=bpy.data.meshes.new(name+'Mesh'); m.from_pydata(verts,[],faces); m.update()
    o=bpy.data.objects.new(name,m); collection.objects.link(o); o.parent=parent
    if material: m.materials.append(material)
    return o

def cylinder(name,pos,radius,depth,material,collection,parent,vertices=16,rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=pos)
    o=bpy.context.object; o.name=name
    if rotation: o.rotation_euler=rotation
    adopt(o,collection,parent,material)
    for p in o.data.polygons: p.use_smooth=len(p.vertices)==4
    return o

exports=[]
def flatten_wheels(collection, root):
    # Blender's experimental FBX space bake mis-converts meshes below nested empties.
    # Export each wheel as one multi-material mesh with its origin at the axle.
    for name in ['WheelFL','WheelFR','WheelRL','WheelRR']:
        pivot=bpy.data.objects.get(name)
        parts=[o for o in pivot.children if o.type=='MESH']
        if not parts: continue
        bpy.ops.object.select_all(action='DESELECT')
        for part in parts: part.select_set(True)
        bpy.context.view_layer.objects.active=next(o for o in parts if '_Tire' in o.name)
        bpy.ops.object.join()
        wheel=bpy.context.object
        world=wheel.matrix_world.copy()
        wheel.parent=root;wheel.matrix_world=world
        pivot.name=name+'_SourcePivot'
        wheel.name=name
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        bpy.data.objects.remove(pivot,do_unlink=True)

def export(c,r):
    bpy.ops.object.select_all(action='DESELECT')
    for o in c.objects: o.select_set(True)
    bpy.context.view_layer.objects.active=r
    path=os.path.join(OUT,r.name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        bake_space_transform=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
    exports.append({'name':r.name,'objects':len(c.objects),'path':path})

# A compact GT coupe: shaped body, separate glass, wheel pivots, lights and aero.
c,r=group('CoastGT')
sections=[(-2.13,.73,.40,.70),(-1.76,.99,.32,.81),(-.72,1.0,.30,.91),
          (.55,.96,.30,.86),(1.52,.94,.34,.69),(2.1,.74,.41,.60)]
verts=[]
for y,w,low,high in sections:
    verts.extend([(-w*.82,y,low),(-w,y,low+.17),(-w*.94,y,high),(w*.94,y,high),(w,y,low+.17),(w*.82,y,low)])
faces=[tuple(range(5,-1,-1))]
for i in range(len(sections)-1):
    for j in range(6): faces.append((i*6+j,i*6+(j+1)%6,(i+1)*6+(j+1)%6,(i+1)*6+j))
faces.append(tuple(range((len(sections)-1)*6,len(sections)*6)))
body=mesh('SculptedBody',verts,faces,paint,c,r)
b=body.modifiers.new('Body edge bevel','BEVEL');b.width=.055;b.segments=3
body.modifiers.new('Body normals','WEIGHTED_NORMAL')
cabin=[(-.81,-1.10,.83),(.81,-1.10,.83),(.76,.68,.82),(-.76,.68,.82),
       (-.62,-.63,1.37),(.62,-.63,1.37),(.57,.12,1.36),(-.57,.12,1.36)]
mesh('PanoramicCabin',cabin,[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],glass,c,r)
cube('RoofPanel',(0,-.27,1.385),(1.23,.77,.055),paint,c,r,.035)
for x in [-.13,.13]:
    stripe=cube('HoodStripe',(x,1.32,.75),(.14,1.23,.015),navy,c,r,.008)
    stripe.rotation_euler[0]=-.17
    cube('RoofStripe',(x,-.27,1.419),(.14,.75,.01),navy,c,r)
for x in [-.97,.97]:
    cube('SideSkirt',(x,-.05,.36),(.10,2.8,.15),navy,c,r,.03)
    cube('Mirror',(x,.23,1.02),(.21,.26,.12),paint,c,r,.035)
cube('FrontSplitter',(0,2.02,.35),(1.75,.30,.09),navy,c,r,.025)
cube('FrontIntake',(0,2.105,.49),(.94,.025,.14),navy,c,r,.02)
for x in [-.57,.57]:
    cube('Headlamp',(x,2.03,.61),(.37,.12,.065),light,c,r,.025)
    cube('RearLamp',(x,-2.085,.64),(.43,.055,.075),brake,c,r,.015)
    cylinder('Exhaust',(x*.76,-2.11,.39),.075,.15,alloy,c,r,12,(math.pi/2,0,0))
for x in [-.62,.62]: cube('WingStrut',(x,-1.82,.98),(.06,.13,.36),navy,c,r)
cube('RearWing',(0,-1.82,1.17),(2.03,.40,.085),navy,c,r,.03)
for x in [-1.0,1.0]: cube('WingEndplate',(x,-1.82,1.19),(.045,.46,.19),paint,c,r,.02)
for name,x,y in [('WheelFL',-.94,1.28),('WheelFR',.94,1.28),('WheelRL',-.94,-1.28),('WheelRR',.94,-1.28)]:
    pivot=bpy.data.objects.new(name,None); c.objects.link(pivot);pivot.parent=r;pivot.location=(x,y,.38)
    tire=cylinder(name+'_Tire',(0,0,0),.365,.25,rubber,c,pivot,24,(0,math.pi/2,0))
    for side in [-1,1]:
        cylinder(name+'_Rim',(side*.133,0,0),.246,.018,alloy,c,pivot,16,(0,math.pi/2,0))
        cylinder(name+'_Hub',(side*.147,0,0),.075,.03,navy,c,pivot,12,(0,math.pi/2,0))
        for k in range(5):
            a=k*math.tau/5
            spoke=cube(name+'_Spoke',(side*.15,math.sin(a)*.13,math.cos(a)*.13),(.025,.048,.24),navy,c,pivot,.008)
            spoke.rotation_euler[0]=-a
for name,pos in [('ForwardMarker',(0,3,0)),('RightMarker',(2,0,0))]:
    o=bpy.data.objects.new(name,None);c.objects.link(o);o.parent=r;o.location=pos
flatten_wheels(c,r)
export(c,r)

# Palm with a curved ring trunk and sculpted leaf ribbons.
c,r=group('CoastPalm')
v=[];f=[]
for i in range(9):
    z=i*.72; cx=.055*i*i; radius=.31-i*.018
    for j in range(8):
        a=j*math.tau/8;v.append((cx+math.cos(a)*radius,math.sin(a)*radius,z))
for i in range(8):
    for j in range(8): f.append((i*8+j,i*8+(j+1)%8,(i+1)*8+(j+1)%8,(i+1)*8+j))
mesh('CurvedTrunk',v,f,wood,c,r)
top=Vector((3.52,0,5.76))
for j in range(8):
    a=j*math.tau/8;direction=Vector((math.cos(a),math.sin(a),0));side=Vector((-math.sin(a),math.cos(a),0))
    v=[]
    for k in range(7):
        t=k/6;center=top+direction*(t*3.6)+Vector((0,0,1.4*math.sin(t*math.pi)-1.25*t))
        w=.47*math.sin(t*math.pi)**.7
        v.extend([center-side*w,center+Vector((0,0,.12*math.sin(t*math.pi))),center+side*w])
    f=[]
    for k in range(6):f.extend([(k*3,(k+1)*3,(k+1)*3+1,k*3+1),(k*3+1,(k+1)*3+1,(k+1)*3+2,k*3+2)])
    o=mesh('PalmFrond',v,f,leaf if j%2 else leaflight,c,r)
    m=o.modifiers.new('Leaf thickness','SOLIDIFY');m.thickness=.025
export(c,r);r.location.x=10

# Mediterranean roadside house with terracotta roof, shutters, porch and chimney.
c,r=group('CoastHouse')
cube('StuccoWalls',(0,0,2.1),(6.8,5.2,4.2),ivory,c,r,.09)
mesh('PitchedRoof',[(-3.7,-2.9,4.15),(3.7,-2.9,4.15),(0,-2.9,5.65),(-3.7,2.9,4.15),(3.7,2.9,4.15),(0,2.9,5.65)],
     [(0,1,2),(3,5,4),(0,3,4,1),(0,2,5,3),(1,4,5,2)],roof,c,r)
cube('Chimney',(1.8,.8,5.0),(.7,.7,1.8),white,c,r,.04)
cube('FrontDoor',(0,2.63,1.1),(1.15,.08,2.2),teal,c,r,.025)
for x in [-2.05,2.05]:
    for z in [1.5,3.15]:
        cube('WindowFrame',(x,2.65,z),(1.3,.15,1.05),white,c,r,.02)
        cube('WindowGlass',(x,2.74,z),(1.06,.05,.82),glass,c,r)
        for dx in [-.78,.78]: cube('Shutter',(x+dx,2.68,z),(.32,.12,.95),teal,c,r,.015)
cube('Porch',(0,3.1,.16),(3.0,1.2,.32),white,c,r,.03)
export(c,r);r.location.x=23

c,r=group('CoastLighthouse')
for k in range(5):
    bpy.ops.mesh.primitive_cone_add(vertices=16,radius1=2.0-k*.19,radius2=1.81-k*.19,depth=2.2,location=(0,0,1.1+k*2.2))
    adopt(bpy.context.object,c,r,white if k%2==0 else roof).name='TowerBand'
cylinder('LanternPlatform',(0,0,11.1),1.65,.30,navy,c,r)
cylinder('LanternGlass',(0,0,12.0),1.1,1.5,glass,c,r)
for k in range(8):
    a=k*math.tau/8;cube('LanternFrame',(math.cos(a)*1.1,math.sin(a)*1.1,12),(.09,.09,1.55),navy,c,r)
bpy.ops.mesh.primitive_cone_add(vertices=16,radius1=1.7,radius2=0,depth=1.25,location=(0,0,13.3))
adopt(bpy.context.object,c,r,roof).name='LanternRoof'
export(c,r);r.location.x=36

c,r=group('CoastRock')
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1)
o=bpy.context.object;o.name='FacetedRock';o.scale=(3,2.1,2.5);o.location.z=1.3
adopt(o,c,r,stone)
export(c,r);r.location.x=47

c,r=group('Guardrail')
for x in [-3,0,3]: cube('RailPost',(x,0,.7),(.12,.16,1.4),railmat,c,r)
cube('SafetyRail',(0,0,1.1),(8,.13,.33),railmat,c,r,.04)
cube('RailHighlight',(0,.08,1.12),(8,.03,.05),white,c,r)
export(c,r);r.location.x=58

c,r=group('TunnelArch')
v=[]
for y in [-3.5,3.5]:
    for radius in [8.1,9.0]:
        for j in range(17):
            a=j*math.pi/16;v.append((math.cos(a)*radius,y,.05+math.sin(a)*radius))
f=[]
for j in range(16):
    f.extend([(j,j+1,35+j,34+j),(17+j,51+j,52+j,18+j),(j,17+j,18+j,j+1),(34+j,35+j,52+j,51+j)])
f.extend([(0,34,51,17),(16,33,67,50)])
mesh('StoneVault',v,f,stone,c,r)
for x in [-7.8,7.8]:cube('TunnelReflector',(x,0,1.0),(.08,.8,.22),light,c,r)
export(c,r);r.location.x=75

c,r=group('RoadModule')
cube('Asphalt',(0,0,-.16),(14,8,.32),roadmat,c,r)
for x in [-6.75,6.75]:cube('EdgeLine',(x,0,.01),(.14,8,.02),white,c,r)
cube('CentreDash',(0,0,.015),(.16,3.2,.025),white,c,r)
export(c,r);r.location.x=100

# A useful source-file view, with the car at the origin and the library laid out beside it.
bpy.ops.object.select_all(action='DESELECT')
car=bpy.data.objects.get('CoastGT');car.select_set(True);bpy.context.view_layer.objects.active=car
scene.world=bpy.data.worlds.new('CoastRushStudio');scene.world.color=(.15,.20,.24)
bpy.ops.object.light_add(type='AREA',location=(1,3,7));bpy.context.object.data.energy=1600;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=7
bpy.ops.object.camera_add(location=(6,8,4))
camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.6))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=48;scene.camera=camera
scene.render.resolution_x=1200;scene.render.resolution_y=800;scene.render.resolution_percentage=100
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource','CoastRushAssets.blend'))
result={'exports':exports,'blend':bpy.data.filepath,'blender':bpy.app.version_string}
