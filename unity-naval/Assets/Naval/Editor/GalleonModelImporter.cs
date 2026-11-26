using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galleon.Editor
{
    // Import once in the editor. Both ships then instantiate the same mesh assets.
    [ScriptedImporter(4, "r13mesh")]
    public sealed class GalleonModelImporter : ScriptedImporter
    {
        const string ShaderPath = "Assets/Naval/Shaders/GalleonSurface.shader";
        const string BrigantineModelPath = "Assets/Naval/Resources/Fleet/Brigantine/Model.r13mesh";
        const string ClearGlassShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            ctx.DependsOnSourceAsset(ShaderPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new InvalidDataException("Missing R13 surface shader.");
            var material = new Material(shader) { name = "Hull wood, cloth and metal" };
            ctx.AddObjectToAsset("Surface", material);
            var root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
            ctx.AddObjectToAsset("Galleon", root);
            Material lining = null;

            using var source = File.OpenRead(ctx.assetPath);
            using var zipped = new GZipStream(source, CompressionMode.Decompress);
            // Buffering avoids 12M individual decompressor calls for the original detailed geometry.
            using var buffered = new BufferedStream(zipped, 256 * 1024);
            using var data = new BinaryReader(buffered, Encoding.UTF8);
            if (Encoding.ASCII.GetString(data.ReadBytes(4)) != "R13M" || data.ReadInt32() != 1)
                throw new InvalidDataException("Unexpected R13 file header.");
            int count = data.ReadInt32();
            var near = new List<Renderer>();
            var far = new List<Renderer>();
            for (int i = 0; i < count; i++)
            {
                string name = Encoding.UTF8.GetString(data.ReadBytes(data.ReadInt32()));
                var pivot = ReadVector(data);
                var mesh = ReadMesh(name, data);
                var part = new GameObject(name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = pivot;
                ctx.AddObjectToAsset(name, mesh);
                if (name.StartsWith("Collision_", StringComparison.Ordinal))
                {
                    part.AddComponent<MeshCollider>().sharedMesh = mesh;
                    continue;
                }
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                bool isHullLining = name.StartsWith("INNER_LINING", StringComparison.Ordinal);
                if (isHullLining)
                {
                    if (!lining)
                    {
                        string path = Path.GetDirectoryName(ctx.assetPath).Replace('\\', '/') + "/InnerOak.mat";
                        ctx.DependsOnArtifact(path);
                        lining = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!lining) throw new InvalidDataException("Missing ship lining material: " + path);
                    }
                    renderer.sharedMaterial = lining;
                }
                else if (name == "ClearGlazing" && ctx.assetPath == BrigantineModelPath)
                {
                    renderer.sharedMaterial = CreateClearGlass(ctx);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                else renderer.sharedMaterial = material;
                near.Add(renderer);
                // The lining also covers the exposed bulwarks visible from the deck and at a distance.
                if (isHullLining || !IsInterior(name)) far.Add(renderer);
            }
            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.48f, near.ToArray()), new LOD(.002f, far.ToArray()) });
            lod.RecalculateBounds();
            ctx.SetMainObject(root);
        }

        static Material CreateClearGlass(AssetImportContext ctx)
        {
            ctx.DependsOnSourceAsset(ClearGlassShaderPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ClearGlassShaderPath);
            if (!shader) throw new InvalidDataException("Missing URP Lit shader for brigantine glazing.");

            var glass = new Material(shader) { name = "Clear cabin glass" };
            glass.SetColor("_BaseColor", new Color(.98f, .995f, 1f, .035f));
            glass.SetFloat("_Smoothness", 1f - .055f);
            glass.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            glass.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Alpha);
            glass.SetFloat("_BlendModePreserveSpecular", 1f);
            // Each pane has two outward-facing surfaces, one on each side of the cabin window.
            glass.SetFloat("_Cull", (float)CullMode.Back);
            BaseShaderGUI.SetMaterialKeywords(glass);
            glass.SetShaderPassEnabled("DepthNormals", false);
            ctx.AddObjectToAsset("ClearGlass", glass);
            return glass;
        }

        static bool IsInterior(string name)
        {
            return name.StartsWith("INNER_") || name == "D0_Planks" || name == "D1_Planks"
                || name == "D2_Planks" || name == "Timbers" || name == "Bulkheads";
        }

        static Vector3 ReadVector(BinaryReader data) =>
            new Vector3(data.ReadSingle(), data.ReadSingle(), data.ReadSingle());

        static Mesh ReadMesh(string name, BinaryReader data)
        {
            int count = data.ReadInt32();
            int indexCount = data.ReadInt32();
            if (count <= 0 || count > 500000 || indexCount <= 0 || indexCount % 3 != 0)
                throw new InvalidDataException("Invalid mesh sizes in " + name);
            var position = new Vector3[count];
            var normal = new Vector3[count];
            var color = new Color32[count];
            var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                position[i] = ReadVector(data);
                normal[i] = new Vector3(data.ReadInt16(), data.ReadInt16(), data.ReadInt16()) / 32767f;
                color[i] = new Color32(data.ReadByte(), data.ReadByte(), data.ReadByte(), data.ReadByte());
                uv[i] = new Vector2(data.ReadInt16(), data.ReadInt16()) / 500f;
            }
            var indices = new int[indexCount];
            for (int i = 0; i < indexCount; i++) indices[i] = data.ReadInt32();
            var mesh = new Mesh { name = name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = position;
            mesh.normals = normal;
            mesh.colors32 = color;
            mesh.uv = uv;
            mesh.SetTriangles(indices, 0);
            // Surface damage samples triangle vertices in the player, including after a build.
            if (!name.StartsWith("HULL_") && !name.StartsWith("INNER_") && !name.StartsWith("Collision_") && name != "TRANSOM_PLANKING")
                mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
