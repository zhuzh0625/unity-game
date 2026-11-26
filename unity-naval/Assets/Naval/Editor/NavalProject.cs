using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Galleon.Editor
{
    public static class NavalProject
    {
        public const string ScenePath = "Assets/Scenes/SeaTrial.unity";
        const string FontDir = "Assets/Naval/Resources/Typography";

        [MenuItem("Galleon/Open sea trial")]
        public static void OpenTrial()
        {
            Configure();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static string Configure()
        {
            const string fittingsPath = "Assets/Naval/Resources/Sea/ShipFittings.mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(fittingsPath))
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("URP Lit is required.");
                AssetDatabase.CreateAsset(new Material(shader), fittingsPath);
            }
            CreateFont();
            CreateDaylight();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Sea trial", typeof(NavalGame));
                var camera = new GameObject("Crew camera", typeof(Camera));
                camera.tag = "MainCamera";
                camera.transform.SetPositionAndRotation(new Vector3(1, 9, -2), Quaternion.Euler(8, 8, 0));
                var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(35, -41, 0);
                RenderSettings.sun = sun;
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save the sea trial.");
            }
            EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(ScenePath, true)};
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            PlayerSettings.companyName = "Galleon";
            PlayerSettings.productName = "风帆海战";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            return ScenePath;
        }

        static void CreateFont()
        {
            string assetPath = FontDir + "/SailorText.asset";
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/NotoSansCJKsc-Regular.otf");
                if (!source) throw new IOException("Chinese typeface unavailable.");
                font = TMP_FontAsset.CreateFontAsset(source, 42, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "SailorText";
                AssetDatabase.CreateAsset(font, assetPath);
                font.material.name="SailorText Material";
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas,font);
            }
            string all = string.Concat(Directory.GetFiles("Assets/Naval/Scripts", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            string glyphs = new string(all.Where(c => c >= 32 && !char.IsControl(c)).Distinct().ToArray());
            if (font.atlasPopulationMode == AtlasPopulationMode.Static && font.atlasTexture.width >= 32
                && glyphs.All(c => font.HasCharacter(c)))
                return;

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.TryAddCharacters(glyphs,out _);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            string missingGlyphs = new string(glyphs.Where(c => !font.HasCharacter(c)).ToArray());
            if (missingGlyphs.Length > 0)
                throw new InvalidOperationException("Missing glyphs in the Chinese HUD atlas: " + missingGlyphs);
            if (font.atlasTexture.width < 32)
                throw new InvalidOperationException("The Chinese HUD glyph atlas was not populated.");
            font.material.mainTexture = font.atlasTexture;
            foreach (var atlas in font.atlasTextures)
            {
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas,font);
                EditorUtility.SetDirty(atlas);
            }
            EditorUtility.SetDirty(font.material);
            EditorUtility.SetDirty(font);
        }

        static void CreateDaylight()
        {
            const string path = "Assets/Naval/Resources/Sea/Daylight.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path)) return;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile,path);
            var tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(.2f);
            bloom.threshold.Override(1.2f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile);
        }

        [MenuItem("Galleon/Build Windows player")]
        public static void BuildFromMenu() => BuildWindows();

        public static string BuildWindows() => BuildWindows("Windows");

        public static void BuildNextWindows() => BuildWindows("Windows-Next");

        static string BuildWindows(string folder)
        {
            Configure();
            string exe = Path.GetFullPath(Path.Combine("Builds", folder, "Galleon.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(exe));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] {ScenePath},
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows player build failed: " + report.summary.result);
            return exe;
        }
    }
}
