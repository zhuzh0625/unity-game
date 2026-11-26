using UnityEditor;
using UnityEngine;

namespace Galleon.Editor
{
    public sealed class DeckhandImporter : AssetPostprocessor
    {
        const string ModelPath = "Assets/Naval/Resources/Crew/Deckhand_R06.fbx";
        const string MedicineMaterialPath = "Assets/Naval/Resources/Crew/MedicineBottle.mat";

        // Each block in the exported take contains frames 0..24, including the loop seam.
        static readonly string[] Poses =
        {
            "Idle", "Run", "HelmSmall", "HelmMedium", "GunSmall", "GunMediumUpper",
            "GunMediumLower", "PumpSmall", "PumpMedium", "Repair", "Swim", "Prone"
        };

        void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var model = (ModelImporter)assetImporter;
            model.animationType = ModelImporterAnimationType.Legacy;
            model.generateAnimations = ModelImporterGenerateAnimations.InRoot;
            model.animationCompression = ModelImporterAnimationCompression.Off;
            model.preserveHierarchy = true;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            var clips = new ModelImporterClipAnimation[Poses.Length + 4];
            for (int i = 0; i < Poses.Length; i++)
            {
                clips[i] = new ModelImporterClipAnimation
                {
                    takeName = "DeckhandTake",
                    name = Poses[i],
                    firstFrame = i * 25,
                    lastFrame = i * 25 + 24,
                    wrapMode = WrapMode.Loop,
                    loopTime = true
                };
            }
            int start = Poses.Length * 25;
            clips[Poses.Length] = Clamped("PistolFire", start, start + 36);
            clips[Poses.Length + 1] = Clamped("CutlassSlash", start + 37, start + 82);
            clips[Poses.Length + 2] = Clamped("CutlassSlashRight", start + 83, start + 128);
            clips[Poses.Length + 3] = Clamped("MedicineHold", start + 129, start + 130);
            model.clipAnimations = clips;
        }

        public override uint GetVersion() => 1;

        void OnPostprocessModel(GameObject model)
        {
            if (assetPath != ModelPath) return;
            context.DependsOnArtifact(MedicineMaterialPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MedicineMaterialPath);
            if (!material) return;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name == "MedicineBottle" || renderer.name == "MedicineBottleCork")
                    renderer.sharedMaterial = material;
        }

        static ModelImporterClipAnimation Clamped(string name, int first, int last) => new ModelImporterClipAnimation
        {
            takeName = "DeckhandTake", name = name, firstFrame = first, lastFrame = last,
            wrapMode = WrapMode.ClampForever, loopTime = false
        };
    }
}
