using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galleon
{
    // Opt-in standalone GPU check. Keep the sailor on the quarterdeck so an avatar-driven
    // effect cannot pass. Viewpoints use the brigantine's lower battery, companionway and raised helm deck.
    public sealed class HoldWaterViewCheck : MonoBehaviour
    {
        readonly struct View
        {
            public readonly string Name;
            public readonly Vector3 Eye, Target;
            public View(string name, Vector3 eye, Vector3 target)
            {
                Name = name;
                Eye = eye;
                Target = target;
            }
        }

        // Look down the main companionway from its upper landing. The lower battery
        // has an aisle to starboard of the staircase, clear of the deck guns and masts.
        static readonly View Hatch = new("hatch", new Vector3(0f, 4.53f, 15.6f), new Vector3(0f, 2.1f, 12.3f));
        static readonly View Hold = new("underwater", new Vector3(.96f, 2.32f, 11.3f), new Vector3(.9f, 2.38f, 6.2f));
        static readonly View Up = new("looking-up", new Vector3(.95f, 2.35f, 11.3f), new Vector3(.95f, 3.75f, 10.6f));
        static readonly View DryDeck = new("raised-deck", new Vector3(.35f, 7.30f, 1.7f), new Vector3(.20f, 9.65f, 9.1f));
        static readonly View OtherShip = new("other-ship", Hold.Eye, Hold.Target);

        Camera cameraToCheck;
        NavalGame game;
        Transform hull;
        RenderTexture target, oldTarget;
        Texture2D readback;
        string directory, receipt;
        float oldCaptureDelta, oldTimeScale;
        bool oldCrewEnabled, oldPirateEnabled, oldNavyEnabled;
        AcceptanceCompanyFixture companyFixture;
        int passed, failed;

        public void Begin(NavalGame session, string output)
        {
            game = session;
            cameraToCheck = game.MainCamera;
            hull = game.Pirate.Rig.transform;
            directory = Path.GetFullPath(output);
            Directory.CreateDirectory(directory);
            receipt = Path.Combine(directory, "water-view-check.txt");
            File.WriteAllText(receipt, "Brigantine hold water GPU check | " + DateTime.UtcNow.ToString("O") + Environment.NewLine);
            oldTarget = cameraToCheck.targetTexture;
            oldCrewEnabled = game.Crew.enabled;
            oldPirateEnabled = game.Pirate.enabled;
            oldNavyEnabled = game.Navy.enabled;
            oldCaptureDelta = Time.captureDeltaTime;
            oldTimeScale = Time.timeScale;
            game.Crew.enabled = false;
            companyFixture = new AcceptanceCompanyFixture(game.Pirate, game.Navy);
            Write("FIXTURE water optics calibration holds the control vessel still and keeps the test breaches open.");
            Time.captureDeltaTime = 1f / 30f;
            StartCoroutine(CheckedRun());
        }

        IEnumerator CheckedRun()
        {
            IEnumerator body = Run();
            while (true)
            {
                try
                {
                    if (!body.MoveNext()) break;
                }
                catch (Exception e)
                {
                    Check(false, "render check threw", e.ToString());
                    break;
                }
                yield return body.Current;
            }
            cameraToCheck.targetTexture = oldTarget;
            game.Crew.enabled = oldCrewEnabled;
            game.Pirate.enabled = oldPirateEnabled;
            game.Navy.enabled = oldNavyEnabled;
            companyFixture.Dispose();
            Time.captureDeltaTime = oldCaptureDelta;
            Time.timeScale = oldTimeScale;
            if (target) { target.Release(); Destroy(target); }
            if (readback) Destroy(readback);
            Write($"RESULT {(failed == 0 ? "PASS" : "FAIL")} passed={passed} failed={failed}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(failed == 0 ? 0 : 3);
#endif
        }

        IEnumerator Run()
        {
            target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { name = "Hold water check camera target" };
            bool gpu = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && target.Create();
            Check(gpu, "GPU camera target", SystemInfo.graphicsDeviceName);
            if (!gpu) yield break;
            readback = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            cameraToCheck.targetTexture = target;
            cameraToCheck.fieldOfView = 63f;

            var flooding = game.Pirate.Flooding;
            var cameras = new[] { Hatch, Hold, Up, DryDeck, OtherShip };
            var dry = new Color32[cameras.Length][];
            Check(game.Pirate.Definition.Type == ShipType.Brigantine && game.Navy.Definition.Type == ShipType.Brigantine &&
                flooding.WaterLevel01 == 0f && !flooding.WaterVisible &&
                Vector3.Distance(game.Crew.transform.position, game.Pirate.Rig.Spawn.position) < .3f,
                "dry hold, crew on quarterdeck", "");
            // Hold ship motion and lighting steady across all image comparisons.
            game.Pirate.enabled = false;
            game.Navy.enabled = false;
            Time.timeScale = 0f;
            for (int i = 0; i < cameras.Length; i++)
            {
                Place(cameras[i], i == cameras.Length - 1 ? game.Navy.Rig.transform : hull);
                yield return null;
                yield return new WaitForEndOfFrame();
                dry[i] = Capture("dry-" + cameras[i].Name);
            }
            bool rendered = SignalRange(dry[1]) > 10;
            Check(rendered, "main camera renders a textured dry control", $"luminance range={SignalRange(dry[1])}");
            if (!rendered) yield break;
            game.StartDamageDrill();
            Time.timeScale = 8f;
            float testHeight = Mathf.Lerp(game.Pirate.Definition.Data.lowerDeck, game.Pirate.Definition.Data.mainDeck, .60f);
            for (int frame = 0; flooding.WaterHeight < testHeight && frame < 350; frame++) yield return null;
            // Inspect the live flash at arm's length while both the camera and patch are submerged.
            Transform patch = game.Pirate.Rig.LeakInside[0];
            Vector3 inward = Vector3.ProjectOnPlane(patch.forward, hull.up).normalized;
            Vector3 eye = patch.position + inward * .95f + hull.up * .12f;
            cameraToCheck.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(patch.position - eye, hull.up));
            Time.timeScale = 1f;
            bool sawLit = false, sawUnlit = false;
            int redOn = 0, redOff = 0;
            for (int frame = 0; frame < 60 && !(sawLit && sawUnlit); frame++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                bool lit = flooding.IsSignalLit(0);
                if ((lit && sawLit) || (!lit && sawUnlit)) continue;
                var pixels = Capture(lit ? "submerged-breach-lit" : "submerged-breach-unlit");
                int red = 0;
                for (int pixel = 0; pixel < pixels.Length; pixel += 5)
                {
                    Color32 c = pixels[pixel];
                    if (c.r > 95 && c.r > c.g*2 && c.r > c.b*2) red++;
                }
                if (lit) { sawLit = true; redOn = red; }
                else { sawUnlit = true; redOff = red; }
            }
            Check(sawLit && sawUnlit && redOn > redOff + 40, "nearby submerged breach still visibly flashes red",
                $"red samples lit={redOn}, unlit={redOff}");
            // End inflow at the attained height. This also removes flashing decals that would
            // invalidate same-frame colour comparisons or obscure the dry-deck control.
            for (int i = 0; i < game.Pirate.Rig.LeakInside.Length; i++) flooding.Repair(i);
            Time.timeScale = 0f;
            Check(flooding.WaterVisible && flooding.WaterHeight >= testHeight && flooding.WaterHeight < game.Pirate.Definition.Data.mainDeck && flooding.ActiveLeaks == 0,
                "training damage filled the hold and repairs retained its water", $"height={flooding.WaterHeight:F3} m");
            if (!flooding.WaterVisible) yield break;

            for (int i = 0; i < cameras.Length; i++)
            {
                View view = cameras[i];
                Place(view, i == cameras.Length - 1 ? game.Navy.Rig.transform : hull);
                yield return null;
                yield return new WaitForEndOfFrame();
                var wet = Capture("flooded-" + view.Name);
                // Use a centred inset. The hatch framing leaves the dry deck visible at its edges.
                int inset = i == 0 ? 34 : 12;
                float shift = ColorDelta(dry[i], wet, inset);
                if (i < 3)
                {
                    float cool = CoolShift(dry[i], wet, inset);
                    Check(shift > 8f && cool > 8f, view.Name + " visibly differs from dry wood",
                        $"mean RGB difference={shift:F1}, gain in blue vs. red={cool:F1}");
                    if (i == 1) Check(SignalRange(wet) > 10, "underwater hold retains visible detail", $"luminance range={SignalRange(wet)}");
                }
                else Check(shift < 5f, view.Name + " stays dry while the player battery floods", $"mean RGB difference={shift:F2}");
            }
            Place(Hold, hull);
            yield return null;
            yield return new WaitForEndOfFrame();
            flooding.Pump(20f);
            yield return null;
            yield return new WaitForEndOfFrame();
            var drained = Capture("drained-underwater-view");
            float residual = ColorDelta(dry[1], drained, 12);
            Check(flooding.WaterLevel01 == 0 && !flooding.WaterVisible && residual < 5f,
                "a stationary camera recovers its dry view after pumping", $"mean RGB difference={residual:F2}");
        }

        void Place(View view, Transform reference)
        {
            Vector3 eye = reference.TransformPoint(view.Eye);
            Vector3 aim = reference.TransformPoint(view.Target);
            cameraToCheck.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye, reference.up));
        }

        Color32[] Capture(string name)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                readback.Apply(false);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), readback.EncodeToPNG());
                return readback.GetPixels32();
            }
            finally { RenderTexture.active = previous; }
        }

        float ColorDelta(Color32[] a, Color32[] b, int inset) => Difference(a, b, inset, false);
        float CoolShift(Color32[] a, Color32[] b, int inset) => Difference(a, b, inset, true);

        float Difference(Color32[] dry, Color32[] wet, int inset, bool blueShift)
        {
            int xPad = target.width * inset / 100, yPad = target.height * inset / 100;
            double total = 0;
            int samples = 0;
            for (int y = yPad; y < target.height - yPad; y += 3)
            for (int x = xPad; x < target.width - xPad; x += 3)
            {
                int index = y * target.width + x;
                Color32 a = dry[index], b = wet[index];
                total += blueShift ? (b.b - b.r) - (a.b - a.r)
                    : (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b)) / 3.0;
                samples++;
            }
            return (float)(total / samples);
        }

        static int SignalRange(Color32[] frame)
        {
            int[] histogram = new int[256];
            for (int i = 0; i < frame.Length; i += 8)
            {
                Color32 c = frame[i];
                histogram[(54 * c.r + 183 * c.g + 19 * c.b) / 256]++;
            }
            int count = (frame.Length + 7) / 8, cumulative = 0, low = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                cumulative += histogram[i];
                if (cumulative < count * .05f) low = i + 1;
                if (cumulative >= count * .95f) return i - low;
            }
            return 0;
        }

        void Check(bool ok, string name, string details)
        {
            if (ok) passed++; else failed++;
            Write((ok ? "PASS " : "FAIL ") + name + (details.Length == 0 ? "" : " | " + details));
        }

        void Write(string line)
        {
            File.AppendAllText(receipt, line + Environment.NewLine);
            Debug.Log("[HoldWaterViewCheck] " + line);
        }
    }
}
