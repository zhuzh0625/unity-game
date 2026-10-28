using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CollectorDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReplaceCollectorCrystals
{
    const string ModelPath = "Assets/CollectorDemo/Models/CollectorCrystal.fbx";
    const string PrefabPath = "Assets/CollectorDemo/Prefabs/EnergyCrystal.prefab";
    const string MaterialDir = "Assets/CollectorDemo/Materials/BlenderCrystal";

    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before replacing assets.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/CollectorDemo/CollectorDemo.unity" || scene.isDirty)
            throw new Exception("Open the saved CollectorDemo scene before replacement.");
        var game = UnityEngine.Object.FindFirstObjectByType<CollectorGame>();
        if (game == null || game.crystals.Length != 8) throw new Exception("Expected eight crystals.");
        foreach (var crystal in game.crystals)
            if (crystal == null || crystal.GetComponent<MeshFilter>() == null || crystal.childCount != 0)
                throw new Exception("A crystal has changed; inspect before replacing.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            throw new Exception("Crystal prefab already exists; inspect instead of rebuilding.");

        Directory.CreateDirectory("ArtSource/Backups");
        string backup = "ArtSource/Backups/CollectorDemo-before-blender.unity";
        if (!File.Exists(backup)) File.Copy(scene.path, backup);
        Directory.CreateDirectory(MaterialDir);
        Directory.CreateDirectory("Assets/CollectorDemo/Prefabs");
        AssetDatabase.Refresh();
        var materials = new Dictionary<string, Material>
        {
            ["Crystal_Amber"] = Material("Crystal_Amber", new Color(1f,.48f,.055f), .25f, .7f, .26f),
            ["Crystal_Honey"] = Material("Crystal_Honey", new Color(1f,.72f,.18f), .2f, .72f, .28f),
            ["Crystal_Shadow"] = Material("Crystal_Shadow", new Color(.58f,.16f,.018f), .3f, .65f, .12f),
            ["Crystal_Tip"] = Material("Crystal_Tip", new Color(1f,.91f,.49f), .2f, .75f, .35f),
            ["Crystal_Band"] = Material("Crystal_Band", new Color(.055f,.095f,.12f), .75f, .7f, 0),
            ["Crystal_Trim"] = Material("Crystal_Trim", new Color(.24f,.95f,.70f), .3f, .7f, 1.5f)
        };
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importNormals = ModelImporterNormals.Import;
        foreach (var pair in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new Exception("FBX did not import.");

        var staging = new GameObject("EnergyCrystal");
        GameObject prefab;
        Bounds bounds;
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, staging.transform);
            // Retain FBX root's -90 degree X correction: Blender Z-up becomes Unity Y-up.
            visual.transform.localPosition = Vector3.zero;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length != 5) throw new Exception("Expected five imported meshes.");
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                if (renderer.sharedMaterials.Any(m => m == null || m.shader.name != "Universal Render Pipeline/Lit"))
                    throw new Exception("Missing URP material remap.");
            }
            if (bounds.size.y < 1.6f || bounds.size.y > 1.9f || bounds.size.x > 1.5f)
                throw new Exception("Unexpected model scale/orientation: " + bounds.size);
            prefab = PrefabUtility.SaveAsPrefabAsset(staging, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(staging); }

        Undo.SetCurrentGroupName("Replace demo crystal visuals with Blender asset");
        for (int i = 0; i < game.crystals.Length; i++)
        {
            Transform crystal = game.crystals[i];
            Undo.RecordObject(crystal, "Set crystal upright");
            crystal.localScale = Vector3.one;
            crystal.localRotation = Quaternion.Euler(0, i * 37f, 0);
            Undo.DestroyObjectImmediate(crystal.GetComponent<MeshRenderer>());
            Undo.DestroyObjectImmediate(crystal.GetComponent<MeshFilter>());
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, crystal);
            instance.name = "BlenderCrystalVisual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(instance, "Install Blender crystal");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = game.crystals[0].gameObject;
        return $"Replaced {game.crystals.Length} visuals, preserving collection transforms/references. Prefab={PrefabPath}, bounds={bounds.size}, triangles={model.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3)}; saved {scene.path}.";
    }

    static Material Material(string name, Color color, float metallic, float smoothness, float emission)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("URP Lit shader not available.");
        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (emission > 0)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
        }
        AssetDatabase.CreateAsset(material, MaterialDir + "/" + name + ".mat");
        return material;
    }
}
