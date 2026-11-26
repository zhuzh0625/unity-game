using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        const float FloodedFireStep = 1f / 60f;
        const float OriginalShotCutoff = -.5f;
        const float FloodedFireSampleX = 31f;
        const BindingFlags FloodedFirePrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        public static void BeginFloodedFire(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(session, directory, "flooded-fire-check", "Flooded target cannon fire", check.RunFloodedFire());
        }

        IEnumerator RunFloodedFire()
        {
            Time.captureDeltaTime = FloodedFireStep;
            yield return null;
            foreach (var choice in new[] { ShipType.Brigantine, ShipType.Sloop })
            {
                type = choice;
                game.SelectShip(type);
                game.StartVoyage();
                var target = game.Navy;
                Require(!game.IsCombatTest && Ship && Ship.Definition.Type == type && target &&
                    target.Definition.Type == type && Crew, $"selected voyage loaded with two opposing {type} ships");
                Crew.ReadDeviceInput = false;
                Ship.enabled = target.enabled = target.Flooding.enabled = false;
                Ship.transform.SetPositionAndRotation(new Vector3(0f, -Ship.Draft, 0f), Quaternion.identity);
                // Both hulls stay on their measured draft. Disable crew interception so the test
                // measures the shell and the battery without casualties interrupting reloads.
                foreach (var sailor in FindObjectsByType<CrewDamageTarget>(FindObjectsSortMode.None))
                    foreach (var body in sailor.GetComponentsInChildren<Collider>())
                        if (body.isTrigger) body.enabled = false;
                Note($"FIXTURE {choice} fires at a stationary {target.Definition.Type}. Starboard broadside, 40m across, 1.4m longitudinal offset; no pumping or new flooding during each case.");

                using (new AcceptanceCompanyFixture(target))
                {
                    if (type == ShipType.Brigantine)
                    {
                        using (new AcceptanceCompanyFixture(Ship))
                        {
                            foreach (float fill in new[] { .90f, .98f })
                            {
                                SetFloodedFireTarget(target, fill);
                                yield return FloodedFireLowShot(target);
                                for (int i = 0; i < target.Rig.LeakInside.Length; i++)
                                    target.Flooding.Repair(i);
                            }
                        }
                    }

                    SetFloodedFireTarget(target, .90f);
                    yield return SampleFloodedFire(target, rotate: true);

                    if (type == ShipType.Brigantine)
                    {
                        using (new AcceptanceCompanyFixture(Ship))
                            yield return FloodedFireMiss(target);
                    }

                    for (int i = 0; i < target.Rig.LeakInside.Length; i++)
                        target.Flooding.Repair(i);
                    SetFloodedFireTarget(target, 0f);
                    yield return SampleFloodedFire(target, rotate: false);
                    yield return CheckCannonRange(target);
                }
                game.ReturnToMenu();
                yield return null;
            }
        }

        static void SetFloodedFireTarget(NavalShip target, float fill)
        {
            var flooding = target.Flooding;
            var setWater = typeof(HullFlooding).GetMethod("SetWater", FloodedFirePrivateInstance);
            setWater.Invoke(flooding, new object[] { Mathf.Lerp(flooding.DryHeight, flooding.SinkingHeight, fill) });
            target.transform.SetPositionAndRotation(new Vector3(40f, -target.Draft, 1.4f), Quaternion.identity);
            Physics.SyncTransforms();
        }

        IEnumerator FloodedFireLowShot(NavalShip target)
        {
            // This gun and the old low aim point reproduce the 40m stock brigantine miss.
            var guns = Ship.Rig.Guns;
            var gun = guns.Where(g => g.forward.x > .8f)
                .OrderBy(g => Mathf.Abs(g.localPosition.z - 12.60f) + Mathf.Abs(g.localPosition.y - 2.53f)).First();
            Vector3 local = target.transform.InverseTransformPoint(gun.position);
            Vector3 bore = target.transform.InverseTransformDirection(gun.forward);
            float length = target.Definition.Length;
            float z = Mathf.Clamp(local.z - bore.z * local.x / bore.x, .18f * length, .82f * length);
            float y = target.Definition.DryDraft + .55f;
            float skin = target.Definition.HalfBreadth(z, y);
            Vector3 offset = target.transform.TransformPoint(-skin, y, z) - gun.position;
            float travel = Mathf.Max(0f, offset.magnitude - NavalShip.ShotSpawnOffset) / NavalShip.ShotSpeed;
            offset.y += .5f * NavalShip.ShotDrop * travel * travel;
            Vector3 direction = gun.InverseTransformDirection(offset);
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;

            var water = target.Flooding;
            int hits = Ship.HitsLanded;
            float deepest = 0f;
            Require(!water.IsSunk && Ship.Anchored && target.Anchored && water.ActiveLeaks == 0 &&
                Vector3.Dot(target.transform.position - Ship.transform.position, Ship.transform.right) == 40f,
                $"independent impact at {water.WaterLevel01:P0} fill with no previous breach");
            var shot = LaunchFloodedFireShot(Array.IndexOf(guns, gun), yaw, pitch);
            for (int frame = 0; shot && frame < 120; frame++)
            {
                deepest = Mathf.Min(deepest, shot.transform.position.y);
                yield return null;
            }
            Require(!shot && deepest < OriginalShotCutoff - .10f && Ship.HitsLanded - hits == 1 &&
                water.ActiveLeaks == 1 && water.ActiveDecals == 2 && !water.IsSunk,
                $"low shot at {water.WaterLevel01:P0} fill stays alive down to {deepest:F2}m, lands {Ship.HitsLanded - hits} hull hit and opens {water.ActiveLeaks} new breach");
        }

        RoundShot LaunchFloodedFireShot(int index, float yaw, float pitch)
        {
            Require(!FindAnyObjectByType<RoundShot>(), "isolated gun has a clear shot scene");
            Ship.Load(index);
            Require(Ship.Fire(index, yaw, pitch), $"cannon {index} launches a real shot at yaw={yaw:F1}, pitch={pitch:F1}");
            var shot = FindAnyObjectByType<RoundShot>();
            Require(shot, "launched projectile is present in the scene");
            return shot;
        }

        IEnumerator SampleFloodedFire(NavalShip target, bool rotate)
        {
            float expectedFill = rotate ? .90f : 0f;
            Require(!FindAnyObjectByType<RoundShot>() && !target.Flooding.IsSunk && target.Flooding.ActiveLeaks == 0 &&
                Mathf.Abs(target.Flooding.WaterLevel01 - expectedFill) < .001f,
                $"AI fire starts with a clear shot scene and no existing breaches at {expectedFill:P0} fill");
            var company = Ship.Company;
            var gunnerField = typeof(RoundShot).GetField("gunner", FloodedFirePrivateInstance);
            var sourceField = typeof(RoundShot).GetField("source", FloodedFirePrivateInstance);
            var guns = Ship.Rig.Guns;
            int[] startingShots = Enumerable.Range(0, guns.Length).Select(company.ShotsFiredByGun).ToArray();
            int[] gunShots = (int[])startingShots.Clone();
            int initialCount = company.ShotsFired;
            int seenCount = initialCount;
            int hits = Ship.HitsLanded;
            int leaks = target.Flooding.ActiveLeaks;
            var seen = new HashSet<int>();
            var live = new List<(RoundShot shot, int gun, int ordinal, Vector3 previous)>();
            var first = new Dictionary<int, float>();
            var second = new Dictionary<int, float>();
            for (int frame = 0; frame < 2100; frame++)
            {
                if (company.ShotsFired != seenCount)
                {
                    foreach (var shot in FindObjectsByType<RoundShot>(FindObjectsSortMode.None))
                    {
                        if (!seen.Add(shot.GetInstanceID())) continue;
                        if ((NavalShip)sourceField.GetValue(shot) != Ship ||
                            !(gunnerField.GetValue(shot) is CrewMate gunner) || !company.Mates.Contains(gunner) ||
                            gunner.Job != CrewPlayer.Role.Gun)
                            throw new InvalidOperationException("Observed projectile did not come from a player-ship AI gunner");
                        int gun = gunner.JobIndex;
                        if (gun < 0 || gun >= guns.Length || company.LastShotSide != 1 ||
                            Vector3.Dot(guns[gun].forward, Ship.transform.right) <= .5f ||
                            company.ShotsFiredByGun(gun) != gunShots[gun] + 1)
                            throw new InvalidOperationException($"Observed projectile did not match a new starboard shot at gun {gun}");
                        gunShots[gun]++;
                        live.Add((shot, gun, gunShots[gun] - startingShots[gun], shot.transform.position));
                        seenCount++;
                    }
                    if (seenCount != company.ShotsFired)
                        throw new InvalidOperationException($"Observed {seen.Count} AI launches, company recorded {company.ShotsFired - initialCount}");
                }

                for (int i = live.Count - 1; i >= 0; i--)
                {
                    var sample = live[i];
                    if (!sample.shot)
                    {
                        live.RemoveAt(i);
                        continue;
                    }
                    Vector3 point = sample.shot.transform.position;
                    if (sample.ordinal <= 2 && sample.previous.x < FloodedFireSampleX && point.x >= FloodedFireSampleX)
                    {
                        var heights = sample.ordinal == 1 ? first : second;
                        float fraction = Mathf.InverseLerp(sample.previous.x, point.x, FloodedFireSampleX);
                        heights.Add(sample.gun, Mathf.Lerp(sample.previous.y, point.y, fraction));
                    }
                    sample.previous = point;
                    live[i] = sample;
                }

                if (rotate ? HasShotHeightSpread(first) && HasShotHeightSpread(second) &&
                    HasShotHeightRotation(first, second) && Ship.HitsLanded - hits >= 2 :
                    first.Count > 0 && Ship.HitsLanded > hits && target.Flooding.ActiveLeaks > leaks)
                    break;
                yield return null;
            }

            using (new AcceptanceCompanyFixture(Ship))
            {
                for (int frame = 0; frame < 370 && FindAnyObjectByType<RoundShot>(); frame++)
                    yield return null;
                Require(!FindAnyObjectByType<RoundShot>() && seen.Count > 0 && company.ShotsFired - initialCount == seen.Count,
                    $"all {seen.Count} sourced AI starboard projectiles resolved at {target.Flooding.WaterLevel01:P0} fill");
            }

            if (!rotate)
            {
                Require(target.Flooding.WaterLevel01 == 0f && first.Count > 0 && Ship.HitsLanded > hits &&
                    target.Flooding.ActiveLeaks > leaks,
                    $"AI shot hits the dry {target.Definition.Type}, hull hits={Ship.HitsLanded - hits}, new breaches={target.Flooding.ActiveLeaks - leaks}");
                yield break;
            }

            string Heights(Dictionary<int, float> samples) => string.Join(", ", samples.OrderBy(sample => sample.Key)
                .Select(sample => $"{sample.Key}:{sample.Value:F2}"));
            Require(HasShotHeightSpread(first) && HasShotHeightSpread(second) && Ship.HitsLanded - hits >= 2,
                $"first two shots per gun span distinct heights at x={FloodedFireSampleX:F0}m, " +
                $"first={Heights(first)}, second={Heights(second)}, hull hits={Ship.HitsLanded - hits}");
            string changes = string.Join(", ", first.Keys.Intersect(second.Keys).OrderBy(gun => gun)
                .Select(gun => $"{gun}:{second[gun] - first[gun]:+0.00;-0.00;0.00}"));
            Require(HasShotHeightRotation(first, second),
                $"consecutive shots by the same guns rotate upward and downward, height changes in metres={changes}");
        }

        static bool HasShotHeightSpread(Dictionary<int, float> heights) => heights.Count >= 3 &&
            heights.Values.Max() - heights.Values.Min() > .45f && heights.Values.Max() > OriginalShotCutoff;

        static bool HasShotHeightRotation(Dictionary<int, float> first, Dictionary<int, float> second)
        {
            var shared = first.Keys.Intersect(second.Keys).ToArray();
            return shared.Length >= 3 && shared.Any(gun => second[gun] - first[gun] > .20f) &&
                shared.Any(gun => first[gun] - second[gun] > .20f);
        }

        IEnumerator CheckCannonRange(NavalShip target)
        {
            for (int i = 0; i < target.Rig.LeakInside.Length; i++)
                target.Flooding.Repair(i);
            var company = Ship.Company;
            var guns = Ship.Rig.Guns;
            float referenceX = guns.Max(gun => gun.position.x);
            void PlaceRangeTarget(float range)
            {
                target.transform.SetPositionAndRotation(new Vector3(referenceX + range + target.Definition.Beam * .5f,
                    -target.Draft, Ship.Definition.PivotZ - target.Definition.PivotZ), Quaternion.identity);
                Physics.SyncTransforms();
            }

            PlaceRangeTarget(520f);
            using (new AcceptanceCompanyFixture(Ship))
                yield return CheckManualCannonRange();

            int initialShots = company.ShotsFired, initialHits = Ship.HitsLanded;
            int readyFrames = 0;
            for (int frame = 0; frame < 1200 && readyFrames < 90; frame++)
            {
                yield return null;
                if (company.ShotsFired != initialShots || FindAnyObjectByType<RoundShot>())
                    throw new InvalidOperationException("AI fired at the ship beyond 520m");
                readyFrames = company.Mates.Any(mate => mate.Job == CrewPlayer.Role.Gun && mate.AtPost &&
                    Ship.IsLoaded(mate.JobIndex)) ? readyFrames + 1 : 0;
            }
            float closestFarHull = target.transform.position.x - target.Definition.Beam * .5f - referenceX;
            Require(readyFrames >= 90 && closestFarHull > NavalShip.MaxShotRange && Ship.HitsLanded == initialHits &&
                !Enumerable.Range(0, guns.Length).Any(index => company.TryAim(index, out _)),
                $"AI held a loaded, staffed gun at a target at least {closestFarHull:F1}m from all cannon positions");

            PlaceRangeTarget(492f);
            Require(Ship.Anchored && target.Anchored && Ship.Speed == 0f && target.Speed == 0f,
                "long-range AI shot uses two stationary ships");
            var gunnerField = typeof(RoundShot).GetField("gunner", FloodedFirePrivateInstance);
            var sourceField = typeof(RoundShot).GetField("source", FloodedFirePrivateInstance);
            var velocityField = typeof(RoundShot).GetField("velocity", FloodedFirePrivateInstance);
            float impactRange = 0f;
            for (int frame = 0; frame < 1500 && Ship.HitsLanded == initialHits; frame++)
            {
                impactRange = 0f;
                foreach (var shot in FindObjectsByType<RoundShot>(FindObjectsSortMode.None))
                {
                    if ((NavalShip)sourceField.GetValue(shot) != Ship ||
                        !(gunnerField.GetValue(shot) is CrewMate mate) || !company.Mates.Contains(mate) ||
                        mate.Job != CrewPlayer.Role.Gun)
                        throw new InvalidOperationException("Long-range projectile did not come from a player-ship AI gunner");
                    Vector3 origin = guns[mate.JobIndex].position;
                    Vector3 point = shot.transform.position;
                    if (HorizontalCannonRange(origin, point) < 488f) continue;
                    // Match a registered impact to the path of a real round on the following simulation step.
                    Vector3 step = (Vector3)velocityField.GetValue(shot) * FloodedFireStep -
                        Vector3.up * (.5f * NavalShip.ShotDrop * FloodedFireStep * FloodedFireStep);
                    foreach (var contact in Physics.RaycastAll(point, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
                        if (contact.collider.GetComponentInParent<NavalShip>() == target)
                        {
                            impactRange = HorizontalCannonRange(origin, contact.point);
                            break;
                        }
                }
                yield return null;
            }
            Require(company.ShotsFired > initialShots && Ship.HitsLanded > initialHits && impactRange >= 490f && impactRange <= 498f,
                $"AI projectile hit the stationary {target.Definition.Type} {impactRange:F2}m from its cannon, " +
                $"launches={company.ShotsFired - initialShots}, hits={Ship.HitsLanded - initialHits}");
        }

        IEnumerator CheckManualCannonRange()
        {
            int index = Small ? ApproachWeatherGun() : GunIndex("下层右舷炮饰 04");
            Transform gun = Ship.Rig.Guns[index];
            if (!Small) ApproachGun(gun);
            yield return Frames(3);
            Enter("右舷炮");
            for (int frame = 0; frame < 320 && !Ship.IsLoaded(index); frame++)
            {
                Crew.Tick(default, FloodedFireStep);
                yield return null;
            }
            Tick(new CrewCommand { Look = Vector2.up * 1000f });
            Require(Crew.IsAiming && Ship.IsLoaded(index) && Crew.GunPitch == 26f,
                $"player set the loaded {gun.name} to the 26 degree elevation limit");

            // Sample the real round at 30 Hz, with the last unrestricted step crossing the cap.
            const float rangeStep = 1f / 30f;
            Vector3 origin = gun.position;
            Vector3 direction = NavalShip.ShotDirection(gun, Crew.GunYaw, Crew.GunPitch);
            Vector3 muzzle = origin + direction * NavalShip.ShotSpawnOffset;
            Vector3 launch = direction * NavalShip.ShotSpeed;
            float horizontalStep = HorizontalCannonRange(Vector3.zero, launch * rangeStep);
            int lastFrame = Mathf.FloorToInt((NavalShip.MaxShotRange - HorizontalCannonRange(origin, muzzle)) / horizontalStep);
            Vector3 PointAtFrame(int frame)
            {
                float time = frame * rangeStep;
                return muzzle + launch * time - Vector3.up * (.5f * NavalShip.ShotDrop * time * time);
            }
            Vector3 last = PointAtFrame(lastFrame), next = PointAtFrame(lastFrame + 1);
            float cutoff = Mathf.InverseLerp(HorizontalCannonRange(origin, last), HorizontalCannonRange(origin, next), NavalShip.MaxShotRange);
            Vector3 probe = Vector3.Lerp(last, next, (1f + cutoff) * .5f);
            var dummy = CombatDummy.Create(transform, "Range limit collision probe",
                transform.InverseTransformPoint(probe - Vector3.up * .90f));
            try
            {
                Physics.SyncTransforms();
                var rawContact = Physics.RaycastAll(last, (next - last).normalized, Vector3.Distance(last, next), ~0,
                    QueryTriggerInteraction.Collide).FirstOrDefault(hit => hit.collider.GetComponentInParent<CombatDummy>() == dummy);
                float contactRange = HorizontalCannonRange(origin, rawContact.point);
                Require(rawContact.collider && contactRange > NavalShip.MaxShotRange + .10f && rawContact.point.y > -6f,
                    $"unclipped last step would touch the active hurtbox at {contactRange:F2}m, height={rawContact.point.y:F2}m");

                Time.captureDeltaTime = rangeStep;
                Tick(new CrewCommand { FirePressed = true });
                var shot = FindAnyObjectByType<RoundShot>();
                var gunnerField = typeof(RoundShot).GetField("gunner", FloodedFirePrivateInstance);
                Require(shot && !Ship.IsLoaded(index) && (CrewDamageTarget)gunnerField.GetValue(shot) == Crew,
                    "the normal occupied cannon command launched the real player projectile");
                Vector3 previous = shot.transform.position;
                float waterCrossing = 0f, furthest = 0f;
                for (int frame = 0; shot && frame < 300; frame++)
                {
                    Vector3 point = shot.transform.position;
                    furthest = Mathf.Max(furthest, HorizontalCannonRange(origin, point));
                    if (previous.y > 0f && point.y <= 0f)
                        waterCrossing = HorizontalCannonRange(origin, Vector3.Lerp(previous, point, previous.y / (previous.y - point.y)));
                    previous = point;
                    yield return null;
                }
                Require(!shot && waterCrossing >= 494f && waterCrossing <= NavalShip.MaxShotRange &&
                    furthest >= 498f && furthest <= NavalShip.MaxShotRange + .02f && dummy.Health == CrewHealth.Maximum,
                    $"26 degree manual round crossed sea level at {waterCrossing:F2}m from the cannon, " +
                    $"furthest position={furthest:F2}m and the {contactRange:F2}m probe kept {dummy.Health} HP");
            }
            finally
            {
                Time.captureDeltaTime = FloodedFireStep;
                Destroy(dummy.gameObject);
                Leave();
            }
        }

        static float HorizontalCannonRange(Vector3 origin, Vector3 point) =>
            new Vector2(point.x - origin.x, point.z - origin.z).magnitude;

        IEnumerator FloodedFireMiss(NavalShip target)
        {
            var guns = Ship.Rig.Guns;
            int index = Array.FindIndex(guns, gun => gun.forward.x < -.8f);
            int hits = Ship.HitsLanded, leaks = target.Flooding.ActiveLeaks;
            var shot = LaunchFloodedFireShot(index, 0f, NavalShip.MinGunPitch);
            const float cutoffDepth = 6f;
            float lastDepth = -shot.transform.position.y;
            float previousDepth = lastDepth;
            for (int frame = 0; shot && frame < 120; frame++)
            {
                previousDepth = lastDepth;
                lastDepth = -shot.transform.position.y;
                yield return null;
            }
            // A coroutine can still see a projectile on the frame where Update schedules Destroy.
            // If the crossing is hidden, use the preceding measured step plus one frame of gravity.
            float previousStep = lastDepth - previousDepth;
            float nextDepth = lastDepth + previousStep + NavalShip.ShotDrop * FloodedFireStep * FloodedFireStep;
            Require(!shot && previousStep > 0f && previousDepth <= cutoffDepth && nextDepth >= cutoffDepth &&
                Ship.HitsLanded == hits && target.Flooding.ActiveLeaks == leaks,
                $"open-water miss is removed at the 6m depth boundary, last two live depths={previousDepth:F2}m/{lastDepth:F2}m, next step would reach {nextDepth:F2}m");
        }
    }
}
