using System;
using System.IO;
using System.Linq;
using IronBastion;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

public static class ProjectBuilder
{
    [MenuItem("Iron Bastion/Build battlefield scene")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before rebuilding the scene.");
        Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Resources/Fonts");Directory.CreateDirectory("Assets/Materials");
        if(!AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
        {
            TMPro.TMP_PackageResourceImporter.ImportResources(true,false,false);
            Debug.Log("TMP essentials are importing. Run scene setup again after import completes.");
            return;
        }
        RemapModels();
        CreateFont();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        QualitySettings.SetQualityLevel(QualitySettings.names.Length-1,true);
        var urp=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(!urp) throw new InvalidOperationException("URP pipeline asset missing");
        urp.supportsHDR=true;urp.msaaSampleCount=4;urp.shadowDistance=70;
        EditorUtility.SetDirty(urp);
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.45f,.55f,.60f);
        RenderSettings.ambientEquatorColor=new Color(.32f,.40f,.40f);
        RenderSettings.ambientGroundColor=new Color(.20f,.23f,.22f);
        var sun=new GameObject("Warm afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.1f;sun.color=new Color(1,.91f,.75f);sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.Euler(48,-36,0);
        // Clear the entire window before rendering the inset battlefield viewport.
        var backdrop=new GameObject("Full-window backdrop").AddComponent<Camera>();
        backdrop.depth=-10;backdrop.cullingMask=0;backdrop.clearFlags=CameraClearFlags.SolidColor;
        backdrop.backgroundColor=new Color(.015f,.025f,.028f);backdrop.allowHDR=false;
        backdrop.GetUniversalAdditionalCameraData().renderShadows=false;
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(0,33,-25);camera.transform.LookAt(new Vector3(0,0,.3f));camera.fieldOfView=44;
        camera.nearClipPlane=.1f;camera.farClipPlane=150;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.07f,.085f);
        camera.rect=new Rect(0,.065f,.81f,.85f);camera.allowHDR=true;camera.gameObject.AddComponent<AudioListener>();
        var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.volumeLayerMask=1;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/BattleVolume.asset");
        if(!profile) {profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,"Assets/Settings/BattleVolume.asset");}
        if(!profile.TryGet<Tonemapping>(out var tone)) {tone=profile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.ACES);
        if(!profile.TryGet<Bloom>(out var bloom)) {bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}bloom.intensity.Override(.25f);bloom.threshold.Override(1.05f);
        if(!profile.TryGet<Vignette>(out var vignette)) {vignette=profile.Add<Vignette>(true);AssetDatabase.AddObjectToAsset(vignette,profile);}vignette.intensity.Override(.15f);
        var volume=new GameObject("Global color grade").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        var game=new GameObject("Battle Director").AddComponent<BattleGame>();game.Preview=WorldBuilder.Build(1);game.Preview.name="Editor battlefield preview";
        // Persist procedural preview materials so the saved scene never contains missing material references.
        foreach(var r in game.Preview.GetComponentsInChildren<Renderer>())
        {
            var material=r.sharedMaterial;if(!material||AssetDatabase.Contains(material))continue;
            var path="Assets/Materials/"+material.name.Replace('/','_')+".mat";
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(path));
        }
        PlayerSettings.companyName="Independent Arcade";PlayerSettings.productName="Iron Bastion - 铁甲堡垒";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.colorSpace=ColorSpace.Linear;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Battlefield.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Battlefield.unity",true)};
        AssetDatabase.SaveAssets();
        Debug.Log("IRON BASTION: scene, model materials, font and build settings ready.");
    }

    static void CreateFont()
    {
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/IB-Regular.asset"))return;
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
        if(!source)throw new InvalidOperationException("Noto CJK font not imported");
        var font=TMP_FontAsset.CreateFontAsset(source,40,5,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        font.name="IB-Regular";
        AssetDatabase.CreateAsset(font,"Assets/Resources/Fonts/IB-Regular.asset");
        font.material.name="IB-Regular Material";AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
        string chars=string.Concat(Directory.GetFiles("Assets/Scripts","*.cs").Select(File.ReadAllText));
        font.TryAddCharacters(chars,out string missing);
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
    }

    static void RemapModels()
    {
        foreach(var path in Directory.GetFiles("Assets/Resources/Models","*.fbx"))
        {
            var normalized=path.Replace('\\','/');
            var importer=AssetImporter.GetAtPath(normalized) as ModelImporter;
            importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(normalized);
            foreach(var renderer in asset.GetComponentsInChildren<Renderer>())
                foreach(var original in renderer.sharedMaterials)
                {
                    if(!original)continue;
                    string materialPath="Assets/Materials/"+original.name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(!material)
                    {
                        material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=original.name};
                        var color=original.HasProperty("_Color")?original.GetColor("_Color"):original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):Color.gray;
                        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.3f);
                        if(original.name.Contains("Beacon")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*1.5f);}
                        AssetDatabase.CreateAsset(material,materialPath);
                    }
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),original.name),material);
                }
            importer.SaveAndReimport();
        }
    }

    [MenuItem("Iron Bastion/Build Windows player")]
    public static void BuildWindows()
    {
        PlayerSettings.bundleVersion="1.0.3";
        Directory.CreateDirectory("Builds/Release-1.0.3");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Battlefield.unity"},locationPathName="Builds/Release-1.0.3/IronBastion.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        Directory.CreateDirectory("tmp");File.WriteAllText("tmp/build-result.json",JsonUtility.ToJson(new BuildReceipt{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize},true));
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
    }
    [Serializable]class BuildReceipt{public string result;public int errors,warnings;public ulong bytes;}
}
