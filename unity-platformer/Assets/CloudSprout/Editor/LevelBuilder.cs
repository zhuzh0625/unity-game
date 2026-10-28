using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CloudSprout.Editor
{
    public static class LevelBuilder
    {
        const string Root="Assets/CloudSprout/";
        static Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static Transform terrain,decoration,gameplay;
        static int coinCount;
        const float CoinSpacing=2.1f;
        public static void Create()
        {
            if(Application.isPlaying)throw new Exception("Exit Play mode first.");
            if(File.Exists(Root+"Scenes/MeadowTrail.unity"))throw new Exception("Level exists. Edit it in place.");
            Directory.CreateDirectory(Root+"Scenes");Directory.CreateDirectory(Root+"Materials");Directory.CreateDirectory(Root+"Fonts");
            AssetDatabase.Refresh();SetupMaterials();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            terrain=new GameObject("01 Terrain").transform;decoration=new GameObject("02 Landscape").transform;gameplay=new GameObject("03 Gameplay").transform;
            var session=new GameObject("Game Session").AddComponent<GameSession>();
            var playerRoot=new GameObject("Sprout");playerRoot.transform.position=new Vector3(2,.2f,0);
            var player=playerRoot.AddComponent<SproutPlayer>();var controller=playerRoot.GetComponent<CharacterController>();controller.height=1.65f;controller.radius=.29f;controller.center=new Vector3(0,.84f,0);controller.skinWidth=.025f;controller.stepOffset=.22f;controller.slopeLimit=50;controller.minMoveDistance=0;
            player.visual=Model("Hero",Vector3.zero,Vector3.one,playerRoot.transform).transform;
            session.player=player;
            var cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.orthographic=true;cam.orthographicSize=5.6f;cam.transform.position=new Vector3(10,3.6f,-25);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.57f,.80f,.80f);cam.farClipPlane=150;cam.allowHDR=true;cam.allowMSAA=true;
            cam.gameObject.AddComponent<AudioListener>();var follow=cam.gameObject.AddComponent<SideCamera>();follow.target=player.transform;
            var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.volumeLayerMask=1;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.0f;light.color=new Color(1,.91f,.75f);light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(42,-30,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.68f,.83f,.88f);RenderSettings.ambientEquatorColor=new Color(.57f,.68f,.63f);RenderSettings.ambientGroundColor=new Color(.36f,.43f,.35f);RenderSettings.fog=false;
            var vol=new GameObject("Atmosphere").AddComponent<Volume>();vol.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.Add<Tonemapping>(true).mode.value=TonemappingMode.ACES;var bloom=profile.Add<Bloom>(true);bloom.intensity.value=.22f;bloom.threshold.value=1;var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.13f;vignette.smoothness.value=.65f;var color=profile.Add<ColorAdjustments>(true);color.saturation.value=8;color.postExposure.value=.25f;AssetDatabase.CreateAsset(profile,Root+"Materials/Atmosphere.asset");vol.sharedProfile=profile;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length-1,true);
            var urp=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(urp==null)throw new Exception("URP required");urp.supportsHDR=true;urp.msaaSampleCount=4;urp.shadowDistance=45;EditorUtility.SetDirty(urp);

            // Three connected zones; all mandatory gaps fit the running jump envelope.
            Ground(-6,22,0);Ground(25,47,0);Ground(51,69,.6f);Ground(73,96,0);
            Ground(110,126,.4f);Ground(148,164,.4f);Ground(176,194,.5f);
            Ground(194,213,.5f);Ground(228,249,1.2f);Ground(263,297,1);
            Platform(99,.4f,3,new Vector3(5,0,0),5);
            Platform(130,1.2f,3);Platform(136,2,3);Platform(142,1.2f,3);
            Platform(167,.8f,3,new Vector3(4,1,0),4.5f);
            Platform(218,1.6f,3);Platform(223,2.6f,3);
            Platform(252,2.1f,3);Platform(257,3,3);
            // Optional upper paths and three sun seeds.
            Platform(15,1.5f,3);Platform(38,1.6f,3);Platform(42,3.1f,3);
            Platform(57,2.1f,3);Platform(62,3.6f,3);
            Platform(83,1.5f,3);Platform(117,2,3);Platform(139,3.5f,2.5f);
            Platform(155,2,3);Platform(182,2,3);Platform(199,2,3);
            Platform(235,2.7f,3);Platform(240,4.1f,3);Platform(271,2.5f,3);
            Coin(42,4.1f,true);Coin(139,4.5f,true);Coin(240,5.1f,true);
            foreach(var range in new[]{new Vector3(6,20,.85f),new Vector3(27,33,.85f),new Vector3(39,46,.85f),new Vector3(53,67,1.45f),new Vector3(76,92,.85f),new Vector3(112,124,1.25f),new Vector3(150,162,1.25f),new Vector3(178,191,1.35f),new Vector3(197,202,1.35f),new Vector3(208,212,1.35f),new Vector3(230,246,2.05f),new Vector3(265,280,1.85f)})
                for(float x=range.x;x<=range.y;x+=CoinSpacing)Coin(x,range.z);
            foreach(var p in new[]{new Vector2(23.5f,1.8f),new Vector2(49,2.1f),new Vector2(71,2),new Vector2(99,1.6f),new Vector2(104,1.7f),new Vector2(130,2.1f),new Vector2(136,2.9f),new Vector2(142,2.1f),new Vector2(168,2.1f),new Vector2(172,2.2f),new Vector2(218,2.5f),new Vector2(223,3.5f),new Vector2(252,3),new Vector2(257,3.9f),new Vector2(15,2.4f),new Vector2(38,2.5f),new Vector2(57,3),new Vector2(62,4.5f),new Vector2(83,2.4f),new Vector2(117,2.9f),new Vector2(155,2.9f),new Vector2(182,2.9f),new Vector2(199,2.9f),new Vector2(235,3.6f),new Vector2(271,3.4f)})Coin(p.x,p.y);
            OptimizeCoinPlacement(false);
            Enemy(30,27,33,0);Enemy(59,53,65,.6f);Enemy(80,75,86,0);Enemy(118,112,124,.4f);Enemy(154,150,161,.4f);Enemy(185,178,190,.5f);Enemy(207,206,211,.5f);Enemy(236,230,244,1.2f);Enemy(274,268,278,1);
            Spikes(35,0);Spikes(87,0);Spikes(158,.4f);Spikes(204,.5f);Spikes(244,1.2f);Spikes(267,1);
            Reward(10,2.5f);Reward(54,3.1f);Reward(120,2.9f);Reward(188,3);Reward(232,3.7f);
            CheckpointAt(92,0);CheckpointAt(190,.5f);
            var gate=Model("Gate",new Vector3(284,1,0),Vector3.one,gameplay);var trigger=gate.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,1.4f,0);trigger.size=new Vector3(1.4f,2.8f,1.4f);gate.AddComponent<FinishGate>();
            // Depth comes from modelled background layers. Collision stays on z=0.
            UnityEngine.Random.InitState(42810);
            for(int i=-2;i<41;i++)
            {
                float x=i*8;
                var mountain=Model("Mountain",new Vector3(x,-2,25+UnityEngine.Random.Range(0,5)),new Vector3(UnityEngine.Random.Range(1.3f,2.5f),UnityEngine.Random.Range(1.5f,3),1),decoration);
                SetColor(mountain,new Color(.34f,.64f,.62f));
                var distant=Model("Mountain",new Vector3(x+3,-2,43),new Vector3(2.5f,UnityEngine.Random.Range(2.5f,4f),1),decoration);SetColor(distant,new Color(.48f,.73f,.73f));
                Model("Cloud",new Vector3(x+2,UnityEngine.Random.Range(7f,10f),17),Vector3.one*UnityEngine.Random.Range(.65f,1.3f),decoration);
            }
            foreach(var region in new[]{new Vector3(-4,22,0),new Vector3(25,47,0),new Vector3(51,69,.6f),new Vector3(73,96,0),new Vector3(110,126,.4f),new Vector3(148,164,.4f),new Vector3(176,213,.5f),new Vector3(228,249,1.2f),new Vector3(263,297,1)})
            {
                for(float x=region.x+1;x<region.y-1;x+=4.2f)
                {
                    if(UnityEngine.Random.value>.25f)Model("Tree",new Vector3(x,region.z,3.0f+UnityEngine.Random.value),Vector3.one*UnityEngine.Random.Range(.8f,1.35f),decoration);
                    Model("Bush",new Vector3(x+1,region.z+.05f,1.5f),Vector3.one*.8f,decoration);
                    Model("Flower",new Vector3(x+.4f,region.z+.06f,-1.15f),Vector3.one*.7f,decoration);
                }
            }
            Complete();
        }
        public static void Complete()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var session=UnityEngine.Object.FindFirstObjectByType<GameSession>();
            decoration=GameObject.Find("02 Landscape").transform;
            coinCount=UnityEngine.Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None).Count(c=>!c.seed);
            foreach(var t in decoration.GetComponentsInChildren<TextMeshPro>())if(string.IsNullOrEmpty(t.text))UnityEngine.Object.DestroyImmediate(t.gameObject);
            WorldSign("A / D   MOVE",3,1.5f);WorldSign("SPACE   JUMP",18,2.5f);WorldSign("HOLD SPACE\nJUMP HIGHER",38,4.8f);WorldSign("SHIFT + JUMP\nCROSS THE GORGE",94,3.7f);WorldSign("RIDE THE WIND",166,4.5f);WorldSign("THE SUN GATE",280,5.3f);
            var hud=new GameObject("HUD").AddComponent<GameHud>();hud.font=MakeFont();hud.sparkMaterial=MakeUnlit();session.hud=hud;
            session.jump=Clip("jump");session.coin=Clip("coin");session.hurt=Clip("hurt");session.checkpoint=Clip("checkpoint");session.win=Clip("win");session.land=Clip("land");session.music=Clip("music");session.TotalCoins=coinCount+25;
            PlayerSettings.companyName="CloudSprout Studio";PlayerSettings.productName="CloudSprout - Meadow Trail";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            SpikePitBuilder.PolishBanks(false);
            EditorSceneManager.SaveScene(scene,Root+"Scenes/MeadowTrail.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"Scenes/MeadowTrail.unity",true)};AssetDatabase.SaveAssets();
            Debug.Log($"CloudSprout: first level ready, {coinCount} coins + 25 block coins, 3 seeds, 2 checkpoints.");
        }
        static AudioClip Clip(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/"+name+".wav");
        static void SetupMaterials()
        {
            string[] keys={"Mint","Leaf","Grass","DarkLeaf","Bark","Earth","Rock","RockLight","Cream","Skin","Orange","Gold","White","Ink","Blue","Pink","Wood","Purple"};
            Color[] values={new Color(.18f,.60f,.39f),new Color(.33f,.73f,.28f),new Color(.43f,.77f,.25f),new Color(.08f,.29f,.25f),new Color(.30f,.16f,.09f),new Color(.45f,.28f,.16f),new Color(.39f,.48f,.46f),new Color(.59f,.65f,.55f),new Color(1,.87f,.62f),new Color(.96f,.64f,.39f),new Color(1,.29f,.07f),new Color(1,.65f,.08f),new Color(.96f,.98f,.89f),new Color(.045f,.10f,.15f),new Color(.19f,.42f,.62f),new Color(.91f,.29f,.38f),new Color(.64f,.39f,.16f),new Color(.39f,.26f,.53f)};
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new Exception("URP Lit unavailable");
            for(int i=0;i<keys.Length;i++){var m=new Material(shader){name="CS_"+keys[i]};m.color=values[i];m.SetFloat("_Smoothness",.18f);AssetDatabase.CreateAsset(m,Root+"Materials/"+keys[i]+".mat");materials[keys[i]]=m;}
        }
        static GameObject Model(string name,Vector3 position,Vector3 scale,Transform parent)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/"+name+".fbx");if(!source)throw new Exception("Missing model: "+name);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(source);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            foreach(var r in obj.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>{var key=m.name.Replace("CS_","").Split('.')[0];return materials.TryGetValue(key,out var replacement)?replacement:materials["Cream"];}).ToArray();return obj;
        }
        static void Ground(float start,float end,float y)
        {
            var root=new GameObject($"Ground {start}-{end}");root.transform.SetParent(terrain);root.transform.position=new Vector3((start+end)/2,y,0);
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,-1,0);collider.size=new Vector3(end-start,2,2.6f);
            for(float x=start;x<end;x+=2){float width=Mathf.Min(2,end-x);var tile=Model("GrassTile",new Vector3(x+width/2,y,0),new Vector3(width,1.8f,1),terrain);}
        }
        static void Platform(float x,float y,float width,Vector3 travel=default,float period=4)
        {
            var go=new GameObject(travel==Vector3.zero?"Ledge":"Moving Platform");go.transform.SetParent(terrain);go.transform.position=new Vector3(x,y,0);
            var collider=go.AddComponent<BoxCollider>();collider.center=new Vector3(0,-.16f,0);collider.size=new Vector3(width,.32f,1.6f);
            Model("WoodPlatform",Vector3.zero,new Vector3(width/2,1,1),go.transform);
            if(travel!=Vector3.zero){var mover=go.AddComponent<MovingPlatform>();mover.travel=travel;mover.period=period;}
        }
        static void Coin(float x,float y,bool seed=false)
        {
            if(!seed)y=ReachableCoinHeight(x,y);
            var obj=Model(seed?"SunSeed":"Coin",new Vector3(x,y,0),Vector3.one,gameplay);var collider=obj.AddComponent<SphereCollider>();collider.radius=seed?.48f:.37f;collider.isTrigger=true;obj.AddComponent<Collectible>().seed=seed;if(!seed)coinCount++;
        }
        static float ReachableCoinHeight(float x,float y)
        {
            foreach(var ledge in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.name=="Ledge"))
                if(Mathf.Abs(x-ledge.transform.position.x)<ledge.size.x*.5f+.35f && y<ledge.transform.position.y && y>ledge.transform.position.y-1.4f)y=ledge.transform.position.y+.85f;
            return y;
        }
        public static int RepairCoinPlacement()
        {
            int count=0;
            foreach(var coin in UnityEngine.Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None).Where(c=>!c.seed))
            {var p=coin.transform.position;float y=ReachableCoinHeight(p.x,p.y);if(Mathf.Abs(y-p.y)>.01f){coin.transform.position=new Vector3(p.x,y,p.z);count++;}}
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return count;
        }
        public static string OptimizeCoinPlacement(bool saveScene=true)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode before changing the coin layout.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var coins=UnityEngine.Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None).Where(c=>!c.seed&&c.gameObject.scene==scene).OrderBy(c=>c.transform.position.x).ToArray();
            var planned=coins.ToDictionary(c=>c,c=>new Vector3(c.transform.position.x,ReachableCoinHeight(c.transform.position.x,c.transform.position.y),c.transform.position.z));
            var removed=new HashSet<Collectible>();
            var ledges=UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c=>c.name=="Ledge"&&c.gameObject.scene==scene).OrderBy(c=>c.transform.position.x);
            foreach(var ledge in ledges)
            {
                var center=ledge.transform.position;
                var row=coins.Where(c=>!removed.Contains(c)&&Mathf.Abs(planned[c].x-center.x)<=ledge.size.x*.5f+.35f&&planned[c].y-center.y>=.65f&&planned[c].y-center.y<=1.15f).OrderBy(c=>planned[c].x).ToArray();
                int capacity=Mathf.Max(1,Mathf.FloorToInt((ledge.size.x-.8f)/CoinSpacing)+1);
                int count=Mathf.Min(row.Length,capacity);
                for(int i=0;i<row.Length;i++)
                    if(i<count)planned[row[i]]=new Vector3(center.x+(i-(count-1)*.5f)*CoinSpacing,center.y+.9f,0);
                    else removed.Add(row[i]);
            }
            // Check all route junctions before applying edits, not only neighbours within one row.
            var kept=coins.Where(c=>!removed.Contains(c)).ToArray();
            float minDistance=float.MaxValue;
            for(int i=0;i<kept.Length;i++)for(int j=i+1;j<kept.Length;j++)
            {
                float distance=Vector2.Distance(planned[kept[i]],planned[kept[j]]);
                minDistance=Mathf.Min(minDistance,distance);
                if(distance<1.5f)throw new InvalidOperationException("Coin routes are too close: "+planned[kept[i]]+" / "+planned[kept[j]]);
            }
            int moved=0;
            foreach(var coin in kept)if(Vector3.Distance(coin.transform.position,planned[coin])>.001f)
            {Undo.RecordObject(coin.transform,"Space coins evenly");coin.transform.position=planned[coin];PrefabUtility.RecordPrefabInstancePropertyModifications(coin.transform);EditorUtility.SetDirty(coin.transform);moved++;}
            foreach(var coin in removed)Undo.DestroyObjectImmediate(coin.gameObject);
            coinCount=kept.Length;
            var session=UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None).FirstOrDefault(s=>s.gameObject.scene==scene);
            if(session){Undo.RecordObject(session,"Update coin total");session.TotalCoins=coinCount+UnityEngine.Object.FindObjectsByType<RewardBlock>(FindObjectsSortMode.None).Count(b=>b.gameObject.scene==scene)*5;EditorUtility.SetDirty(session);}
            if(saveScene){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
            return $"Coins={coinCount}; moved={moved}; merged={removed.Count}; minimumDistance={minDistance:F3}";
        }
        static void Enemy(float x,float left,float right,float y)
        {
            var root=new GameObject("Patrol Beetle");root.transform.SetParent(gameplay);root.transform.position=new Vector3(x,y+.08f,0);var e=root.AddComponent<PatrolEnemy>();e.left=left;e.right=right;e.visual=Model("Beetle",Vector3.zero,Vector3.one,root.transform).transform;var c=root.AddComponent<BoxCollider>();c.isTrigger=true;c.center=new Vector3(0,.42f,0);c.size=new Vector3(1,.78f,.8f);
        }
        static void Spikes(float tileStart,float y){SpikePitBuilder.Create(tileStart,y,terrain,gameplay);}
        static void Reward(float x,float y){var root=new GameObject("Reward Block");root.transform.SetParent(gameplay);root.transform.position=new Vector3(x,y,0);root.AddComponent<BoxCollider>().size=Vector3.one*.93f;var b=root.AddComponent<RewardBlock>();b.visual=Model("RewardBlock",Vector3.zero,Vector3.one,root.transform).transform;b.rewardCoin=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/Coin.fbx");}
        static void CheckpointAt(float x,float y){var go=Model("Checkpoint",new Vector3(x,y,0),Vector3.one,gameplay);go.AddComponent<Checkpoint>();var c=go.AddComponent<BoxCollider>();c.isTrigger=true;c.center=new Vector3(0,1,0);c.size=new Vector3(1.2f,2,1.3f);}
        static void SetColor(GameObject go,Color c){foreach(var r in go.GetComponentsInChildren<Renderer>()){var m=new Material(materials["Mint"]);m.color=c;r.sharedMaterial=m;}}
        static TMP_FontAsset MakeFont()
        {
            string path=Root+"Fonts/TrailFont.asset";var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(existing)return existing;
            var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"Fonts/Roboto-Medium.ttf");if(!source)throw new Exception("Missing Roboto source font");
            var font=TMP_FontAsset.CreateFontAsset(source);if(!font)throw new Exception("Font creation failed");font.name="TrailFont";AssetDatabase.CreateAsset(font,path);foreach(var tex in font.atlasTextures)AssetDatabase.AddObjectToAsset(tex,font);AssetDatabase.AddObjectToAsset(font.material,font);return font;
        }
        static Material MakeUnlit(){var m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));m.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(m,Root+"Materials/Spark.mat");return m;}
        static void WorldSign(string text,float x,float y)
        {
            var go=new GameObject("Trail Sign");go.transform.SetParent(decoration);go.transform.position=new Vector3(x,y,.4f);var t=go.AddComponent<TextMeshPro>();t.font=MakeFont();t.text=text;t.fontSize=2.7f;t.alignment=TextAlignmentOptions.Center;t.color=new Color(.08f,.27f,.27f);t.rectTransform.sizeDelta=new Vector2(6,2);
        }
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"Scenes/MeadowTrail.unity"},locationPathName="Builds/Windows/CloudSprout.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText("Builds/build-result.json",JsonUtility.ToJson(new BuildResult{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize},true));
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Windows build failed");
        }
        [Serializable]class BuildResult{public string result;public int errors,warnings;public ulong bytes;}
    }
}
