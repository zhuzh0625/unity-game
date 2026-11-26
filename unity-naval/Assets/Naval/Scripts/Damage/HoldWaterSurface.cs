using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galleon
{
    /// <summary>Standing water and its submerged volume in the installed ship's local coordinates.</summary>
    internal sealed class HoldWaterSurface
    {
        private const int Sections = 64;
        private const int RingSize = 2 * (Sections + 1);
        private readonly ShipDefinition definition;
        private readonly float[] ringHeights;

        private readonly GameObject visual;
        private readonly Mesh mesh;
        private readonly Material surface, volume;
        private readonly Vector3[] vertices;
        private float previousHeight = float.NegativeInfinity;

        public bool Visible => visual != null && visual.activeSelf;

        public HoldWaterSurface(GalleonRig rig, Material surface, Material volume)
        {
            definition = rig.Definition;
            ringHeights = BuildRings(definition);
            vertices = new Vector3[(ringHeights.Length + 1) * RingSize];
            this.surface = surface;
            this.volume = volume;
            volume.SetTexture("_HullWidths", rig.SectionTexture);
            volume.SetVector("_HullBounds", definition.SectionBounds);
            volume.SetFloat("_SectionSpan", definition.Length);
            visual = new GameObject("Standing water inside hull");
            visual.transform.SetParent(rig.transform, false);
            visual.SetActive(false);
            mesh = new Mesh { name = "Bilge and flooded decks" };
            mesh.MarkDynamic();
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { surface, volume };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var lid = new List<int>(Sections * 6);
            var shell = new List<int>();
            for (int i = 0; i < Sections; i++)
            {
                int v = 2 * i;
                Quad(lid, v, v + 2, v + 3, v + 1);
                int floor = v + ringHeights.Length * RingSize;
                Quad(shell, floor, floor + 1, floor + 3, floor + 2);
                for (int ring = 0; ring < ringHeights.Length; ring++)
                {
                    int port = ring * RingSize + v;
                    Quad(shell, port, port + RingSize, port + RingSize + 2, port + 2);
                    int starboard = port + 1;
                    Quad(shell, starboard, starboard + 2, starboard + RingSize + 2, starboard + RingSize);
                }
            }
            for (int ring = 0; ring < ringHeights.Length; ring++)
            {
                int aft = ring * RingSize, fore = aft + 2 * Sections;
                Quad(shell, aft, aft + 1, aft + RingSize + 1, aft + RingSize);
                Quad(shell, fore, fore + RingSize, fore + RingSize + 1, fore + 1);
            }
            shell.AddRange(lid);
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(lid, 0, false);
            mesh.SetTriangles(shell, 1, false);
        }

        private static void Quad(List<int> indices, int a, int b, int c, int d)
        {
            indices.Add(a); indices.Add(b); indices.Add(c);
            indices.Add(a); indices.Add(c); indices.Add(d);
        }

        static float[] BuildRings(ShipDefinition ship)
        {
            var d = ship.Data;
            var levels = new SortedSet<float> { d.dryHeight, ship.SinkingHeight };
            foreach (float height in new[] { d.holdCeiling, d.lowerDeck, d.mainDeck, d.upperDeck })
                levels.Add(Mathf.Clamp(height, d.dryHeight, ship.SinkingHeight));
            for (int i = 1; i < 9; i++)
                levels.Add(Mathf.Lerp(d.dryHeight, ship.SinkingHeight, i / 9f));
            var rings = new List<float>(levels);
            rings.Reverse();
            return rings.ToArray();
        }

        // On a flush deck, the forward and aft spaces start flooding at the same level.
        static float Spread(float from, float to, float height) => from == to
            ? (height >= to ? 1f : 0f)
            : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, height));

        public void SetLevel(float height)
        {
            var d = definition.Data;
            if (height <= d.dryHeight)
            {
                if (Visible) visual.SetActive(false);
                return;
            }
            if (Visible && (previousHeight == height ||
                (Mathf.Abs(previousHeight - height) < .003f && height < definition.SinkingHeight)))
                return;

            float lower = Spread(d.holdCeiling, d.lowerDeck, height);
            float main = Spread(d.lowerDeck, d.mainDeck, height);
            float aft = Mathf.Lerp(Mathf.Lerp(d.holdAft, d.lowerAft, lower), 0f, main);
            float fore = Mathf.Lerp(Mathf.Lerp(d.holdFore, d.lowerFore, lower), d.length, main);
            // At deck levels both surfaces terminate within the planking, on opposite
            // sides of its centre. This covers plank edges and the thin transom.
            float inset = d.liningInset * Mathf.Lerp(.445f / .36f, .10f / .36f, lower);
            for (int ring = 0; ring <= ringHeights.Length; ring++)
            {
                float y = ring == 0 ? height : Mathf.Min(height, ringHeights[ring - 1]);
                int start = ring * RingSize;
                if (ring > 0 && y == height)
                {
                    System.Array.Copy(vertices, 0, vertices, start, RingSize);
                    continue;
                }
                for (int i = 0; i <= Sections; i++)
                {
                    float z = Mathf.Lerp(aft, fore, (float)i / Sections);
                    float w = Mathf.Max(0f, definition.HalfBreadth(z, y) - inset);
                    int v = start + 2 * i;
                    vertices[v] = new Vector3(-w, y, z);
                    vertices[v + 1] = new Vector3(w, y, z);
                }
            }
            previousHeight = height;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            volume.SetVector("_HoldBounds", new Vector4(d.dryHeight, height, aft, fore));
            volume.SetFloat("_SideInset", inset);
            surface.SetFloat("_OpenDeck", Mathf.InverseLerp(d.upperDeck - .1f, d.upperDeck + .15f, height));
            if (!Visible) visual.SetActive(true);
        }

        public void Dispose() => HullFlooding.DisposeVisual(visual);
    }
}
