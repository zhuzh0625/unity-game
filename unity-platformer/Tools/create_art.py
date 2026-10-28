"""原创《云芽冒险》模型。由 Blender Python 执行；不修改用户原有场景。"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/CloudSprout/Models'
SOURCE = ROOT / 'ArtSource'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
if (SOURCE / 'CloudSprout.blend').exists():
    raise RuntimeError('已有建模源文件，拒绝覆盖。')
original_scene = bpy.data.scenes.get('Scene') or bpy.context.scene
scene = bpy.data.scenes.get('CloudSprout_ArtStudio') or bpy.data.scenes.new('CloudSprout_ArtStudio')
if len(scene.objects): raise RuntimeError('建模场景非空，拒绝重复创建。')
scene.unit_settings.system='METRIC'
bpy.context.window.scene = scene
palette = {
 'Mint':(.18,.60,.39), 'Leaf':(.33,.73,.28), 'Grass':(.43,.77,.25),
 'DarkLeaf':(.08,.29,.25), 'Bark':(.30,.16,.09), 'Earth':(.45,.28,.16),
 'Rock':(.39,.48,.46), 'RockLight':(.59,.65,.55), 'Cream':(1,.87,.62),
 'Skin':(.96,.64,.39), 'Orange':(1,.29,.07), 'Gold':(1,.65,.08),
 'White':(.96,.98,.89), 'Ink':(.045,.10,.15), 'Blue':(.19,.42,.62),
 'Pink':(.91,.29,.38), 'Wood':(.64,.39,.16), 'Purple':(.39,.26,.53)}
mats={}
for name,c in palette.items():
 m=bpy.data.materials.new('CS_'+name); m.diffuse_color=(*c,1); m.use_nodes=True
 bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); bs.inputs['Base Color'].default_value=(*c,1); bs.inputs['Roughness'].default_value=.72
 mats[name]=m

def point(p): return (p[0],-p[2],p[1])
def parent_empty(name,loc=(0,0,0),parent=None):
 o=bpy.data.objects.new(name,None); scene.collection.objects.link(o);o.location=point(loc);o.parent=parent;return o
def finish(o,name,mat,loc,scale,parent,bevel=0):
 o.name=name;o.location=point(loc);o.scale=(scale[0],scale[2],scale[1]);o.parent=parent
 o.data.materials.append(mats[mat]);bpy.context.view_layer.objects.active=o;o.select_set(True)
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('SoftEdges','BEVEL');mod.width=bevel;mod.segments=2
  bpy.ops.object.modifier_apply(modifier=mod.name)
 for p in o.data.polygons: p.use_smooth=True
 o.select_set(False);return o
def box(name,loc,scale,mat,parent,bevel=.06):
 bpy.ops.mesh.primitive_cube_add(size=1)
 return finish(bpy.context.object,name,mat,loc,scale,parent,bevel)
def ball(name,loc,scale,mat,parent,sub=2):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1)
 return finish(bpy.context.object,name,mat,loc,scale,parent)
def cylinder(name,loc,radius,depth,mat,parent,vertices=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=1,depth=1)
 return finish(bpy.context.object,name,mat,loc,(radius,depth,radius),parent,.025)
def cone(name,loc,radius,depth,mat,parent):
 bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=radius,radius2=0,depth=depth)
 return finish(bpy.context.object,name,mat,loc,(1,1,1),parent)

roots=[]
def root(name):
 r=parent_empty(name);roots.append(r);return r
r=root('Hero')
box('Tunic',(0,.86,0),(.57,.57,.39),'Mint',r,.10)
box('Belt',(0,.61,-.01),(.58,.095,.42),'Bark',r,.03)
box('Buckle',(0,.62,-.23),(.14,.12,.06),'Gold',r,.02)
ball('Head',(0,1.36,-.035),(.36,.35,.31),'Skin',r,3)
ball('Hair',(0,1.52,.05),(.38,.28,.31),'Bark',r)
ball('Cap',(0,1.66,.015),(.40,.21,.35),'Mint',r)
box('CapBrim',(0,1.57,-.25),(.53,.07,.33),'DarkLeaf',r,.04)
for x in [-.13,.13]:
 ball('Eye',(x,1.39,-.325),(.047,.065,.023),'Ink',r)
 ball('EyeGlint',(x-.012,1.41,-.345),(.013,.017,.008),'White',r)
ball('Nose',(0,1.27,-.35),(.07,.065,.07),'Skin',r)
box('Scarf',(0,1.10,0),(.59,.11,.47),'Orange',r,.04)
box('ScarfTail',(.25,.91,.24),(.16,.44,.07),'Orange',r,.03)
box('Backpack',(0,.88,.29),(.42,.48,.23),'Wood',r,.08)
for side,x in [('L',-.2),('R',.2)]:
 p=parent_empty('Leg'+side,(x,.58,0),r)
 box('Trouser'+side,(0,-.17,0),(.21,.37,.23),'Blue',p,.06)
 box('Boot'+side,(0,-.43,-.075),(.25,.19,.39),'Bark',p,.06)
 p=parent_empty('Arm'+side,(x*1.9,1.02,0),r)
 box('Sleeve'+side,(0,-.15,0),(.21,.32,.25),'Mint',p,.06)
 ball('Hand'+side,(0,-.36,0),(.12,.14,.12),'Skin',p)
leaf=ball('CapLeaf',(.14,1.94,.02),(.10,.23,.045),'Leaf',r);leaf.rotation_euler[1]=-.45

r=root('Beetle')
ball('Shell',(0,.48,0),(.57,.44,.43),'Pink',r)
box('ShellStripe',(0,.56,-.4),(.08,.44,.04),'Cream',r,.02)
ball('Face',(0,.31,-.30),(.40,.24,.23),'Purple',r)
for x in [-.16,.16]:
 ball('EyeWhite',(x,.41,-.49),(.10,.12,.05),'White',r)
 ball('Pupil',(x,.41,-.535),(.045,.06,.025),'Ink',r)
 for z in [-.2,.23]:ball('Foot',(x*2.8,.10,z),(.19,.10,.18),'Ink',r)

r=root('GrassTile')
box('Soil',(0,-.56,0),(1,1.12,2.6),'Earth',r,.10)
box('GrassCap',(0,-.06,0),(1.06,.26,2.72),'Grass',r,.08)
for x in [-.32,.18]:ball('EmbeddedStone',(x,-.63,-1.31),(.13,.16,.035),'Wood',r)
r=root('StoneTile');box('Stone',(0,-.25,0),(1,.50,2.5),'Rock',r,.08);box('Moss',(0,.01,0),(1.03,.09,2.52),'Mint',r,.03)
r=root('WoodPlatform')
for x in [-.75,-.25,.25,.75]:box('Plank',(x,-.1,0),(.47,.20,1.65),'Wood',r,.04)
for z in [-.55,.55]:box('Beam',(0,-.28,z),(2.15,.2,.15),'Bark',r,.03)
r=root('Tree')
cylinder('Trunk',(0,1.1,0),.23,2.2,'Bark',r)
for pos,scale in [((0,2.3,0),(1.15,1.05,.85)),((-.5,2.0,0),(.8,.7,.75)),((.45,2.1,.1),(.8,.8,.8))]:ball('Crown',pos,scale,'Leaf',r)
for pos in [(-.5,2.25,-.6),(.4,2.0,-.65)]:ball('Fruit',pos,(.13,.14,.13),'Orange',r)
r=root('Bush')
for x,s in [(-.35,.43),(0,.6),(.4,.45)]:ball('LeafCluster',(x,s*.65,0),(s,s*.8,s*.8),'Mint',r)
r=root('Cloud')
for x,y,s in [(-.85,0,.65),(0,.2,.95),(.9,0,.7)]:ball('CloudPuff',(x,y,0),(s,s*.57,.45),'White',r)
r=root('Coin')
c=cylinder('CoinDisk',(0,0,0),.29,.10,'Gold',r,24);c.rotation_euler[0]=math.pi/2
box('CoinMark',(0,0,-.065),(.065,.30,.025),'Cream',r,.015)
r=root('SunSeed')
ball('Heart',(0,0,0),(.30,.34,.16),'Gold',r)
for i in range(8):
 a=i*math.pi/4;ball('Ray',(math.sin(a)*.40,math.cos(a)*.40,0),(.075,.10,.06),'Cream',r)
r=root('RewardBlock')
box('Crate',(0,0,0),(.93,.93,.93),'Gold',r,.08)
box('FrontPlate',(0,0,-.48),(.73,.73,.04),'Orange',r,.05)
# FBX handedness conversion mirrors local X; precompensate lettering for the Unity side camera.
for x,y in [(-.12,.20),(.05,.24),(.19,.11),(.07,-.02),(.0,-.25)]:box('Question',(-x,y,-.52),(.14,.14,.03),'Cream',r,.02)
r=root('Spike')
box('Base',(0,.04,0),(1,.08,1.5),'Rock',r,.02)
for x in [-.33,0,.33]:
 for z in [-.35,.35]:cone('Thorn',(x,.32,z),.17,.55,'Cream',r)
r=root('Checkpoint')
cylinder('Pole',(0,1.15,0),.065,2.3,'Bark',r)
ball('Finial',(0,2.35,0),(.13,.13,.13),'Gold',r)
box('Flag',(.43,1.93,0),(.8,.48,.07),'Orange',r,.02)
box('Foot',(0,.10,0),(.6,.2,.6),'RockLight',r,.06)
r=root('Gate')
for x in [-1.1,1.1]:
 box('Pillar',(x,1.55,0),(.48,3.1,.8),'RockLight',r,.10)
 box('PillarFoot',(x,.20,0),(.75,.4,1),'Rock',r,.05)
 ball('LeafTop',(x,3.18,0),(.48,.40,.52),'Mint',r)
box('Lintel',(0,2.9,0),(2.6,.46,.85),'RockLight',r,.1)
ball('SunEmblem',(0,2.97,-.5),(.40,.40,.12),'Gold',r)
r=root('Flower')
cylinder('Stem',(0,.23,0),.025,.46,'DarkLeaf',r,6)
for i in range(5):
 a=i*math.pi*2/5;ball('Petal',(math.cos(a)*.13,.48+math.sin(a)*.13,-.04),(.11,.11,.055),'Cream',r)
ball('Center',(0,.48,-.1),(.075,.075,.04),'Gold',r)
r=root('Mountain')
cone('Peak',(0,3,0),4,6,'Mint',r)

# Export each root and its complete hierarchy; all assets have their origin at ground level.
exported=[]
for r in roots:
 bpy.ops.object.select_all(action='DESELECT')
 r.select_set(True)
 for o in r.children_recursive:o.select_set(True)
 bpy.context.view_layer.objects.active=r
 bpy.ops.export_scene.fbx(filepath=str(OUT/(r.name+'.fbx')),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
 exported.append(r.name)
# A spaced art catalogue in the source file, after export.
for i,r in enumerate(roots):r.location=(i%5*5,i//5*5,0)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CloudSprout.blend'),copy=True)
bpy.context.window.scene=original_scene
(SOURCE/'palette.json').write_text(json.dumps(palette,indent=2),encoding='utf-8')
result={'assets':exported,'source':str(SOURCE/'CloudSprout.blend'),'export_dir':str(OUT)}
