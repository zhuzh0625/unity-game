using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Galleon
{
    public sealed class NavalAudioCheck : MonoBehaviour
    {
        const float Step = 1f / 60f;
        const string Sea = "01-sea-loop", Gulls = "02-seagulls", Cannon = "03-cannon-fire";
        const string HullHit = "04-cannonball-hull-hit", Collision = "05-ship-collision";
        const string Pistol = "06-flintlock-pistol", Cutlass = "07-cutlass-swing";
        const string Medicine = "08-drink-medicine", Repair = "09-hull-repair", Splash = "10-cannon-splash";
        static readonly int PlayVoiceSeed = Enumerable.Range(0, 1000).First(seed => NextVoiceRoll(seed) == 0);
        static readonly int SkipVoiceSeed = Enumerable.Range(0, 1000).First(seed => NextVoiceRoll(seed) == 1);
        static readonly int OtherSkipVoiceSeed = Enumerable.Range(0, 1000).First(seed => NextVoiceRoll(seed) == 2);
        static readonly int PlayThenSkipSeed = Enumerable.Range(0, 10000).First(seed =>
        {
            var random = new System.Random(seed);
            return random.Next(3) == 0 && Enumerable.Range(0, 6).All(_ => random.Next(3) != 0);
        });
        readonly float[] outputSamples = new float[1024];
        NavalGame game;
        NavalAudio voyageAudio;
        string receipt;
        int passed;

        public void Begin(NavalGame session, string directory)
        {
            game = session;
            Directory.CreateDirectory(directory);
            receipt = Path.Combine(directory, "audio-check.txt");
            File.WriteAllText(receipt, "Naval gameplay audio | " + DateTime.UtcNow.ToString("O") + Environment.NewLine);
            StartCoroutine(Guard());
        }

        IEnumerator Guard()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            Exception failure = null;
            while (stack.Count > 0)
            {
                object next;
                try
                {
                    var current = stack.Peek();
                    if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    next = current.Current;
                }
                catch (Exception error) { failure = error; break; }
                if (next is IEnumerator child) stack.Push(child);
                else yield return next;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (failure != null) Note("FAIL " + failure);
            Note($"RESULT {(failure == null ? "PASS" : "FAIL")} passed={passed} failed={(failure == null ? 0 : 1)}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(failure == null ? 0 : 3);
#endif
        }

        IEnumerator Run()
        {
            yield return null;
            voyageAudio = game.GetComponent<NavalAudio>();
            Require(voyageAudio && game.IsInMenu && Sources().All(source => !source.isPlaying),
                "fleet menu begins without playing voyage audio");
            CheckResources();
            int sourceCount = Sources().Length;

            foreach (ShipType type in Enum.GetValues(typeof(ShipType)))
            {
                game.SelectShip(type);
                game.StartVoyage();
                var player = game.Pirate;
                var navy = game.Navy;
                game.Crew.ReadDeviceInput = false;
                using (new AcceptanceCompanyFixture(player, navy))
                {
                    var playerVoices = Roster(player);
                    var navyVoices = Roster(navy);
                    var combined = playerVoices.Values.Concat(navyVoices.Values).GroupBy(index => index)
                        .Select(group => group.Count()).ToArray();
                    Require(combined.Length == 3 && combined.Max() - combined.Min() <= 1,
                        $"{type} combined roster was balanced across all three voices");
                    yield return WaitFor(() => Playing(Sea), "voyage started the sea loop");
                    Require(Playing(Sea).loop && Playing(Sea).spatialBlend == 0f && !Playing(Gulls),
                        "nonpositional sea loops and gulls do not start immediately");
                    if (type == ShipType.Sloop)
                    {
                        yield return CheckPauseAndGulls();
                        yield return CheckEnemyCannon(navy, player);
                        yield return CheckCannon(player, navy);
                        yield return CheckCollision(player, navy);
                    }
                    if (type == ShipType.Brigantine)
                    {
                        yield return CheckRepair(player);
                        yield return CheckDeathVoices(player);
                    }

                    Require(playerVoices.Concat(navyVoices).All(entry => entry.Key.VoiceIndex == entry.Value),
                        $"{type} retained each actor's assigned voice during play");
                }
                game.ReturnToMenu();
                yield return WaitFor(() => Sources().All(source => !source.isPlaying),
                    $"{type} return to menu stopped voyage sources");
            }
            yield return CheckCrewActions();
            Require(Sources().Length == sourceCount, "returning after repeated voyages and actions left the original source pool");
        }

        void CheckResources()
        {
            string[] effects = { Sea, Gulls, Cannon, HullHit, Collision, Pistol, Cutlass, Medicine, Repair, Splash };
            foreach (string effect in effects) CheckClip(effect, effect == Sea || effect == Gulls);
            foreach (string category in new[] { "attack", "hit", "kill", "miss", "death" })
                for (int voice = 1; voice <= 3; voice++) CheckClip($"voices/{category}-{voice}", false);
            Require(FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Count(listener => listener.enabled) == 1,
                "there is one active AudioListener");
        }

        void CheckClip(string name, bool ambience)
        {
            var clip = Resources.Load<AudioClip>("NavalAudio/" + name);
            Require(clip && clip.samples > 0 && clip.frequency == 48000 && clip.channels == (ambience ? 2 : 1),
                $"packaged {name} is a nonempty 48 kHz {(ambience ? "stereo" : "mono")} clip");
        }

        Dictionary<CrewDamageTarget, int> Roster(NavalShip ship)
        {
            var roster = ship.Company.Mates.Cast<CrewDamageTarget>().Append(ship.Company.Captain).Distinct()
                .ToDictionary(crew => crew, crew => crew.VoiceIndex);
            var counts = Enumerable.Range(0, 3).Select(index => roster.Values.Count(value => value == index)).ToArray();
            Require(roster.Count == ship.Company.Capacity && counts.Sum() == roster.Count && counts.Max() - counts.Min() <= 1,
                $"{ship.Definition.Type} {(ship.IsPirate ? "pirate" : "navy")} roster voice counts {string.Join("/", counts)}");
            return roster;
        }

        IEnumerator CheckPauseAndGulls()
        {
            var sea = Playing(Sea);
            yield return WaitFor(() =>
            {
                AudioListener.GetOutputData(outputSamples, 0);
                return sea.timeSamples > 512 && outputSamples.Any(sample => Mathf.Abs(sample) > .00001f);
            }, "loop advanced and produced nonzero listener output");
            game.SetPaused(true);
            yield return new WaitForSecondsRealtime(.15f);
            int pausedSample = sea.timeSamples;
            // Wait beyond the scheduled first gull. The timer must use active voyage time.
            yield return new WaitForSecondsRealtime(3.1f);
            Require(Time.timeScale == 0f && !sea.isPlaying && pausedSample > 0 &&
                    Mathf.Abs(sea.timeSamples - pausedSample) <= 512 && !Playing(Gulls),
                "help pause stopped the sea in place and did not spend the gull timer");
            game.SetPaused(false);
            yield return WaitFor(() => Playing(Sea) == sea && sea.timeSamples > pausedSample + 512,
                "unpausing continued the same sea source and playhead");
            Require(!Playing(Gulls), "pausing did not make the first gull immediately due");
            yield return WaitFor(() => Playing(Gulls), "gulls played after resuming the active voyage timer", 5f);
            Require(!Playing(Gulls).loop, "gull calls are an intermittent clip");
        }

        static int NextVoiceRoll(int seed) => new System.Random(seed).Next(3);

        void SeedVoice(bool plays) => voyageAudio.SetVoiceSeed(plays ? PlayVoiceSeed : SkipVoiceSeed);

        int PrepareCannon(NavalShip source, NavalShip target)
        {
            source.enabled = target.enabled = false;
            source.transform.SetPositionAndRotation(new Vector3(25f, -source.Draft, 0f), Quaternion.identity);
            target.transform.SetPositionAndRotation(new Vector3(0f, -target.Draft, 0f), Quaternion.identity);
            foreach (var sailor in target.Company.Mates)
                sailor.transform.position += Vector3.up * 20f;
            if (target.Company.Captain is CrewPlayer captain)
            {
                var motor = captain.GetComponent<CharacterController>();
                motor.enabled = false;
                captain.transform.position += Vector3.up * 20f;
                motor.enabled = true;
            }
            Physics.SyncTransforms();
            int index = Array.FindIndex(source.Rig.Guns, gun =>
                Vector3.Dot(NavalShip.ShotDirection(gun, 0f, 0f), Vector3.left) > .98f);
            Require(index >= 0, "sloop has a port gun for the controlled broadside");
            return index;
        }

        static void AimAtSloopHull(RoundShot shot, NavalShip target, Vector3 heading)
        {
            // Reuse the measured sloop waterline impact from the cannon acceptance, preserving velocity and attribution.
            Vector3 hullPoint = target.transform.TransformPoint(new Vector3(2.228373f, 1.65000045f, 6.64999962f));
            shot.transform.position = hullPoint - heading * .20f;
        }

        IEnumerator CheckEnemyCannon(NavalShip enemy, NavalShip home)
        {
            int index = PrepareCannon(enemy, home);
            var gunner = enemy.Company.Mates.First(mate => mate.Duty == CrewDuty.Gunner);
            Vector3 heading = NavalShip.ShotDirection(enemy.Rig.Guns[index], 0f, 0f);
            // The next eligible friendly request must still have the only winning draw in this short sequence.
            voyageAudio.SetVoiceSeed(PlayThenSkipSeed);
            var shot = Launch(enemy, index, gunner);
            Require(!Playing(Cannon) && !AnyVoice("attack"), "enemy firing created a projectile with no muzzle sound or attack voice");
            AimAtSloopHull(shot, home, heading);
            yield return WaitFor(() => !shot, "enemy projectile resolved against the player's hull");
            Require(enemy.HitsLanded == 1 && home.Flooding.ActiveLeaks > 0 && Playing(HullHit) && !AnyVoice("hit"),
                "enemy shot damaged the player's hull and played only the local impact");
            shot = Launch(enemy, index, gunner, -14f);
            shot.transform.position = new Vector3(12f, 1f, 35f);
            yield return WaitFor(() => !shot, "enemy round crossed the open sea and expired", 3f);
            Require(!Playing(Cannon) && !Playing(Splash) && !AnyVoice("attack") && !AnyVoice("miss"),
                "enemy muzzle, open-sea splash and gunner calls stayed silent");
            yield return WaitFor(() => !Playing(HullHit), "the local impact finished before testing a strike on the enemy hull", 3f);
        }

        IEnumerator CheckCannon(NavalShip source, NavalShip target)
        {
            int index = PrepareCannon(source, target);
            var gunner = source.Company.Mates.First(mate => mate.Duty == CrewDuty.Gunner);
            Vector3 heading = NavalShip.ShotDirection(source.Rig.Guns[index], 0f, 0f);
            // In each case the projectile goes through production physics; seed only selects voice outcomes.
            foreach (bool first in new[] { true, false })
            {
                if (!first) SeedVoice(false);
                var shot = Launch(source, index, gunner);
                CheckVoiceRoll("attack", gunner, first);
                if (first)
                    Require(Playing(Voice("attack", gunner)).spatialBlend > .9f,
                        "the first friendly voice used the unchanged draw after the enemy actions and was positional");
                // The first hit takes the next draw from the same sequence as the preceding attack.
                if (!first) SeedVoice(true);
                int previousHits = source.HitsLanded;
                AimAtSloopHull(shot, target, heading);
                yield return WaitFor(() => !shot, "friendly projectile resolved against the enemy hull");
                Require(source.HitsLanded == previousHits + 1 && !Playing(HullHit),
                    "the enemy hull took a real hit without playing its impact");
                CheckVoiceRoll("hit", gunner, !first);
                if (first)
                    yield return WaitFor(() => !AnyVoice("attack"), "first attack line cleared before comparing the skipped roll", 4f);
            }

            var victims = target.Company.Mates.Take(2).ToArray();
            for (int i = 0; i < victims.Length; i++)
            {
                var victim = victims[i];
                Vector3 cluster = new Vector3(12f, 10f, 8f);
                victim.transform.position = cluster;
                Physics.SyncTransforms();
                SeedVoice(false);
                var shot = Launch(source, index, gunner);
                // Enemy death is processed before the local gunner outcome and must not spend its draw.
                voyageAudio.SetVoiceSeed(i == 1 ? PlayThenSkipSeed : OtherSkipVoiceSeed);
                shot.transform.position = cluster + Vector3.up * .90f - heading * .87f;
                yield return WaitFor(() => !shot && victim.Health == 0, "friendly cannon killed its real enemy target");
                Require(!AnyVoice("death"), "enemy crew death did not reach the player");
                CheckVoiceRoll("kill", gunner, i == 1);
            }

            yield return WaitFor(() => !AnyVoice("kill"), "kill line ended before the next gunner call", 4f);
            foreach (bool speaks in new[] { false, true })
            {
                SeedVoice(false);
                var shot = Launch(source, index, gunner, -14f);
                SeedVoice(speaks);
                // Outside both hulls, cross the real wave surface before the round expires underwater.
                shot.transform.position = new Vector3(12f, 1f, 35f);
                yield return WaitFor(() => Playing(Splash), "friendly cannonball crossed the water and played splash");
                yield return WaitFor(() => !shot, "friendly round expired without a hit", 3f);
                CheckVoiceRoll("miss", gunner, speaks);
                if (!speaks)
                    yield return WaitFor(() => !Playing(Splash), "first splash ended before verifying another", 4f);
            }
        }

        RoundShot Launch(NavalShip ship, int index, CrewDamageTarget gunner, float pitch = 0f)
        {
            var previouslyPlaying = Sources().Where(source => source.isPlaying && source.clip &&
                source.clip.name == Cannon).ToArray();
            ship.Load(index);
            Require(ship.Fire(index, 0f, pitch, gunner), "production broadside accepted the loaded shot");
            if (ship.IsPirate)
                Require(Sources().Any(source => source.isPlaying && source.clip && source.clip.name == Cannon &&
                        source.spatialBlend > .9f && !previouslyPlaying.Contains(source)),
                    "friendly muzzle always started a fresh positional effect, regardless of the voice draw");
            return FindObjectsByType<RoundShot>(FindObjectsSortMode.None).Single();
        }

        IEnumerator CheckCollision(NavalShip player, NavalShip navy)
        {
            var a = HullFootprint.For(player.Rig);
            var b = HullFootprint.For(navy.Rig);
            float close = 0f, separate = a.Radius + b.Radius;
            for (int i = 0; i < 20; i++)
            {
                float midpoint = (close + separate) * .5f;
                if (a.Overlap(Vector2.zero, 0f, b, Vector2.up * midpoint, 180f, out _)) close = midpoint;
                else separate = midpoint;
            }
            player.PlaceAtSea(Vector3.zero, 0f);
            navy.PlaceAtSea(Vector3.forward * (separate + 4f), 180f);
            player.SetSailStep(2);
            navy.SetSailStep(2);
            player.enabled = navy.enabled = true;
            yield return WaitFor(() => Playing(Collision), "two moving bows met and played the hull collision", 7f);
        }

        IEnumerator CheckRepair(NavalShip ship)
        {
            const int index = 0;
            SeedVoice(false);
            game.StartDamageDrill();
            var crew = game.Crew;
            var patch = ship.Rig.LeakInside[index];
            Vector3 inward = Vector3.ProjectOnPlane(patch.forward, ship.transform.up).normalized;
            Vector3 foot = ship.transform.InverseTransformPoint(patch.position + inward * .70f);
            foot.y = ship.Definition.Data.lowerDeck + .026f;
            var motor = crew.GetComponent<CharacterController>();
            motor.enabled = false;
            crew.transform.position = ship.transform.TransformPoint(foot);
            motor.enabled = true;
            for (int i = 0; i < 3; i++) { Tick(); yield return null; }
            Vector3 current = ship.transform.InverseTransformDirection(game.MainCamera.transform.forward);
            Vector3 toward = ship.transform.InverseTransformDirection(patch.position - crew.transform.position);
            float fromYaw = Mathf.Atan2(current.x, current.z) * Mathf.Rad2Deg;
            float toYaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(current.y, -1f, 1f)) * Mathf.Rad2Deg;
            Tick(new CrewCommand { Look = new Vector2(Mathf.DeltaAngle(fromYaw, toYaw) / .115f, (pitch - 28f) / .10f) });
            for (int i = 0; i < 5; i++) { Tick(); yield return null; }
            Tick(new CrewCommand { UsePressed = true });
            Require(crew.StationName == "船壳破口" && ship.Flooding.IsLeaking(index), "real use input occupied the active leak");
            for (int i = 0; i < 4; i++) { Tick(); yield return null; }
            yield return WaitFor(() => Playing(Repair), "active old-hull repair started the repair source");
            Tick(new CrewCommand { UsePressed = true });
            yield return WaitFor(() => !Playing(Repair), "leaving the repair post stopped the sound");
            Require(ship.Flooding.IsLeaking(index), "cancelled repair left the leak open");
        }

        IEnumerator CheckDeathVoices(NavalShip ship)
        {
            var silent = ship.Company.Mates.GroupBy(mate => mate.VoiceIndex).First(group => group.Count() > 1).Last();
            SeedVoice(false);
            silent.TakeDamage(silent.Health);
            Require(silent.Health == 0, "crew died even on the skipped death draw");
            CheckVoiceRoll("death", silent, false);
            foreach (var sailor in ship.Company.Mates.Where(mate => mate.Health > 0)
                         .GroupBy(mate => mate.VoiceIndex).Select(group => group.First()))
            {
                SeedVoice(true);
                sailor.TakeDamage(sailor.Health);
                var source = Playing(Voice("death", sailor));
                Require(sailor.Health == 0 && source, $"voice {sailor.VoiceIndex + 1} played its own death clip");
                yield return WaitFor(() =>
                {
                    source.GetOutputData(outputSamples, 0);
                    return outputSamples.Any(value => Mathf.Abs(value) > .0001f);
                }, $"death voice {sailor.VoiceIndex + 1} produced audio samples");
            }
        }

        IEnumerator CheckCrewActions()
        {
            game.StartCombatTest();
            var crew = game.Crew;
            crew.ReadDeviceInput = false;
            int voice = crew.VoiceIndex;
            Require(voice >= 0 && voice < 3 && game.TestDummies.All(dummy => dummy.VoiceIndex == -1),
                "training assigned a real captain voice and left the dummies voiceless");
            SeedVoice(false);
            Tick(new CrewCommand { WeaponKey = CrewWeapon.Pistol });
            Tick(new CrewCommand { AimPressed = true });
            Tick(new CrewCommand { AttackPressed = true });
            Require(crew.PistolCooldownRemaining > 0f && Playing(Pistol), "pistol trigger played the muzzle effect");

            Tick(new CrewCommand { WeaponKey = CrewWeapon.Cutlass });
            Tick(new CrewCommand { AttackPressed = true });
            Require(crew.IsAttacking, "cutlass command started a swing");
            for (float elapsed = 0f; crew.IsAttacking && !Playing(Cutlass) && elapsed < 3f; elapsed += Step)
            {
                Tick();
                yield return null;
            }
            Require(Playing(Cutlass), "the animated cutlass contact played the sword effect");
            crew.TakeDamage(75);
            SeedVoice(false);
            Tick(new CrewCommand { WeaponKey = CrewWeapon.Medicine });
            Tick(new CrewCommand { AttackPressed = true });
            Require(crew.IsDrinkingMedicine && Playing(Medicine), "drinking actually started the medicine source");
            var drink = Playing(Medicine);
            game.SetPaused(true);
            yield return new WaitForSecondsRealtime(.15f);
            Require(crew.IsDrinkingMedicine && !drink.isPlaying, "pausing the voyage suspended the ongoing drink sound");
            game.SetPaused(false);
            yield return WaitFor(() => Playing(Medicine) == drink, "resuming the voyage resumed its drink source");
            Tick(new CrewCommand { WeaponKey = CrewWeapon.Cutlass });
            yield return WaitFor(() => !Playing(Medicine), "switching weapons stopped the unfinished drink");
            Require(crew.Health == 25, "stopped drink did not heal");
            SeedVoice(true);
            crew.TakeDamage(crew.Health);
            Require(crew.IsDead && Playing(Voice("death", crew)), "captain death played his assigned voice");
            yield return WaitFor(() => !crew.IsDead && crew.Health == 100, "captain respawned through the gameplay timer", 14f);
            Require(crew.VoiceIndex == voice, "respawn held the exact same captain voice assignment");
            game.ReturnToMenu();
            yield return WaitFor(() => Sources().All(source => !source.isPlaying),
                "training return to menu removed every remaining source from playback");
        }

        void Tick(CrewCommand command = default) => game.Crew.Tick(command, Step);
        static string Voice(string category, CrewDamageTarget actor) => $"{category}-{actor.VoiceIndex + 1}";
        AudioSource[] Sources() => voyageAudio.GetComponentsInChildren<AudioSource>(true);
        AudioSource Playing(string clipName) =>
            Sources().FirstOrDefault(source => source.isPlaying && source.clip && source.clip.name == clipName);
        bool AnyVoice(string category) => Sources().Any(source => source.isPlaying && source.clip &&
            source.clip.name.StartsWith(category + "-", StringComparison.Ordinal));

        void CheckVoiceRoll(string category, CrewDamageTarget actor, bool plays)
        {
            bool correctClip = Playing(Voice(category, actor));
            Require(plays ? correctClip : !AnyVoice(category),
                $"{category} {(plays ? "winning draw played the actor's assigned voice" : "losing draw stayed silent")}");
        }

        IEnumerator WaitFor(Func<bool> condition, string message, float seconds = 1.5f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Require(condition(), message);
        }

        void Require(bool condition, string message)
        {
            if (!condition)
            {
                string active = voyageAudio ? string.Join(", ", Sources().Where(source => source.isPlaying)
                    .Select(source => source.clip ? source.clip.name : source.name)) : "";
                throw new InvalidOperationException($"{message}; playing=[{active}]");
            }
            passed++;
            Note("PASS " + message);
        }

        void Note(string message)
        {
            File.AppendAllText(receipt, message + Environment.NewLine);
            Debug.Log("[NavalAudioCheck] " + message);
        }
    }
}
