using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Galleon
{
    // Standalone probe for the shader/material variants and texture actually shipped in the player.
    public sealed class HullLiningCheck : MonoBehaviour
    {
        Camera view;
        GalleonRig rig;
        RenderTexture target;
        Texture2D readback;
        MeshRenderer[] walls;
        MaterialPropertyBlock[] previousBlocks;
        string directory;
        int failures;

        public void Begin(NavalGame game, string output)
        {
            directory = Path.GetFullPath(output);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "lining-check.txt"), "Brigantine R06 rendered hull lining" + Environment.NewLine);
            view = game.MainCamera;
            rig = game.Pirate.Rig;
            StartCoroutine(CheckPlayer(game));
        }

        IEnumerator CheckPlayer(NavalGame game)
        {
            var previousTarget = view.targetTexture;
            var previousPosition = view.transform.position;
            var previousRotation = view.transform.rotation;
            float previousFov = view.fieldOfView, previousScale = Time.timeScale;
            bool crewEnabled = game.Crew.enabled;
            game.Crew.enabled = false;
            Time.timeScale = 0f;
            var steps = Run();
            while (true)
            {
                try { if (!steps.MoveNext()) break; }
                catch (Exception e) { Check(false, e.ToString()); break; }
                yield return steps.Current;
            }
            if (previousBlocks != null)
                for (int i = 0; i < walls.Length; i++)
                    if (walls[i]) walls[i].SetPropertyBlock(previousBlocks[i]);
            view.targetTexture = previousTarget;
            view.fieldOfView = previousFov;
            view.transform.SetPositionAndRotation(previousPosition, previousRotation);
            Time.timeScale = previousScale;
            game.Crew.enabled = crewEnabled;
            if (target) { target.Release(); Destroy(target); }
            if (readback) Destroy(readback);
            Write("RESULT " + (failures == 0 ? "PASS" : "FAIL"));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(failures == 0 ? 0 : 3);
#endif
        }

        IEnumerator Run()
        {
            var source = Resources.Load<Texture2D>("Fleet/Brigantine/HullLining");
            var filters = Array.FindAll(rig.InnerPlanking, mesh => mesh.name.StartsWith("INNER_LINING", StringComparison.Ordinal));
            Check(rig.Definition.Type == ShipType.Brigantine && source && filters.Length > 0,
                $"imported R06 lining and reference texture: groups={filters.Length} asset={source}");
            if (!source || filters.Length == 0) yield break;
            walls = Array.ConvertAll(filters, mesh => mesh.GetComponent<MeshRenderer>());
            previousBlocks = new MaterialPropertyBlock[walls.Length];
            var lods = rig.GetComponent<LODGroup>().GetLODs();
            for (int i = 0; i < walls.Length; i++)
            {
                var wall = walls[i];
                var material = wall.sharedMaterial;
                previousBlocks[i] = new MaterialPropertyBlock();
                wall.GetPropertyBlock(previousBlocks[i]);
                var bound = material.GetTexture("_BaseMap");
                string descriptor = bound ? $"{bound.name} {bound.width}x{bound.height}" : "null";
                bool samplesPaint = material.IsKeywordEnabled("_SHIP_PAINT_MAP");
                Check(bound == source && samplesPaint, $"{wall.name} material={material.name} shader={material.shader.name} baseMap={descriptor} " +
                    $"source={source.name} {source.width}x{source.height} keyword={samplesPaint}");
                for (int j = 0; j < lods.Length; j++)
                    Check(Array.IndexOf(lods[j].renderers, wall) >= 0, $"LOD {j} includes {wall.name}");
            }
            target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            if (!target.Create()) throw new InvalidOperationException("Could not create the lining camera target.");
            readback = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            view.targetTexture = target;
            view.fieldOfView = 60f;

            var poses = new[] {
                ("upper-stairs", new Vector3(1.8f, 8.45f, 2.6f), new Vector3(.1f, 4.55f, 12.8f)),
                ("upper-port", new Vector3(.40f, 5.45f, 9.35f), new Vector3(-3.25f, 4.52f, 12.6f)),
                ("upper-starboard", new Vector3(-.40f, 5.45f, 9.35f), new Vector3(3.25f, 4.52f, 12.6f)),
                ("lower-port", new Vector3(-.7f, 2.90f, 11.2f), new Vector3(-3.2f, 2.65f, 15.0f)),
                ("lower-starboard", new Vector3(.7f, 2.90f, 11.2f), new Vector3(3.2f, 2.65f, 15.0f))
            };
            var control = new MaterialPropertyBlock();
            foreach (var (name, eye, aim) in poses)
            {
                var position = rig.transform.TransformPoint(eye);
                view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(rig.transform.TransformPoint(aim) - position, rig.transform.up));
                for (int i = 0; i < walls.Length; i++) walls[i].SetPropertyBlock(previousBlocks[i]);
                yield return null;
                yield return new WaitForEndOfFrame();
                var wood = Capture(name);
                control.SetTexture("_BaseMap", Texture2D.whiteTexture);
                foreach (var wall in walls) wall.SetPropertyBlock(control);
                yield return null;
                yield return new WaitForEndOfFrame();
                var white = Capture(null);
                control.SetTexture("_BaseMap", Texture2D.blackTexture);
                foreach (var wall in walls) wall.SetPropertyBlock(control);
                yield return null;
                yield return new WaitForEndOfFrame();
                Inspect(name, wood, white, Capture(null));
            }
        }

        Color32[] Capture(string name)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                readback.Apply(false);
                if (name != null) File.WriteAllBytes(Path.Combine(directory, name + ".png"), readback.EncodeToPNG());
                return readback.GetPixels32();
            }
            finally { RenderTexture.active = previous; }
        }

        void Inspect(string name, Color32[] wood, Color32[] white, Color32[] black)
        {
            // White and black overrides isolate pixels shaded by this exact renderer. Normalising to those
            // controls also removes most lighting variation from the measured plank colour and contrast.
            int pixels = 0, oak = 0;
            var histogram = new int[256];
            for (int i = 0; i < wood.Length; i++)
            {
                var w = white[i]; var b = black[i]; var actual = wood[i];
                if (Math.Min(w.r - b.r, Math.Min(w.g - b.g, w.b - b.b)) < 40) continue;
                float r = (actual.r - b.r) / (float)(w.r - b.r);
                float g = (actual.g - b.g) / (float)(w.g - b.g);
                float blue = (actual.b - b.b) / (float)(w.b - b.b);
                if (r > g + .035f && g > blue + .018f && blue < .68f) oak++;
                histogram[Mathf.RoundToInt(255f * Mathf.Clamp01((r + g + blue) / 3f))]++;
                pixels++;
            }
            int low = Percentile(histogram, pixels, .05f), high = Percentile(histogram, pixels, .95f);
            Check(pixels >= 500 && oak > pixels * .70f && high - low >= 8,
                $"{name} visible lining pixels={pixels}, oak={oak}, normalised plank tones P05/P95={low}/{high}");
        }

        static int Percentile(int[] histogram, int count, float fraction)
        {
            int remainder = Mathf.Max(1, Mathf.CeilToInt(count * fraction));
            for (int i = 0; i < histogram.Length; i++)
                if ((remainder -= histogram[i]) <= 0) return i;
            return 0;
        }

        void Check(bool ok, string details)
        {
            if (!ok) failures++;
            Write((ok ? "PASS " : "FAIL ") + details);
        }

        void Write(string line) => File.AppendAllText(Path.Combine(directory, "lining-check.txt"), line + Environment.NewLine);
    }
}
