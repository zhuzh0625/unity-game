using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Emberfall.Editor
{
    public static class PrototypeSetup
    {
        [MenuItem("Emberfall/Create or open prototype")]
        public static void Create()
        {
            const string path="Assets/Scenes/Emberfall.unity";
            var profile=EnsureAtmosphere();
            if(File.Exists(path)) {EditorSceneManager.OpenScene(path);return;}
            TMPro.TMP_PackageResourceImporter.ImportResources(true,false,false);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";
            var cam=camera.GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=8.7f;camera.transform.position=new Vector3(0,0,-10);
            var additional=camera.AddComponent<UniversalAdditionalCameraData>();additional.renderPostProcessing=true;
            var light=new GameObject("Global Light",typeof(UnityEngine.Rendering.Universal.Light2D)).GetComponent<UnityEngine.Rendering.Universal.Light2D>();light.lightType=UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            var volume=new GameObject("Global atmosphere",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;
            volume.sharedProfile=profile;
            new GameObject("Adventure",typeof(Adventure));
            PlayerSettings.companyName="Local Prototypes";PlayerSettings.productName="Emberfall";PlayerSettings.runInBackground=true;
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            QualitySettings.SetQualityLevel(QualitySettings.names.Length-1);
            EditorSceneManager.SaveScene(scene,path);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("EMBERFALL_SETUP_OK");
        }
        static VolumeProfile EnsureAtmosphere()
        {
            const string path="Assets/Emberfall/Atmosphere.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
            profile.components.RemoveAll(c=>c==null);
            if(!profile.TryGet<Tonemapping>(out var tone))tone=profile.Add<Tonemapping>(true);
            if(!profile.TryGet<Vignette>(out var vignette))vignette=profile.Add<Vignette>(true);
            if(!profile.TryGet<Bloom>(out var bloom))bloom=profile.Add<Bloom>(true);
            tone.mode.Override(TonemappingMode.ACES);vignette.intensity.Override(.18f);bloom.intensity.Override(.2f);
            foreach(var component in profile.components)if(!EditorUtility.IsPersistent(component))AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            return profile;
        }
        public static void Build()
        {
            if(!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
                throw new System.Exception("TMP essentials must finish importing before building.");
            Create();
            ChinesePresentationSetup.Prepare();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/Emberfall.unity"},locationPathName="Builds/Emberfall/Emberfall.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Build failed: "+report.summary.result);
            Debug.Log("EMBERFALL_BUILD_OK");
        }
    }
}
