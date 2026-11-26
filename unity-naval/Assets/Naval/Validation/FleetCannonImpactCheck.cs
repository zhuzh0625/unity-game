using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        public static void BeginCannonImpact(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(session, directory, "cannon-impact-check", "Cannon impact", check.RunCannonImpact());
        }

        IEnumerator RunCannonImpact()
        {
            yield return null;
            type = ShipType.Sloop;
            game.SelectShip(type);
            game.StartVoyage();
            using (new AcceptanceCompanyFixture(Ship, game.Navy))
            {
                Crew.ReadDeviceInput = false;
                Ship.enabled = game.Navy.enabled = false;
                yield return null;
                CheckCannonDistanceBands();
                yield return CheckCannonDirectHit();
                yield return CheckCannonHullHit(game.Navy, Ship);
                yield return CheckCannonHullHit(Ship, game.Navy);
            }
            game.ReturnToMenu();
            yield return null;

            // Local contacts and gun indices from the four live shots shown in the design preview.
            const string sloopHull = "Collision_HullAndBulwarks";
            yield return CheckCannonAtStations(ShipType.Sloop, "waterline",
                new Vector3(2.228373f, 1.65000045f, 6.64999962f), Vector3.left, sloopHull, 5, 100);
            yield return CheckCannonAtStations(ShipType.Sloop, "stern",
                new Vector3(2.16491365f, 3.5165894f, 3.203124f), Vector3.left, sloopHull, 4, 50);
            yield return CheckCannonAtStations(ShipType.Brigantine, "waterline",
                new Vector3(3.497335f, 1.89000022f, 12.6000023f), Vector3.left, "Collision_Fixtures", 10, 100);
            yield return CheckCannonAtStations(ShipType.Brigantine, "stern",
                new Vector3(.606000066f, 4.818651f, 5.052f), new Vector3(.3378092f, -.156434089f, .928123534f),
                "Collision_Shell", 7, 50);
            yield return CheckNavyCannonAtBrigHelm(1);
            yield return CheckNavyCannonAtBrigHelm(-1);
        }

        IEnumerator CheckNavyCannonAtBrigHelm(int side)
        {
            type = ShipType.Brigantine;
            game.SelectShip(type);
            game.StartVoyage();
            var vessel = Ship;
            var source = game.Navy;
            var company = source.Company;
            var captain = Crew;
            string broadside = side > 0 ? "starboard" : "port";

            using (new AcceptanceCompanyFixture(vessel))
            {
                vessel.enabled = source.enabled = vessel.Flooding.enabled = false;
                vessel.transform.SetPositionAndRotation(new Vector3(0f, -vessel.Draft, 0f), Quaternion.identity);
                source.transform.SetPositionAndRotation(new Vector3(side * 40f, -source.Draft, 1.4f), Quaternion.identity);
                Physics.SyncTransforms();
                Note($"FIXTURE navy attacks the brigantine's {broadside} side. Dry level hulls, same heading, " +
                    "40m across and 1.4m forward. Player company is idle and ingress paused. Both crews retain their hitboxes.");
                CheckNewVoyageGuns();
                yield return HoldKeys(1, Key.E);
                yield return HoldKeys(16);
                Require(!game.IsCombatTest && source.Definition.Type == type && vessel.Definition.Type == type &&
                    captain.IsAtHelm && captain.Health == 100 && company.ShotsFired == 0,
                    $"navy {broadside}: player occupies the selected brigantine's helm at full health before any launch");

                // Only the navy company can fire here. Read the live damage and hull-hit results.
                double deadline = Time.timeAsDouble + 27.0;
                while (captain.Health == 100 && Time.timeAsDouble < deadline)
                    yield return null;
                Require(vessel.Company.ShotsFired == 0 && company.LastShotSide == -side &&
                    company.ShotsFired >= 2 && source.HitsLanded >= 2 && captain.Health == 50 && captain.IsAtHelm,
                    $"navy {broadside}: player at the helm HP 100→{captain.Health}, " +
                    $"navy AI launches {company.ShotsFired}, hull hits {source.HitsLanded}");
            }
            game.ReturnToMenu();
            yield return null;
        }

        IEnumerator CheckCannonAtStations(ShipType choice, string example, Vector3 localPoint, Vector3 localApproach,
            string colliderName, int gunIndex, int captainHealth)
        {
            type = choice;
            game.SelectShip(type);
            game.StartVoyage();
            var vessel = Ship;
            var source = game.Navy;
            vessel.enabled = source.enabled = false;
            Crew.ReadDeviceInput = false;
            Crew.Tick(new CrewCommand { UsePressed = true }, Step);
            for (int i = 0; i < 16; i++) Crew.Tick(default, Step);
            Require(Crew.IsAtHelm, $"{example}: player used the helm ({Crew.Prompt})");

            var gunner = vessel.Company.Mates[0];
            var gun = vessel.Rig.Guns[gunIndex];
            Require(CrewStationFit.TryGetPose(vessel, CrewPlayer.Role.Gun, gunIndex, gun,
                gunner.transform.position, out var foot, out _), $"{example}: {gun.name} has a usable crew pose");
            gunner.transform.position = foot;
            Require(gunner.Assign(CrewPlayer.Role.Gun, gunIndex), $"{example}: gunner accepted assignment to {gun.name}");
            gunner.SendMessage("Update");
            Require(gunner.AtPost && gunner.JobIndex == gunIndex && gunner.Health == 100 && Crew.Health == 100,
                $"{example}: both sailors at their posts at full health");

            using (new AcceptanceCompanyFixture(vessel, source))
            {
                Physics.SyncTransforms();
                Vector3 planned = vessel.transform.TransformPoint(localPoint);
                Vector3 direction = vessel.transform.TransformDirection(localApproach).normalized;
                var hits = Physics.RaycastAll(planned - direction * 35f, direction, 35.25f,
                    Physics.AllLayers, QueryTriggerInteraction.Collide);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                var first = Array.Find(hits, hit =>
                {
                    if (hit.collider.isTrigger)
                    {
                        var target = hit.collider.GetComponentInParent<CrewDamageTarget>();
                        return target && target.Health > 0;
                    }
                    var ship = hit.collider.GetComponentInParent<NavalShip>();
                    return ship && ship != source;
                });
                Require(first.collider && first.collider.name == colliderName &&
                    first.collider.GetComponentInParent<NavalShip>() == vessel &&
                    Vector3.Distance(first.point, planned) <= .025f,
                    $"{example}: exterior ray first contacted {first.collider?.name} at " +
                    $"{vessel.transform.InverseTransformPoint(first.point):F4}, expected {colliderName} at {localPoint:F4}");

                // The full ray checks the exterior approach. The live projectile can then resolve its first sweep.
                yield return CannonShot(source, first.point - direction * .08f, direction);
                Require(gunner.Health == 0 && Crew.Health == captainHealth && Crew.IsAtHelm,
                    $"{example}: {gun.name} gunner/captain HP {gunner.Health}/{Crew.Health}, expected 0/{captainHealth}");
            }
            game.ReturnToMenu();
            yield return null;
        }

        void CheckCannonDistanceBands()
        {
            var centre = new Vector3(0f, 64f, 0f);
            using var targets = new CannonTargets();
            float inner = RoundShot.InnerRadius, outer = RoundShot.OuterRadius;
            Require(inner == 1.8f && outer == 3f, $"approved cannon radii are {inner:F2}/{outer:F2} m");
            float middle = (inner + outer) * .5f;
            var distances = new[] { inner - .02f, inner, inner + .02f, outer, outer + .02f, outer + .20f, middle, .50f };
            var directions = new[] { Vector3.back, Vector3.right, Vector3.left, Vector3.forward,
                Vector3.back, Vector3.up, Vector3.forward, Vector3.left };
            var expected = new[] { 0, 0, 50, 50, 100, 100, 50, 0 };
            for (int i = 0; i < distances.Length; i++)
            {
                Vector3 direction = directions[i];
                float reach = direction.y == 0f ? .25f : .90f;
                float offset = i == 7 ? middle - distances[i] : 0f;
                targets.Add("range " + i, centre + direction * (distances[i] + reach + offset));
            }

            // Two equal outer-band hitboxes must only cost 50 HP. A nearer hitbox belonging
            // to another dummy must take precedence over that dummy's farther hitbox.
            targets.AddCapsule(6, Vector3.zero);
            targets.AddCapsule(7, Vector3.right * (middle - distances[7]));
            Physics.SyncTransforms();
            var measured = new float[distances.Length];
            for (int i = 0; i < distances.Length; i++)
            {
                measured[i] = targets.Distance(i, centre);
                Require(Mathf.Abs(measured[i] - distances[i]) < .001f,
                    $"range fixture {i} lies at {measured[i]:F5} m (target {distances[i]:F2} m)");
            }
            RoundShot.DamageCrewAt(centre);
            for (int i = 0; i < distances.Length; i++)
                Require(targets[i].Health == expected[i],
                    $"range fixture {i} at {measured[i]:F5} m, direction {directions[i]}, HP {targets[i].Health}/{expected[i]}");
        }

        IEnumerator CheckCannonDirectHit()
        {
            using var targets = new CannonTargets();
            var body = CannonHurtbox(Crew);
            float inner = RoundShot.InnerRadius, outer = RoundShot.OuterRadius;
            Physics.SyncTransforms();
            Vector3 origin = body.bounds.center + Vector3.up * (body.bounds.extents.y + .40f);
            Require(body.Raycast(new Ray(origin, Vector3.down), out var contact, 1f),
                "downward firing line reaches the live player hurtbox");
            targets.Add("near the direct hit", contact.point + Vector3.right * ((inner + outer) * .5f + .25f));
            targets.AddCapsule(0, Vector3.zero);
            targets.Add("outside the direct hit", contact.point + Vector3.left * (outer + .50f));
            Physics.SyncTransforms();
            float near = targets.Distance(0, contact.point), far = targets.Distance(1, contact.point);
            Require(near > inner + .2f && near < outer - .2f && far > outer + .1f,
                $"direct-hit neighbours at {near:F3}/{far:F3} m");
            yield return CannonShot(game.Navy, origin, Vector3.down);
            Require(Crew.Health == 0 && Crew.IsDead && targets[0].Health == 50 && targets[1].Health == 100,
                $"navy direct hit killed live player and applied surrounding HP {targets[0].Health}/{targets[1].Health}");
        }

        IEnumerator CheckCannonHullHit(NavalShip source, NavalShip struck)
        {
            var flooding = struck.Flooding;
            var anchors = struck.Rig.LeakOutside;
            var anchor = anchors[0];
            Vector3 outboard = anchor.forward;
            Vector3 origin = anchor.position + outboard * .9f;
            Vector3 along = Vector3.Cross(Vector3.up, outboard).normalized;
            float inner = RoundShot.InnerRadius, outer = RoundShot.OuterRadius;
            float outerBandCentre = (inner + outer) * .5f + .25f;
            Physics.SyncTransforms();
            bool hasHull = Physics.Raycast(origin, -outboard, out var hull, 1.4f, ~0, QueryTriggerInteraction.Ignore);
            Require(hasHull && hull.collider.GetComponentInParent<NavalShip>() == struck &&
                (hull.collider.name == "Collision_HullAndBulwarks" || hull.collider.name == "Collision_Shell"),
                $"{struck.Definition.Type} real outer planking collision at {hull.point:F4}, mesh {hull.collider?.name}");

            int initial = flooding.ActiveLeaks;
            int hits = source.HitsLanded;
            yield return CannonShot(source, origin, -outboard);
            Require(source.HitsLanded == hits + 1 && flooding.ActiveLeaks == initial + 1 && flooding.IsLeaking(0),
                $"{source.name} opened one initial breach on {struck.name}");

            using (var targets = new CannonTargets())
            {
                targets.Add("fore of real hull contact", hull.point + along * outerBandCentre);
                targets.Add("aft of real hull contact", hull.point - along * outerBandCentre);
                Physics.SyncTransforms();
                float one = targets.Distance(0, hull.point), two = targets.Distance(1, hull.point);
                Require(one > inner && one < outer && two > inner && two < outer,
                    $"both test sailors lie in the contact's outer band at {one:F3}/{two:F3} m");
                var openBefore = new bool[anchors.Length];
                for (int i = 0; i < anchors.Length; i++) openBefore[i] = flooding.IsLeaking(i);
                yield return CannonShot(source, origin, -outboard);
                int alternate = -1;
                for (int i = 0; i < anchors.Length; i++)
                    if (!openBefore[i] && flooding.IsLeaking(i)) alternate = i;
                Require(source.HitsLanded == hits + 2 && flooding.ActiveLeaks == initial + 2 && alternate > 0,
                    $"{source.name} opened offset breach {alternate} on {struck.name}");
                Vector3 replacement = anchors[alternate].position;
                float altOne = targets.Distance(0, replacement), altTwo = targets.Distance(1, replacement);
                Require((altOne > outer + .1f && altTwo < inner - .1f) ||
                        (altTwo > outer + .1f && altOne < inner - .1f),
                    $"offset breach at {replacement:F4} would change both damage bands ({altOne:F3}/{altTwo:F3} m)");
                Require(targets[0].Health == 50 && targets[1].Health == 50,
                    $"true hull contact at {hull.point:F4} produced HP {targets[0].Health}/{targets[1].Health} despite offset breach");
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                int before = flooding.ActiveLeaks;
                flooding.Hit(hull.point, hull.normal);
                if (flooding.ActiveLeaks == before) break;
            }
            using (var targets = new CannonTargets())
            {
                targets.Add("near saturated hull contact", hull.point + along * .85f);
                targets.Add("outer band of saturated hull contact", hull.point - along * outerBandCentre);
                targets.Add("outside saturated hull contact", hull.point + along * (outer + .60f));
                Physics.SyncTransforms();
                float near = targets.Distance(0, hull.point), middle = targets.Distance(1, hull.point),
                    outside = targets.Distance(2, hull.point);
                Require(near < inner && middle > inner && middle < outer && outside > outer,
                    $"saturated hull fixture at {near:F3}/{middle:F3}/{outside:F3} m from contact");
                int before = flooding.ActiveLeaks;
                yield return CannonShot(source, origin, -outboard);
                Require(source.HitsLanded == hits + 3 && flooding.ActiveLeaks == before &&
                    targets[0].Health == 0 && targets[1].Health == 50 && targets[2].Health == 100,
                    $"{source.name} hit saturated {struck.name}, retained {before} breaches and produced HP " +
                    $"{targets[0].Health}/{targets[1].Health}/{targets[2].Health}");
            }
        }

        IEnumerator CannonShot(NavalShip source, Vector3 origin, Vector3 direction)
        {
            var round = new GameObject("Acceptance cannon round").AddComponent<RoundShot>();
            round.transform.position = origin;
            // Cancel gravity on the first frame, keeping the independently measured ray contact.
            round.Launch(source, direction * 42f + Vector3.up * (4f * Step));
            for (int i = 0; round && i < 5; i++) yield return null;
            Require(!round, $"{source.name} projectile was consumed by its normal collision sweep");
        }

        static CapsuleCollider CannonHurtbox(CrewDamageTarget target) =>
            Array.Find(target.GetComponentsInChildren<CapsuleCollider>(), collider => collider.isTrigger && collider.enabled);

        sealed class CannonTargets : IDisposable
        {
            readonly GameObject root = new GameObject("Acceptance cannon damage samples");
            readonly List<CombatDummy> samples = new();

            internal CombatDummy this[int index] => samples[index];

            internal void Add(string name, Vector3 centre)
            {
                var dummy = CombatDummy.Create(root.transform, name, Vector3.zero);
                var body = CannonHurtbox(dummy);
                dummy.transform.position += centre - body.transform.TransformPoint(body.center);
                samples.Add(dummy);
            }

            internal void AddCapsule(int index, Vector3 worldOffset)
            {
                var body = CannonHurtbox(samples[index]);
                var extra = body.gameObject.AddComponent<CapsuleCollider>();
                extra.isTrigger = true;
                extra.radius = body.radius;
                extra.height = body.height;
                extra.direction = body.direction;
                extra.center = body.center + body.transform.InverseTransformVector(worldOffset);
            }

            internal float Distance(int index, Vector3 centre)
            {
                float distance = float.PositiveInfinity;
                foreach (var body in samples[index].GetComponentsInChildren<CapsuleCollider>())
                    distance = Mathf.Min(distance, Vector3.Distance(centre, body.ClosestPoint(centre)));
                return distance;
            }

            public void Dispose()
            {
                root.SetActive(false);
                Destroy(root);
                Physics.SyncTransforms();
            }
        }
    }
}
