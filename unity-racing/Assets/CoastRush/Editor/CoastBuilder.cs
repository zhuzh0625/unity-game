using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastRush;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class CoastBuilder
{
    const string Root="Assets/CoastRush";
    public const string ScenePath=Root+"/Scenes/CoastCircuit.unity";
    static Dictionary<string,Material> mats;
    static Transform scenery;
    static CoastTrack track;
    static int meshSerial;

    [MenuItem("Coast Rush/Build coast scene")]
    public static string BuildScene()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before rebuilding the authored scene.");
        Directory.CreateDirectory(Root+"/GeneratedMeshes");Directory.CreateDirectory(Root+"/Prefabs");
        AssetDatabase.Refresh();
        if(TMP_Settings.instance==null)TMP_PackageResourceImporter.ImportResources(true,false,false);
        Configure();CreateMaterials();meshSerial=0;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        track=new GameObject("Sunlit Coast Circuit").AddComponent<CoastTrack>();track.Generate();
        scenery=new GameObject("Blender scenery").transform;
        BuildRoad();BuildScenery();BuildLighting();
        var director=new GameObject("Race Director").AddComponent<RaceDirector>();director.track=track;
        director.racers=new VehicleController[4];
        string[] names={"你","海风","日光","浪花"};
        Color[] colors={new Color(.96f,.27f,.08f),new Color(.06f,.57f,.64f),new Color(.97f,.77f,.23f),new Color(.73f,.80f,.85f)};
        for(int i=0;i<4;i++)director.racers[i]=CreateCar(i,names[i],colors[i]);
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";
        var cam=camera.GetComponent<Camera>();cam.nearClipPlane=.12f;cam.farClipPlane=1500;cam.fieldOfView=61;
        var cameraData=cam.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=true;cameraData.volumeLayerMask=1;cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        var chase=camera.AddComponent<ChaseCamera>();chase.target=director.racers[0];director.chaseCamera=chase;
        camera.transform.SetPositionAndRotation(new Vector3(6,6,-4),Quaternion.Euler(12,215,0));
        var canvas=new GameObject("Coast UI",typeof(RectTransform));var hud=canvas.AddComponent<RaceHUD>();hud.Construct(CreateFont(),track);director.hud=hud;
        var events=new GameObject("Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        CombineEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();
        return $"Saved {ScenePath}; length={track.length:F1}m; road samples={track.points.Length}; four cars and 24 ordered checkpoints.";
    }

    static void Configure()
    {
        PlayerSettings.companyName="Coast Rush Studio";PlayerSettings.productName="Coast Rush - 海岸疾驰";PlayerSettings.bundleVersion="1.0.0";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        QualitySettings.vSyncCount=0;QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowDistance=90;
        var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if(!rp)throw new InvalidOperationException("PC URP asset missing.");
        GraphicsSettings.defaultRenderPipeline=rp;QualitySettings.renderPipeline=rp;rp.supportsHDR=true;rp.msaaSampleCount=1;rp.shadowDistance=90;rp.shadowCascadeCount=2;rp.supportsDynamicBatching=true;
        rp.supportsCameraDepthTexture=false;rp.supportsCameraOpaqueTexture=false;
        // A small stylized scene needs neither a full-screen SSAO pass nor clustered light culling.
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        renderer.renderingMode=RenderingMode.Forward;
        foreach(var feature in renderer.rendererFeatures){feature.SetActive(false);EditorUtility.SetDirty(feature);}
        renderer.SetDirty();EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(rp);
        var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue="CoastWorld";tags.FindProperty("layers").GetArrayElementAtIndex(9).stringValue="Racers";tags.ApplyModifiedProperties();
        Time.fixedDeltaTime=.02f;
    }

    static Material Make(string name,Color color,float metal=0,float smooth=.25f,bool emission=false)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
        if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.4f);}
        EditorUtility.SetDirty(m);return m;
    }
    static void CreateMaterials()
    {
        mats=new Dictionary<string,Material>();
        void Add(string n,Color c,float metal=0,float smooth=.25f,bool emit=false){mats[n]=Make(n,c,metal,smooth,emit);}
        Add("CarPaint",new Color(.96f,.27f,.08f),.45f,.75f);Add("Carbon",new Color(.025f,.055f,.07f),.2f,.5f);
        Add("Glass",new Color(.035f,.19f,.24f),.55f,.85f);Add("TireRubber",new Color(.024f,.03f,.035f));
        Add("WheelAlloy",new Color(.74f,.8f,.82f),.8f,.75f);Add("Headlight",new Color(.70f,.95f,1),.2f,.6f,true);Add("BrakeLight",new Color(.95f,.025f,.005f),.2f,.5f,true);
        Add("Ivory",new Color(.88f,.80f,.61f));Add("Chalk",new Color(.92f,.93f,.84f));Add("Terracotta",new Color(.64f,.20f,.12f));Add("ShutterTeal",new Color(.04f,.39f,.42f));
        Add("PalmBark",new Color(.35f,.23f,.12f));Add("PalmLeaf",new Color(.12f,.36f,.11f));Add("PalmLeafLight",new Color(.26f,.49f,.14f));
        Add("WarmStone",new Color(.65f,.54f,.38f));Add("RoadAsphalt",new Color(.15f,.19f,.21f),0,.13f);Add("GuardrailSilver",new Color(.50f,.60f,.62f),.5f,.55f);
        Add("Sand",new Color(.80f,.70f,.48f));Add("Grass",new Color(.43f,.55f,.26f));Add("RoadShoulder",new Color(.44f,.44f,.36f));Add("Stripe",new Color(.88f,.89f,.76f));Add("CurbRed",new Color(.85f,.22f,.13f));Add("Banner",new Color(.025f,.10f,.13f));
    }

    static GameObject Model(string name,Transform parent,Vector3 p,Quaternion rotation,float scale=1)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+name+".fbx");
        if(!asset)throw new FileNotFoundException(name+" model is missing.");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.name=name;go.transform.SetParent(parent,false);
        go.transform.SetPositionAndRotation(p,rotation*Quaternion.Euler(0,180,0));go.transform.localScale=Vector3.one*scale;
        foreach(var renderer in go.GetComponentsInChildren<Renderer>())
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)if(materials[i])
            {var key=mats.Keys.OrderByDescending(k=>k.Length).FirstOrDefault(k=>materials[i].name.StartsWith(k,StringComparison.Ordinal));if(key!=null)materials[i]=mats[key];}
            renderer.sharedMaterials=materials;
        }
        return go;
    }

    static Mesh PersistMesh(Mesh mesh,string name)
    {
        mesh.name=name;string path=Root+"/GeneratedMeshes/"+name+".asset";
        var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(previous){CoastRoadside.ReplaceMesh(previous,mesh);UnityEngine.Object.DestroyImmediate(mesh);return previous;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static GameObject Geometry(string name,Vector3[] vertices,int[] triangles,Material material,Transform parent,bool collider=false)
    {
        var mesh=new Mesh();mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=PersistMesh(mesh,name+"_"+(meshSerial++));go.GetComponent<MeshRenderer>().sharedMaterial=material;
        if(collider){go.layer=8;go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;}
        return go;
    }

    static void Band(string name,float left,float right,Func<float,float> leftHeight,Func<float,float> rightHeight,Material material,bool collision=false)
    {
        var v=new Vector3[track.points.Length*2];var tri=new int[(track.points.Length-1)*6];
        for(int i=0;i<track.points.Length;i++)
        {
            float s=track.distances[i];v[i*2]=track.Position(s,left);v[i*2+1]=track.Position(s,right);
            v[i*2].y=leftHeight(s);v[i*2+1].y=rightHeight(s);
            if(i==track.points.Length-1){v[i*2]=v[0];v[i*2+1]=v[1];}
            if(i<track.points.Length-1){int k=i*6,j=i*2;tri[k]=j;tri[k+1]=j+2;tri[k+2]=j+1;tri[k+3]=j+1;tri[k+4]=j+2;tri[k+5]=j+3;}
        }
        Geometry(name,v,tri,material,track.transform,collision);
    }
    static float RoadY(float s)=>track.Position(s).y;
    static GameObject Box(string name,Vector3 p,Vector3 size,Material material,Transform parent,Quaternion rotation,bool collision=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(p,rotation);go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=material;
        if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());else go.layer=8;
        return go;
    }

    static void BuildRoad()
    {
        Band("RaceSurface",-7,7,RoadY,RoadY,mats["RoadAsphalt"],true);
        Band("LeftShoulder",-12,-7,s=>RoadY(s)-.08f,s=>RoadY(s)-.08f,mats["RoadShoulder"],true);
        Band("RightShoulder",7,12,s=>RoadY(s)-.08f,s=>RoadY(s)-.08f,mats["RoadShoulder"],true);
        Band("LeftMeadow",-30,-12,s=>RoadY(s)-1.5f,s=>RoadY(s)-.09f,mats["Grass"],true);
        Band("RightMeadow",12,30,s=>RoadY(s)-.09f,s=>RoadY(s)-1.5f,mats["Grass"],true);
        Band("LeftBeach",-48,-30,s=>-.4f,s=>RoadY(s)-1.5f,mats["Sand"],true);
        Band("RightBeach",30,48,s=>RoadY(s)-1.5f,s=>-.4f,mats["Sand"],true);
        Band("LeftWhiteLine",-6.78f,-6.64f,s=>RoadY(s)+.015f,s=>RoadY(s)+.015f,mats["Stripe"]);
        Band("RightWhiteLine",6.64f,6.78f,s=>RoadY(s)+.015f,s=>RoadY(s)+.015f,mats["Stripe"]);
        for(float s=0;s<track.length;s+=8)
        {
            var q=track.Rotation(s);Box("Centre dash",track.Position(s)+Vector3.up*.021f,new Vector3(.13f,.015f,3.2f),mats["Stripe"],scenery,q);
        }
        CoastRoadside.Build(track,scenery,mats);
        // Inland low ground is separate from the elevated road ribbon.
        var island=new List<Vector3>{new Vector3(140,.3f,-10)};for(int i=0;i<track.points.Length-1;i++){var p=track.Position(track.distances[i],30);p.y=.25f;island.Add(p);}
        var triangles=new List<int>();for(int i=1;i<island.Count;i++){triangles.Add(0);triangles.Add(i);triangles.Add(i==island.Count-1?1:i+1);}
        Geometry("IslandMeadow",island.ToArray(),triangles.ToArray(),mats["Grass"],scenery,true);
        var water=Make("Sea",new Color(.03f,.45f,.56f));water.shader=Shader.Find("CoastRush/Sea");
        water.SetColor("_DeepColor",new Color(.018f,.32f,.43f));water.SetColor("_ShallowColor",new Color(.08f,.62f,.63f));
        Geometry("OpenSea",new[]{new Vector3(-2200,-.75f,-2200),new Vector3(-2200,-.75f,2200),new Vector3(2200,-.75f,2200),new Vector3(2200,-.75f,-2200)},new[]{0,1,2,0,2,3},water,scenery);
        // Start line and the sculptural gantry are visible in both directions.
        for(int i=0;i<14;i++)for(int j=0;j<2;j++)Box("Start checker",track.Position(j*.65f,(i-6.5f)) +Vector3.up*.028f,new Vector3(1,.02f,.65f),mats[(i+j)%2==0?"Chalk":"Carbon"],scenery,track.Rotation(0));
        var start=track.Position(0);
        foreach(int side in new[]{-1,1})Box("Gantry tower",start+new Vector3(side*8,3,0),new Vector3(.65f,6,1.0f),mats["CarPaint"],scenery,Quaternion.identity,true);
        Box("Gantry header",start+Vector3.up*6.1f,new Vector3(17,1.45f,.65f),mats["Banner"],scenery,Quaternion.identity);
        WorldText("COAST RUSH",start+new Vector3(0,6.12f,-.36f),Quaternion.identity,17,1.2f,5,Color.white);
    }

    static TMP_FontAsset cachedFont;
    static TMP_FontAsset CreateFont()
    {
        if(cachedFont)return cachedFont;
        string path=Root+"/Fonts/CoastSans.asset";cachedFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(cachedFont)return cachedFont;
        var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/NotoSansCJKsc-Regular.otf");
        cachedFont=TMP_FontAsset.CreateFontAsset(source,48,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        cachedFont.name="CoastSans";AssetDatabase.CreateAsset(cachedFont,path);
        foreach(var texture in cachedFont.atlasTextures){texture.name="CoastSans Atlas";AssetDatabase.AddObjectToAsset(texture,cachedFont);}
        cachedFont.material.name="CoastSans Material";AssetDatabase.AddObjectToAsset(cachedFont.material,cachedFont);
        cachedFont.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 :./—·!海岸疾驰日光风全速前行穿过滨小镇追逐下一个弯道开始竞单人计时切换车身配色圈辆赛车方向键驾驶空格手刹复位暂停静音退出名次最佳休息会等你比赛也继续重新返回主菜单冲线冠军属于第顺利完总用刷新你的成绩已安全路段出发挑战完成错误请掉头浪花",out string missing);
        EditorUtility.SetDirty(cachedFont);return cachedFont;
    }
    static void WorldText(string text,Vector3 p,Quaternion q,float width,float height,float size,Color color)
    {
        var go=new GameObject("Track sign");go.transform.SetParent(scenery);go.transform.SetPositionAndRotation(p,q);
        var label=go.AddComponent<TextMeshPro>();label.font=CreateFont();label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width,height);
    }

    static void BuildScenery()
    {
        Physics.SyncTransforms();
        Vector3 Ground(Vector3 p)
        {
            if(Physics.Raycast(p+Vector3.up*100,Vector3.down,out var hit,200,1<<8))p.y=hit.point.y;
            else p.y=-.8f;
            return p;
        }
        var random=new System.Random(20261030);
        for(float s=14;s<track.length;s+=24)
        {
            if(s>track.length*.34f&&s<track.length*.44f)continue;
            int side=((int)(s/24)%3==0)?-1:1;float offset=side*(15+(float)random.NextDouble()*7);
            var p=track.Position(s,offset);p.y-=.1f+(Mathf.Abs(offset)-12)*.078f;
            Model("CoastPalm",scenery,p,Quaternion.Euler(0,(float)random.NextDouble()*360,0),.85f+(float)random.NextDouble()*.35f);
        }
        for(int i=0;i<14;i++)
        {
            float s=32+i*19;float lane=19+(i%2)*9;var p=track.Position(s,lane);p.y-=.1f+(lane-12)*.078f;
            Model("CoastHouse",scenery,p,track.Rotation(s)*Quaternion.Euler(0,-90,0),.78f+(i%3)*.13f);
        }
        for(int i=0;i<70;i++)
        {
            float s=(float)random.NextDouble()*track.length;float side=i%2==0?1:-1;float lateral=side*(26+(float)random.NextDouble()*18);
            if(s<130 || s>track.length-130)continue;
            var p=Ground(track.Position(s,lateral));p.y-=.2f;
            var rock=Model("CoastRock",scenery,p,Quaternion.Euler(0,(float)random.NextDouble()*360,0),.65f+(float)random.NextDouble()*.8f);
            rock.transform.localScale=Vector3.Scale(rock.transform.localScale,new Vector3(1,.8f+(float)random.NextDouble()*.4f,1));
        }
        foreach(var p in new[]{new Vector3(112,2,65),new Vector3(156,2,110),new Vector3(118,1,-112)})
        {var hill=Model("CoastRock",scenery,p,Quaternion.Euler(0,34,0),12);hill.transform.localScale=new Vector3(13,9,13);}
        var lighthouseP=Ground(track.Position(track.length*.20f,-35));Model("CoastLighthouse",scenery,lighthouseP,Quaternion.identity,1.8f);
        // Modular Blender vaults form a continuous covered bend.
        int n=0;
        for(float s=track.length*.355f;s<track.length*.425f;s+=6.8f)
        {
            var tunnel=Model("TunnelArch",scenery,track.Position(s)-Vector3.up*.12f,track.Rotation(s));
            foreach(var f in tunnel.GetComponentsInChildren<MeshFilter>())if(f.name=="StoneVault"){f.gameObject.layer=8;f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;}
            if(n++%3==0)
            {var l=new GameObject("Tunnel amber light",typeof(Light));l.transform.SetParent(scenery);l.transform.position=track.Position(s)+Vector3.up*5.7f;var light=l.GetComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.72f,.39f);light.range=18;light.intensity=5;}
        }
        foreach(float fraction in new[]{.10f,.28f,.47f,.62f,.78f,.91f})
        {
            float s=track.length*fraction;var p=track.Position(s,9.8f);
            Box("Sign pole",p+Vector3.up*1.7f,new Vector3(.10f,3.4f,.10f),mats["GuardrailSilver"],scenery,Quaternion.identity);
            Box("Corner board",p+Vector3.up*3.0f,new Vector3(3.1f,1.4f,.12f),mats["Banner"],scenery,track.Rotation(s));
            WorldText("SLOW",p+Vector3.up*3.0f-track.Forward(s)*.08f,track.Rotation(s),3,1.1f,4, new Color(1,.65f,.28f));
        }
    }

    static void BuildLighting()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.66f,.73f);RenderSettings.ambientEquatorColor=new Color(.44f,.56f,.55f);RenderSettings.ambientGroundColor=new Color(.27f,.28f,.22f);RenderSettings.ambientIntensity=1;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.64f,.79f,.80f);RenderSettings.fogStartDistance=230;RenderSettings.fogEndDistance=1150;
        var sky=Make("CoastalSky",Color.white);sky.shader=Shader.Find("Skybox/Procedural");sky.SetColor("_SkyTint",new Color(.47f,.63f,.77f));sky.SetColor("_GroundColor",new Color(.60f,.71f,.70f));sky.SetFloat("_Exposure",1.1f);sky.SetFloat("_AtmosphereThickness",.8f);sky.SetFloat("_SunSize",.035f);RenderSettings.skybox=sky;
        var sun=new GameObject("Warm afternoon sun",typeof(Light));sun.transform.rotation=Quaternion.Euler(42,-38,0);
        var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.91f,.77f);light.intensity=2.2f;light.shadows=LightShadows.Soft;light.shadowBias=.03f;RenderSettings.sun=light;
        var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.40f,.47f,.51f));RenderSettings.ambientProbe=ambient;
        var fill=new GameObject("Soft sea bounce",typeof(Light));fill.transform.rotation=Quaternion.Euler(28,145,0);
        fill.GetComponent<Light>().type=LightType.Directional;fill.GetComponent<Light>().color=new Color(.65f,.83f,1);fill.GetComponent<Light>().intensity=.65f;fill.GetComponent<Light>().shadows=LightShadows.None;
        var volume=new GameObject("Coast color grade",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;
        string path=Root+"/Materials/CoastGrade.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
        T Effect<T>() where T:VolumeComponent {if(profile.TryGet<T>(out var effect))return effect;var c=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(c,profile);return c;}
        Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);Effect<Bloom>().threshold.Override(1.1f);Effect<Bloom>().intensity.Override(.20f);
        Effect<ColorAdjustments>().saturation.Override(8);Effect<ColorAdjustments>().contrast.Override(7);Effect<Vignette>().intensity.Override(.15f);Effect<Vignette>().smoothness.Override(.6f);
        volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
    }

    static VehicleController CreateCar(int index,string driverName,Color color)
    {
        var root=new GameObject(index==0?"Player Coast GT":"AI Coast GT "+index);root.layer=9;
        var rb=root.AddComponent<Rigidbody>();rb.mass=1200;rb.linearDamping=.06f;rb.angularDamping=1.2f;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        var hit=root.AddComponent<BoxCollider>();hit.center=new Vector3(0,.68f,0);hit.size=new Vector3(1.8f,.85f,4.0f);
        var model=Model("CoastGT",root.transform,Vector3.zero,Quaternion.identity);
        var controller=root.AddComponent<VehicleController>();controller.isPlayer=index==0;controller.driverName=driverName;controller.track=track;controller.aiLane=(index%2==0?-1:1)*(1.3f+index*.12f);controller.aiPace=.88f+index*.035f;
        controller.wheels=new WheelCollider[4];controller.wheelVisuals=new Transform[4];
        string[] names={"WheelFL","WheelFR","WheelRL","WheelRR"};
        for(int i=0;i<4;i++)
        {
            var visual=model.GetComponentsInChildren<Transform>().First(t=>t.name==names[i]);controller.wheelVisuals[i]=visual;
            var wgo=new GameObject(names[i]+" Physics");wgo.transform.SetParent(root.transform,false);var pos=root.transform.InverseTransformPoint(visual.position);pos.y=.52f;wgo.transform.localPosition=pos;wgo.layer=9;
            var w=wgo.AddComponent<WheelCollider>();w.radius=.365f;w.mass=25;w.suspensionDistance=.30f;w.wheelDampingRate=1;w.forceAppPointDistance=.12f;
            var spring=w.suspensionSpring;spring.spring=36000;spring.damper=5200;spring.targetPosition=.5f;w.suspensionSpring=spring;
            var forward=w.forwardFriction;forward.extremumSlip=.4f;forward.extremumValue=1;forward.asymptoteSlip=.8f;forward.asymptoteValue=.65f;forward.stiffness=1.65f;w.forwardFriction=forward;
            var side=w.sidewaysFriction;side.extremumSlip=.2f;side.extremumValue=1;side.asymptoteSlip=.5f;side.asymptoteValue=.75f;side.stiffness=2;w.sidewaysFriction=side;
            w.ConfigureVehicleSubsteps(8,8,4);controller.wheels[i]=w;
        }
        controller.paintRenderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.sharedMaterial==mats["CarPaint"]).ToArray();
        controller.brakeRenderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.sharedMaterial==mats["BrakeLight"]).ToArray();
        controller.tireSmoke=new ParticleSystem[2];
        for(int i=0;i<2;i++)
        {
            var go=new GameObject("Rear tire smoke");go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(i==0?-.94f:.94f,.12f,-1.3f);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=2;main.loop=true;main.startLifetime=.7f;main.startSpeed=.45f;main.startSize=.38f;main.startColor=new Color(.85f,.83f,.73f,.22f);main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=70;main.playOnAwake=true;
            var emission=ps.emission;emission.rateOverTime=0;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.1f;
            var pr=ps.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=ParticleMaterial();controller.tireSmoke[i]=ps;
        }
        root.AddComponent<VehicleAudio>();
        root.transform.SetPositionAndRotation(track.Position(-12-(index/2)*7,index%2==0?-2:2)+Vector3.up*.22f,track.Rotation(-12-(index/2)*7));
        controller.Paint(color);
        return controller;
    }
    static Material ParticleMaterial()
    {
        string path=Root+"/Materials/TireSmoke.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(m,path);}
        string texturePath=Root+"/Materials/Smoke.png";
        if(!File.Exists(texturePath))
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float radius=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;pixels[y*64+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),2));}
            texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(texturePath);
        }
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));m.SetColor("_BaseColor",new Color(.85f,.83f,.74f,.5f));m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;
        EditorUtility.SetDirty(m);return m;
    }

    static void CombineEnvironment()
    {
        var renderers=scenery.GetComponentsInChildren<MeshRenderer>().Where(r=>!r.GetComponent<TMP_Text>()&&r.GetComponent<MeshFilter>()&&r.sharedMaterial&&r.sharedMaterial.shader.name!="CoastRush/Sea").ToArray();
        // Keep batches local so frustum/shadow culling can discard the far side of the circuit.
        foreach(var group in renderers.GroupBy(r=>new {material=r.sharedMaterial,x=Mathf.FloorToInt(r.bounds.center.x/64),z=Mathf.FloorToInt(r.bounds.center.z/64)}))
        {
            var combines=new List<CombineInstance>();
            foreach(var r in group)
            {var f=r.GetComponent<MeshFilter>();if(f.sharedMesh.subMeshCount!=1)continue;combines.Add(new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix});r.enabled=false;}
            if(combines.Count==0)continue;
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines.ToArray(),true,true);
            string name=$"Scenery_{group.Key.material.name}_{group.Key.x}_{group.Key.z}";
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(scenery,false);go.GetComponent<MeshFilter>().sharedMesh=PersistMesh(mesh,name);go.GetComponent<MeshRenderer>().sharedMaterial=group.Key.material;
        }
    }

    [MenuItem("Coast Rush/Build Windows player")]
    public static string BuildWindows()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before building.");
        Directory.CreateDirectory("Builds/Windows");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Windows/CoastRush.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
        return $"Builds/Windows/CoastRush.exe; {report.summary.totalSize/1048576} MiB; {report.summary.totalTime.TotalSeconds:F1}s";
    }
}
