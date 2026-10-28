using System;
using System.IO;
using System.Linq;
using CollectorDemo;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CreateCollectorDemo
{
    const string Root = "Assets/CollectorDemo";
    static TMP_FontAsset font;
    static Color Ink = new Color(0.025f, 0.06f, 0.10f, 0.96f);
    static Color Mint = new Color(0.22f, 1f, 0.78f);

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before building.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new Exception("Save the current scene before creating the demo.");
        if (File.Exists(Root + "/CollectorDemo.unity")) throw new Exception("Demo scene already exists; edit it instead of rebuilding.");
        Directory.CreateDirectory(Root + "/Materials");
        AssetDatabase.Refresh();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) throw new Exception("TMP essentials font is missing.");
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) throw new Exception("An active URP asset is required.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var game = new GameObject("CollectorGame").AddComponent<CollectorGame>();
        var stage = new GameObject("Arena").transform;
        var floor = Mat("Midnight", new Color(.045f,.09f,.13f), .15f, .45f);
        var edge = Mat("Edge", new Color(.025f,.05f,.08f), .4f, .65f);
        var grid = Mat("Grid", new Color(.07f,.19f,.23f), .1f, .5f);
        var mint = Mat("MintGlow", Mint, .4f, .75f, 1.1f);
        var gold = Mat("GoldGlow", new Color(1f,.60f,.1f), .5f, .8f, .65f);
        var pearl = Mat("Player", new Color(.55f,.90f,1f), .65f, .8f, .2f);
        Shape("FloatingBase", PrimitiveType.Cube, new Vector3(0,-.65f,0), new Vector3(17.4f,1.15f,13.4f), edge, stage);
        Shape("PlaySurface", PrimitiveType.Cube, new Vector3(0,-.08f,0), new Vector3(16.6f,.16f,12.6f), floor, stage);
        for (int i = -7; i <= 7; i++) Shape("GridX" + i, PrimitiveType.Cube, new Vector3(i,.012f,0), new Vector3(.016f,.008f,12), grid, stage);
        for (int i = -5; i <= 5; i++) Shape("GridZ" + i, PrimitiveType.Cube, new Vector3(0,.012f,i), new Vector3(16,.008f,.016f), grid, stage);
        Shape("NorthRail", PrimitiveType.Cube, new Vector3(0,.16f,6.4f), new Vector3(17,.32f,.20f), edge, stage);
        Shape("SouthRail", PrimitiveType.Cube, new Vector3(0,.16f,-6.4f), new Vector3(17,.32f,.20f), edge, stage);
        Shape("WestRail", PrimitiveType.Cube, new Vector3(-8.4f,.16f,0), new Vector3(.20f,.32f,13), edge, stage);
        Shape("EastRail", PrimitiveType.Cube, new Vector3(8.4f,.16f,0), new Vector3(.20f,.32f,13), edge, stage);
        Shape("NorthLight", PrimitiveType.Cube, new Vector3(0,.33f,6.4f), new Vector3(16.8f,.035f,.06f), mint, stage);
        Shape("SouthLight", PrimitiveType.Cube, new Vector3(0,.33f,-6.4f), new Vector3(16.8f,.035f,.06f), mint, stage);
        Shape("WestLight", PrimitiveType.Cube, new Vector3(-8.4f,.33f,0), new Vector3(.06f,.035f,12.8f), mint, stage);
        Shape("EastLight", PrimitiveType.Cube, new Vector3(8.4f,.33f,0), new Vector3(.06f,.035f,12.8f), mint, stage);
        foreach (float x in new[]{-8.4f,8.4f}) foreach (float z in new[]{-6.4f,6.4f})
        {
            Shape("CornerTower", PrimitiveType.Cube, new Vector3(x,.5f,z), new Vector3(.5f,1,.5f), edge, stage);
            Shape("CornerBeacon", PrimitiveType.Cube, new Vector3(x,1.03f,z), new Vector3(.34f,.08f,.34f), mint, stage);
        }
        var player = new GameObject("Player").transform;
        player.position = new Vector3(0,.55f,-4.4f);
        var ball = Shape("RollingCore", PrimitiveType.Sphere, player.position, Vector3.one, pearl, player);
        var band = Shape("EnergyBand", PrimitiveType.Cylinder, player.position, new Vector3(1.035f,.06f,1.035f), mint, ball.transform);
        band.transform.localRotation = Quaternion.Euler(0,0,30);
        var marker = Shape("PlayerMarker", PrimitiveType.Cylinder, new Vector3(0,.04f,-4.4f), new Vector3(1.35f,.015f,1.35f), grid, player);
        game.player = player;
        game.ball = ball.transform;
        Vector3[] positions = {new Vector3(-6,1,-4),new Vector3(-6,1,0),new Vector3(-6,1,4),new Vector3(0,1,4.5f),new Vector3(6,1,4),new Vector3(6,1,0),new Vector3(6,1,-4),new Vector3(0,1,0)};
        var pickups = new GameObject("Crystals").transform;
        game.crystals = new Transform[positions.Length];
        for (int i=0;i<positions.Length;i++)
        {
            var crystal = Shape("Crystal_" + (i+1).ToString("00"), PrimitiveType.Cube, positions[i], new Vector3(.55f,.8f,.55f), gold, pickups);
            crystal.transform.rotation = Quaternion.Euler(0,45,45);
            game.crystals[i] = crystal.transform;
            Shape("Pad_" + (i+1).ToString("00"), PrimitiveType.Cylinder, new Vector3(positions[i].x,.04f,positions[i].z), new Vector3(1.15f,.025f,1.15f), grid, stage);
        }
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.transform.position = new Vector3(0,16,-16);
        cam.transform.LookAt(new Vector3(0,0,.4f));
        cam.orthographic = true;
        cam.orthographicSize = 8.8f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.012f,.024f,.045f);
        cam.allowHDR = true;
        var cameraData = cam.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask = 1;
        var sun = new GameObject("KeyLight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 2.3f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48,-32,0);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.22f,.32f,.45f);
        var volume = new GameObject("Global Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile,Root + "/DemoVolume.asset");
        var bloom = profile.Add<Bloom>(true); bloom.intensity.value = .3f; bloom.threshold.value = 1f;
        var tone = profile.Add<Tonemapping>(true); tone.mode.value = TonemappingMode.ACES;
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
        volume.sharedProfile = profile;
        BuildHud(game);
        EditorSceneManager.SaveScene(scene,Root + "/CollectorDemo.unity");
        EditorBuildSettings.scenes = new[]{new EditorBuildSettingsScene(Root + "/CollectorDemo.unity",true)}
            .Concat(EditorBuildSettings.scenes.Where(s=>s.path != Root + "/CollectorDemo.unity")).ToArray();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = game.gameObject;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(Vector3.zero,Quaternion.Euler(50,0,0),14);
        return "Created CollectorDemo: 8 crystals, player, camera, URP volume, Canvas, EventSystem; saved to " + scene.path;
    }

    static Material Mat(string name, Color color, float metal, float smooth, float emission=0)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("URP Lit shader missing");
        var m = new Material(shader) { name = name };
        m.SetColor("_BaseColor",color); m.SetFloat("_Metallic",metal); m.SetFloat("_Smoothness",smooth);
        if(emission>0) {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
        AssetDatabase.CreateAsset(m,Root + "/Materials/" + name + ".mat");
        return m;
    }

    static GameObject Shape(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        var obj = GameObject.CreatePrimitive(type); obj.name = name;
        obj.transform.SetParent(parent,false); obj.transform.position = pos; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = mat;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent,false); r.anchorMin=r.anchorMax=anchor; r.pivot=pivot; r.anchoredPosition=pos; r.sizeDelta=size;
        return r;
    }
    static UnityEngine.UI.Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        var img=Rect(name,parent,anchor,pivot,pos,size).gameObject.AddComponent<UnityEngine.UI.Image>(); img.color=color; img.raycastTarget=false; return img;
    }
    static TMP_Text Label(string name,string text,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size,float fontSize,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        var t=Rect(name,parent,anchor,pivot,pos,size).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font; t.text=text; t.fontSize=fontSize; t.color=color; t.alignment=alignment; t.raycastTarget=false;
        return t;
    }
    static void BuildHud(CollectorGame game)
    {
        var canvas=new GameObject("HUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
        new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var tl=new Vector2(0,1); var tr=Vector2.one; var center=new Vector2(.5f,.5f);
        Label("Eyebrow","UNITY / INTERACTIVE LAB  01",canvas.transform,tl,tl,new Vector2(36,-24),new Vector2(430,24),14,Mint);
        var title=Label("Title","ORBIT COLLECTOR",canvas.transform,tl,tl,new Vector2(34,-48),new Vector2(530,54),36,Color.white);title.fontStyle=FontStyles.Bold;
        Label("Subtitle","A tiny world. Eight sparks. One clean sweep.",canvas.transform,tl,tl,new Vector2(36,-106),new Vector2(530,24),16,new Color(.53f,.66f,.73f));
        var score=Panel("ScoreCard",canvas.transform,tr,tr,new Vector2(-34,-24),new Vector2(220,118),Ink);
        Label("ScoreLabel","ENERGY RECOVERED",score.transform,tl,tl,new Vector2(20,-15),new Vector2(180,22),12,Mint);
        game.scoreText=Label("Score","00 / 08",score.transform,tl,tl,new Vector2(20,-40),new Vector2(180,52),36,Color.white);
        Panel("ProgressTrack",score.transform,new Vector2(0,0),Vector2.zero,new Vector2(20,14),new Vector2(180,4),new Color(.12f,.22f,.28f));
        var fill=Panel("Progress",score.transform,Vector2.zero,Vector2.zero,new Vector2(20,14),new Vector2(180,4),Mint);
        var tex=Texture2D.whiteTexture; fill.sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f));
        AssetDatabase.CreateAsset(fill.sprite,Root+"/ProgressSprite.asset");
        fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fill.fillAmount=0; game.progressFill=fill;
        game.timeText=Label("Timer","000.0 s",canvas.transform,tr,tr,new Vector2(-36,-156),new Vector2(220,38),27,new Color(.60f,.73f,.80f),TextAlignmentOptions.Right);
        var footer=Panel("ControlsBar",canvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,24),new Vector2(840,58),Ink);
        Label("Controls","WASD / ARROWS   Move     |     R   Restart",footer.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(22,0),new Vector2(440,42),15,Color.white);
        game.statusText=Label("Status","COLLECT THE 8 ENERGY CRYSTALS",footer.transform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-22,0),new Vector2(350,42),13,Mint,TextAlignmentOptions.Right);
        var win=Panel("Victory",canvas.transform,center,center,Vector2.zero,new Vector2(510,240),Ink);
        Label("VictoryTitle","CIRCUIT COMPLETE",win.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-32),new Vector2(470,52),32,Mint,TextAlignmentOptions.Center);
        game.victoryText=Label("VictoryDetails","All energy recovered",win.transform,center,center,new Vector2(0,12),new Vector2(470,36),18,Color.white,TextAlignmentOptions.Center);
        var buttonImage=Panel("RestartButton",win.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,30),new Vector2(210,48),Mint);
        buttonImage.raycastTarget=true;
        var button=buttonImage.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=buttonImage;
        Label("ButtonText","PLAY AGAIN  /  R",button.transform,center,center,Vector2.zero,new Vector2(200,42),17,Ink,TextAlignmentOptions.Center);
        game.restartButton=button;game.victoryPanel=win.gameObject;win.gameObject.SetActive(false);
    }
}
