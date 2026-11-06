using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace Emberfall
{
    // Opt-in, identical baseline/final workload. Never uses the ordinary player's save.
    public class PerformanceVerification : MonoBehaviour
    {
        static string output;
        readonly List<string> rows = new List<string>();
        Adventure game;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-emberfall-performance") < 0) return;
            int i = Array.IndexOf(args, "-performance-output");
            output = i >= 0 && i + 1 < args.Length ? args[i + 1] : Application.persistentDataPath + "/Performance";
            Directory.CreateDirectory(output);
            AdventureSave.VerificationPath = output + "/profile-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json";
            UnityEngine.Random.InitState(8600);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (output != null) new GameObject("Performance verification").AddComponent<PerformanceVerification>();
        }
        IEnumerator Start()
        {
            yield return null;
            game = Adventure.Instance;
            rows.Add("scenario,frames,median_ms,p95_ms,p99_ms,gc_bytes_per_frame,sprite_renderers");
            yield return new WaitForSeconds(1);
            yield return Sample("camp_idle", false);
            for (int i = 0; i < 16; i++) game.data.bag.Add(new Gear { name = "Tempered Sword", slot = i % 2, power = 5, vitality = 2, rarity = 1 });
            game.inventory = true;
            yield return new WaitForSeconds(.5f);
            yield return Sample("full_inventory", false);
            game.inventory = false;
            game.EnterZone(1);
            foreach (var f in game.enemies) Destroy(f.go);
            game.enemies.Clear();
            game.player.position = Vector2.zero;
            game.health = 100000;
            for (int i = 0; i < 30; i++)
            {
                float a = i * Mathf.PI * 2 / 30;
                var f = game.Spawn(i % 3, new Vector2(Mathf.Cos(a) * 5, Mathf.Sin(a) * 5));
                f.hp = f.max = 100000;
            }
            yield return new WaitForSeconds(.5f);
            yield return Sample("combat_30_foes", true);
            foreach (int zone in new[] { 0, 1, 2, 1, 0 })
            {
                var timer = Stopwatch.StartNew();
                game.EnterZone(zone);
                double synchronous = timer.Elapsed.TotalMilliseconds;
                yield return null;
                rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "zone_{0},1,{1:0.000},{2:0.000},0,0,{3}", zone, synchronous, timer.Elapsed.TotalMilliseconds,
                    FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length));
                yield return new WaitForSeconds(.3f);
            }
            File.WriteAllLines(output + "/performance.csv", rows);
            UnityEngine.Debug.Log("EMBERFALL_PERFORMANCE_OK " + output);
            GracefulExit.Request();
        }
        IEnumerator Sample(string scenario, bool combat)
        {
            const int count = 180;
            var timings = new List<double>(count);
            long allocated = 0;
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                var timer = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    if (combat) { game.mana = 100; game.UseAttack(i % 30 == 0 ? 1 : i % 40 == 0 ? 2 : i % 20 == 0 ? 3 : 0); }
                    double before = timer.Elapsed.TotalMilliseconds;
                    yield return null;
                    timings.Add(timer.Elapsed.TotalMilliseconds - before);
                    if (gc.Valid) allocated += gc.LastValue;
                }
                timings.Sort();
                rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1},{2:0.000},{3:0.000},{4:0.000},{5},{6}", scenario, count, timings[count / 2],
                    timings[(int)(count * .95f)], timings[(int)(count * .99f)], gc.Valid ? allocated / count : -1,
                    FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length));
            }
        }
    }
}
