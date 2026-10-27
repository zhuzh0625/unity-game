using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronBastion
{
    public enum SurfaceKind { Brick, Steel, Water, Base }

    public static class WorldBuilder
    {
        public const int Size = 13;
        public const int SolidLayer = 8;
        public const int TankLayer = 9;
        public const int WaterLayer = 10;
        public const int MovementMask = (1 << SolidLayer) | (1 << TankLayer) | (1 << WaterLayer);
        public const int ShotMask = (1 << SolidLayer) | (1 << TankLayer);
        public static readonly Vector3 BasePosition = new(0, 0, -12);
        public static readonly string[][] Maps = {
            new[] { ".............", ".BB.B.B.B.BB.", ".BB.S.B.S.BB.", ".B..B.B..B...", "....WWW......", "BB..W.W..BB..", "..S.....S....", ".BB.B.B.B.BB.", "...B...B.....", ".B...S...B.B.", ".B.B...B.B.B.", ".....BBB.....", ".....BHB....." },
            new[] { ".............", ".B.S.B.S.B.B.", ".B...B...B.B.", "...BB.BB.....", "WW..S.S..WW..", "....B.B......", ".SS.....SS...", ".B..WWW..B.B.", ".B.......B.B.", "...BS.SB.....", ".B.B...B.B.B.", ".....BBB.....", ".....BHB....." },
            new[] { ".............", ".S.B.B.B.S.B.", "...B...B.....", ".BBB.S.BBB.B.", "...W...W.....", ".S.W.B.W.S.B.", "...B.B.B.....", ".BB..S..BB.B.", "....BBB......", ".S.B...B.S.B.", "...B...B.....", ".....BBB.....", ".....BHB....." }
        };
        static readonly Dictionary<string, Material> materials = new();
        public static Vector3 Cell(int c, int r) => new((c - 6) * 2, 0, (6 - r) * 2);
        public static Vector2Int Grid(Vector3 p) => new(Mathf.Clamp(Mathf.RoundToInt(p.x / 2) + 6, 0, 12), Mathf.Clamp(6 - Mathf.RoundToInt(p.z / 2), 0, 12));
        public static bool Inside(Vector3 p) => Mathf.Abs(p.x) < 12.4f && Mathf.Abs(p.z) < 12.4f;

        public static Material Material(string name, Color color, float metallic = 0, float emission = 0)
        {
            if (materials.TryGetValue(name, out var existing) && existing) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader missing");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .25f); m.SetFloat("_Metallic", metallic);
            if (emission > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); }
            materials[name] = m;
            return m;
        }

        public static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 size, Material material, int layer = 0, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.layer = layer;
            var col = go.GetComponent<Collider>();
            if (!collider) { col.enabled = false; if (Application.isPlaying) UnityEngine.Object.Destroy(col); else UnityEngine.Object.DestroyImmediate(col); }
            return go;
        }

        public static GameObject Model(string name, Transform parent, Vector3 pos)
        {
            var asset = Resources.Load<GameObject>("Models/" + name);
            if (!asset) throw new InvalidOperationException("Missing Blender model: " + name);
            var go = UnityEngine.Object.Instantiate(asset, parent); go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        public static Transform Build(int stage)
        {
            var root = new GameObject("Battlefield").transform;
            var ground = Material("Sandstone", new Color(.36f, .39f, .32f));
            var tile = Material("Sandstone seam", new Color(.30f, .33f, .28f));
            var edge = Material("Diorama edge", new Color(.095f, .15f, .16f));
            var trim = Material("Brass markings", new Color(.72f, .56f, .27f), .5f);
            Cube("Floating plinth", root, new(0, -.65f, 0), new(28, 1.2f, 28), edge);
            Cube("Battlefield sand", root, new(0, -.08f, 0), new(26, .18f, 26), ground);
            for (int i = 0; i <= 13; i++)
            {
                float n = -13 + i * 2;
                Cube("Tile seam", root, new(n, .018f, 0), new(.025f, .015f, 26), tile);
                Cube("Tile seam", root, new(0, .018f, n), new(26, .015f, .025f), tile);
            }
            for (int side = 0; side < 4; side++)
            {
                var border = Cube("Boundary", root, new(0, .15f, 13.45f), new(28, .7f, .65f), edge, SolidLayer, true);
                border.transform.RotateAround(Vector3.zero, Vector3.up, side * 90);
                for (int i = -6; i <= 6; i++)
                {
                    var mark = Cube("Edge tick", root, new(i * 2, .515f, 13.45f), new(.22f, .015f, .44f), trim);
                    mark.transform.RotateAround(Vector3.zero, Vector3.up, side * 90);
                }
            }
            var map = Maps[Mathf.Clamp(stage - 1, 0, 2)];
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                {
                    Vector3 p = Cell(c, r);
                    switch (map[r][c])
                    {
                        case 'B':
                            for (int x = -1; x <= 1; x += 2)
                                for (int z = -1; z <= 1; z += 2)
                                    AddBlock("Brick", root, p + new Vector3(x * .5f, 0, z * .5f), new(.96f, 1.25f, .96f), SurfaceKind.Brick, 1);
                            break;
                        case 'S': AddBlock("Steel", root, p, new(1.94f, 1.35f, 1.94f), SurfaceKind.Steel, 1); break;
                        case 'H': AddBlock("Headquarters", root, p, new(1.7f, 1.65f, 1.7f), SurfaceKind.Base, 1); break;
                        case 'W':
                            var water = Material("Water", new Color(.12f, .43f, .50f), .35f);
                            var pool = Cube("Water", root, p + Vector3.up * .06f, new(1.98f, .14f, 1.98f), water, WaterLayer, true);
                            // A tall movement-only volume avoids precision-dependent shore collisions.
                            pool.GetComponent<BoxCollider>().size = new Vector3(1, 10, 1);
                            for (int j = 0; j < 3; j++) Cube("Water ripple", root, p + new Vector3(.16f * j - .25f, .14f, j * .5f - .5f), new(.8f, .012f, .025f), Material("Foam", new Color(.37f, .70f, .70f)));
                            break;
                    }
                }
            // Foliage is passable and never hides the player's entire silhouette.
            foreach (var p in new[] { new Vector3(-10,0,-4), new Vector3(10,0,2), new Vector3(6,0,-4), new Vector3(-4,0,6) })
            {
                var cell = Grid(p); if (map[cell.y][cell.x] != '.') continue;
                var tree = Model("Pine", root, p + new Vector3(.5f, 0, .5f)); tree.transform.localScale = Vector3.one * .72f;
            }
            foreach (float x in new[] { -10f, 0f, 10f })
            {
                Cube("Enemy deployment stripe", root, new(x, .04f, 12), new(1.45f,.025f,.12f), Material("Enemy marker", new Color(.8f,.31f,.23f), 0, .3f));
            }
            return root;
        }

        static void AddBlock(string model, Transform parent, Vector3 pos, Vector3 size, SurfaceKind kind, int hp)
        {
            var go = new GameObject(kind.ToString()); go.transform.SetParent(parent); go.transform.localPosition = pos;
            go.layer = SolidLayer;
            Model(model, go.transform, Vector3.zero);
            var collider = go.AddComponent<BoxCollider>(); collider.size = size; collider.center = Vector3.up * size.y * .5f;
            var block = go.AddComponent<BattleBlock>(); block.Kind = kind; block.HitPoints = hp;
        }
    }
}
