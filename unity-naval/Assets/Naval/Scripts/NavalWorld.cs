using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Galleon
{
    public sealed class NavalWorld : MonoBehaviour
    {
        public static Material ShotMaterial { get; private set; }
        public NavalShip Pirate;
        public NavalShip Navy;
        GameObject ocean;
        Camera viewer;
        Material sea, sky, rock;
        Mesh seaMesh;

        internal void Initialize(Camera camera, Light sun)
        {
            viewer = camera;
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .045f;
            camera.farClipPlane = 2500f;
            camera.fieldOfView = 59f;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .9f, .73f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .66f;
            sun.transform.rotation = Quaternion.Euler(35, -41, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.71f, .80f, .85f);
            RenderSettings.ambientEquatorColor = new Color(.53f, .62f, .64f);
            RenderSettings.ambientGroundColor = new Color(.40f, .34f, .31f);
            sky = new Material(Resources.Load<Shader>("Sea/SeaSky"));
            RenderSettings.skybox = sky;
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Resources.Load<VolumeProfile>("Sea/Daylight");

            sea = new Material(Resources.Load<Shader>("Sea/OpenSea"));
            seaMesh = OceanGrid();
            ocean = CreateMesh("Open sea", seaMesh, sea);
            ShotMaterial = Lit(new Color(.15f, .14f, .12f));
            rock = Lit(new Color(.29f, .38f, .36f));
            MakeIsland(new Vector3(130, -4, 240), 32, 17, 6);
            MakeIsland(new Vector3(-175, -4, 120), 48, 14, 24);
            MakeIsland(new Vector3(80, -4, -250), 42, 19, 61);
        }

        void LateUpdate()
        {
            if (!Pirate)
            {
                ocean.transform.position = Vector3.zero;
                sea.SetVector("_ShipBoundsA", Vector4.zero);
                sea.SetVector("_ShipBoundsB", Vector4.zero);
                sea.SetVector("_DeckFlood", Vector4.zero);
                return;
            }
            Vector3 middle = viewer.transform.position;
            ocean.transform.position = new Vector3(Mathf.Floor(middle.x / 30) * 30, 0, Mathf.Floor(middle.z / 30) * 30);
            sea.SetMatrix("_ShipA", Pirate.transform.worldToLocalMatrix);
            sea.SetTexture("_ShipWidthsA", Pirate.Rig.SectionTexture);
            sea.SetVector("_ShipBoundsA", Pirate.Rig.Definition.SectionBounds);
            if (Navy)
            {
                sea.SetMatrix("_ShipB", Navy.transform.worldToLocalMatrix);
                sea.SetTexture("_ShipWidthsB", Navy.Rig.SectionTexture);
            }
            sea.SetVector("_ShipBoundsB", Navy ? Navy.Rig.Definition.SectionBounds : Vector4.zero);
            sea.SetVector("_DeckFlood", new Vector4(DeckFlood(Pirate), Navy ? DeckFlood(Navy) : 0f, 0, 0));
            sea.SetVector("_SectionSpans", new Vector4(Pirate.Definition.Length,
                Navy ? Navy.Definition.Length : 1f, 0, 0));
        }

        static float DeckFlood(NavalShip ship) => Mathf.InverseLerp(
            ship.Rig.Definition.Data.upperDeck, ship.Rig.Definition.SinkingHeight, ship.Flooding.WaterHeight);

        // The swimmer rides the same two swells and flood shelter as OpenSea.shader.
        internal float SurfaceHeight(Vector3 position)
        {
            float t = Time.time;
            float wave = .12f * Mathf.Sin(position.x * .12f + position.z * .19f - t * .8f)
                       + .048f * Mathf.Sin(-position.x * .34f + position.z * .17f + t * 1.12f);
            return wave * (1f - Mathf.Max(Shelter(Pirate, position), Shelter(Navy, position)));
        }

        static float Shelter(NavalShip vessel, Vector3 position)
        {
            if (!vessel) return 0f;
            float flooded = DeckFlood(vessel);
            if (flooded <= 0f) return 0f;
            var local = vessel.transform.InverseTransformPoint(position);
            float halfLength = vessel.Definition.PivotZ;
            float x = Mathf.Max(0f, Mathf.Abs(local.x) - vessel.Definition.SectionBounds.z);
            float z = Mathf.Max(0f, Mathf.Abs(local.z - halfLength) - halfLength);
            float t = Mathf.InverseLerp(6f, 14f, Mathf.Sqrt(x*x + z*z));
            return flooded * (1f - Mathf.SmoothStep(0f, 1f, t));
        }

        public static Material Lit(Color color)
        {
            var mat = new Material(Resources.Load<Material>("Sea/ShipFittings"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", .26f);
            return mat;
        }

        GameObject CreateMesh(string label, Mesh mesh, Material material)
        {
            var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(transform, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return item;
        }

        static Mesh OceanGrid()
        {
            const int count = 97;
            var vertices = new Vector3[count * count];
            var triangles = new int[(count - 1) * (count - 1) * 6];
            int k = 0;
            for (int z = 0; z < count; z++)
            for (int x = 0; x < count; x++)
            {
                int n = z * count + x;
                vertices[n] = new Vector3(Spacing(x), 0, Spacing(z));
                if (x + 1 == count || z + 1 == count) continue;
                triangles[k++] = n; triangles[k++] = n + count; triangles[k++] = n + 1;
                triangles[k++] = n + 1; triangles[k++] = n + count; triangles[k++] = n + count + 1;
            }
            var mesh = new Mesh {name="Sea surface", vertices=vertices, triangles=triangles};
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(4600, 4, 4600));
            return mesh;

            static float Spacing(int i)
            {
                float t = (i - 48) / 48f;
                return t * 128f + t * Mathf.Pow(Mathf.Abs(t), 6) * 2100f;
            }
        }

        void MakeIsland(Vector3 position, float radius, float height, int seed)
        {
            const int segments = 19;
            const int rings = 5;
            var random = new System.Random(seed);
            var vertices = new Vector3[segments * rings + 1];
            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                float r = radius * (.72f + (float)random.NextDouble() * .28f);
                for (int j = 0; j < rings; j++)
                {
                    float t = j / (float)rings;
                    float jitter = (float)random.NextDouble() * .13f;
                    vertices[j * segments + i] = new Vector3(Mathf.Cos(a) * r * (1 - t), height * (t + jitter), Mathf.Sin(a) * r * .58f * (1 - t));
                    int l = j * segments + i, next = j * segments + (i + 1) % segments;
                    if (j + 1 < rings)
                        triangles.AddRange(new[] {l, l + segments, next, next, l + segments, next + segments});
                    else triangles.AddRange(new[] {l, vertices.Length - 1, next});
                }
            }
            vertices[vertices.Length - 1] = Vector3.up * height;
            var mesh = new Mesh {name="Rocky islet", vertices=vertices, triangles=triangles.ToArray()};
            mesh.RecalculateNormals();
            CreateMesh("Islet", mesh, rock).transform.position = position;
        }

        void OnDestroy()
        {
            if (RenderSettings.skybox == sky) RenderSettings.skybox = null;
            if (sea) Destroy(sea);
            if (sky) Destroy(sky);
            if (rock) Destroy(rock);
            if (ShotMaterial) Destroy(ShotMaterial);
            if (seaMesh) Destroy(seaMesh);
        }
    }
}
