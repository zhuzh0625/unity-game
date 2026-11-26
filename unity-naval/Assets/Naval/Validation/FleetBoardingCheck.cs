using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        IEnumerator RunBoarding()
        {
            yield return null;
            Require(game.IsInMenu, "starts at fleet selection");
            foreach (var choice in new[] { ShipType.Sloop, ShipType.Brigantine })
            {
                type = choice;
                Click(game.Menu.GetShipButton(type));
                Click(game.Menu.StartButton);
                Note("FIXTURE boarding routes ignore stray gunfire and use only the measured moving-ship controls.");
                using var companyFixture = new AcceptanceCompanyFixture(Ship, game.Navy);
                yield return null;
                Require(Ship && Ship.Definition.Type == type && Crew, "started selected voyage");
                var ladders = Ship.Definition.Data.boardingLadders;
                int expected = Small ? 2 : 4;
                Require(ladders?.Length == expected &&
                    Array.FindAll(ladders, ladder => ladder.bottom.x < 0f).Length == expected / 2 &&
                    Array.FindAll(ladders, ladder => ladder.bottom.x > 0f).Length == expected / 2,
                    $"R06 exterior ladders count={ladders?.Length}, expected={expected} split equally across both sides");
                for (int i = 0; i < ladders.Length; i++)
                {
                    var ladder = ladders[i];
                    Note("ROUTE " + ladder.name);
                    var mesh = Ship.Rig.transform.Find(ladder.name);
                    Require(mesh && mesh.TryGetComponent<MeshRenderer>(out var drawn) && drawn.enabled &&
                        mesh.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh && filter.sharedMesh.vertexCount > 12,
                        $"R06 physical ladder {ladder.name} has its imported visible mesh");
                    yield return BoardOne(ladder, i == 0);
                }
                // Walk from the last ladder landing directly to a neighbouring working station.
                int index = GunIndex(Small ? "S2" : "上层右舷炮饰 01");
                var gun = Ship.Rig.Guns[index];
                Require(CrewStationFit.TryGetPose(Ship, CrewPlayer.Role.Gun, index, gun, Crew.transform.position,
                    out var gunFoot, out _), "neighbouring starboard cannon has an authored working position");
                var stand = Ship.transform.InverseTransformPoint(gunFoot);
                for (int i = 0; i < 45 && HorizontalTo(stand).magnitude > .16f; i++)
                    yield return HoldKeys(1, TowardKey(HorizontalTo(stand)));
                yield return HoldKeys(1);
                yield return HoldKeys(1, Key.E);
                yield return HoldKeys(2);
                Require(Crew.IsAiming && Crew.StationName == "右舷炮" && !Crew.IsSwimming && !Crew.IsClimbing,
                    $"walked from ladder to {gun.name} and entered with E at {CrewOnShip:F2}; active={Crew.StationName}");
                Crew.ReadDeviceInput = false;
                game.ReturnToMenu();
                yield return null;
                Require(game.IsInMenu && !Ship && !Crew, "returned to fleet selection");
            }
        }

        Vector3 CrewOnShip => Ship.transform.InverseTransformPoint(Crew.transform.position);

        void Keyboard(params Key[] keys) =>
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(keys));

        IEnumerator HoldKeys(int frames, params Key[] keys)
        {
            Keyboard(keys);
            for (int i = 0; i < frames; i++) yield return null;
        }

        // Choose an actual unobstructed deck segment across solid timber near the ladder.
        // Only the start of the independent jump is placed; all travel from there uses the player controls.
        Vector3 Takeoff(Vector3 landing, float side, out Vector3 rail)
        {
            var hull = Ship.transform;
            var outward = hull.right * side;
            foreach (float dz in new[] { -.85f, .85f, -1.35f, 1.35f, -1.9f, 1.9f, -.45f, .45f })
            {
                var origin = hull.TransformPoint(0, landing.y + .45f, landing.z + dz);
                var hits = Physics.RaycastAll(origin, outward, Ship.Definition.Beam, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                foreach (var wall in hits)
                {
                    if (!wall.transform.IsChildOf(hull) ||
                        !(wall.collider.name.Contains("Shell") || wall.collider.name.Contains("Bulwark"))) continue;
                    rail = hull.InverseTransformPoint(wall.point);
                    if (Mathf.Abs(rail.x - landing.x) > 2f || Vector3.Dot(wall.normal, outward) > -.55f) continue;
                    Vector3 horizontal = wall.point - outward * 1.48f;
                    if (!Physics.Raycast(horizontal + hull.up * .08f, -hull.up, out var floor, .92f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || !floor.transform.IsChildOf(hull) ||
                        !floor.collider.name.Contains("Deck") || floor.collider.name.Contains("Fitting") ||
                        Vector3.Dot(floor.normal, hull.up) < .8f) continue;
                    var foot = floor.point + hull.up * .055f;
                    if (Physics.CheckCapsule(foot + hull.up * .22f, foot + hull.up * 1.34f, .21f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.SphereCast(foot + hull.up * .29f, .18f, outward, out _, .96f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    Note($"FIXTURE {wall.collider.name} at {rail:F2}, deck={floor.collider.name}, takeoff={hull.InverseTransformPoint(floor.point):F2}");
                    return hull.InverseTransformPoint(floor.point);
                }
            }
            throw new InvalidOperationException($"{type} found no clear deck takeoff facing the physical bulwark at {landing:F2}");
        }

        IEnumerator BoardOne(BoardingLadder ladder, bool capture)
        {
            string name = ladder.name;
            Vector3 bottom = ladder.bottom, top = ladder.top, landing = ladder.landing;
            float side = Mathf.Sign(bottom.x);
            Vector3 outward = Ship.transform.right * side;
            Crew.ReadDeviceInput = false;
            Physics.SyncTransforms();
            var start = Takeoff(landing, side, out var rail);
            Fixture(start);
            yield return Frames(4);
            if (capture)
            {
                Crew.ReadDeviceInput = true;
                yield return HoldKeys(1, Key.Space);
                bool airborne = Crew.IsJumping;
                yield return HoldKeys(38);
                Require(airborne && !Crew.IsJumping && !Crew.IsSwimming && Vector3.Distance(CrewOnShip, start) < .20f,
                    $"standing Space jumps and returns to same deck at {CrewOnShip:F2}");
            }

            var outwardKey = TowardKey(outward);
            Crew.ReadDeviceInput = true;
            Vector3 before = CrewOnShip;
            yield return HoldKeys(3, outwardKey, Key.LeftShift);
            float sprint = Mathf.Abs(CrewOnShip.x - before.x) / (3 * Step);
            yield return HoldKeys(1);
            Crew.ReadDeviceInput = false;
            Fixture(start);
            yield return Frames(3);
            before = CrewOnShip;
            Crew.ReadDeviceInput = true;
            yield return HoldKeys(1, outwardKey, Key.Space);
            bool jumped = Crew.IsJumping;
            yield return HoldKeys(2, outwardKey);
            float jumpSpeed = Mathf.Abs(CrewOnShip.x - before.x) / (3 * Step);
            Require(jumped && sprint > 2.8f && Mathf.Abs(sprint - jumpSpeed) < .45f,
                $"{name} Space with direction, no Shift: jump={jumpSpeed:F2} m/s measured sprint={sprint:F2} m/s");
            var animation = Crew.GetComponentInChildren<Animation>(true);
            var body = Crew.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Require(animation && animation.name == "Deckhand_R06" && IsPose(animation, "Run"),
                $"{name} jumping uses imported R06 Run");
            float crossingY = float.NegativeInfinity;
            float peak = CrewOnShip.y;
            for (int i = 0; i < 90 && !Crew.IsSwimming; i++)
            {
                yield return null;
                Vector3 local = CrewOnShip;
                peak = Mathf.Max(peak, local.y);
                if (float.IsNegativeInfinity(crossingY) && (local.x - rail.x) * side >= 0)
                {
                    crossingY = local.y;
                    if (capture) yield return Photograph("jumping-" + type);
                }
            }
            Require(Crew.IsSwimming && crossingY > rail.y && (CrewOnShip.x - rail.x) * side > .2f,
                $"{name} crossed solid side at y={crossingY:F2}, wall sample={rail.y:F2}, apex={peak:F2}, sea entry={CrewOnShip:F2}");

            var waterEntry = Crew.transform.position;
            var swimKey = TowardKey(outward);
            if (capture) yield return CheckSwimStroke(name, animation, body, swimKey);
            else yield return HoldKeys(26, swimKey);
            Vector3 travel = Vector3.ProjectOnPlane(Crew.transform.position - waterEntry, Vector3.up);
            var head = SailorBone(body, "Head");
            var feet = (SailorBone(body, "Foot.L").position + SailorBone(body, "Foot.R").position) * .5f;
            Vector3 bodyAxis = head.position - feet;
            float alignment = Vector3.Dot(bodyAxis.normalized, travel.normalized);
            Require(Crew.IsSwimming && IsPose(animation, "Swim") && travel.magnitude > 1f && alignment > .5f &&
                Mathf.Abs(bodyAxis.y) < .65f && Mathf.Abs(head.position.y) < .65f,
                $"{name} surface crawl: head y={head.position.y:F2}, head-to-feet={bodyAxis:F2}, motion={travel:F2}, alignment={alignment:F2}");
            yield return HoldKeys(1);
            yield return new WaitForEndOfFrame();
            var camera = game.MainCamera;
            var visible = camera.WorldToViewportPoint(head.position);
            Require(SailorOnCamera(body) && camera.transform.position.y > .12f && visible.z > 0 &&
                visible.x > 0 && visible.x < 1 && visible.y > 0 && visible.y < 1,
                $"{name} swimmer's head in view={visible:F2}, camera above water at y={camera.transform.position.y:F2}");

            float nearest = float.PositiveInfinity;
            foreach (var candidate in Ship.Definition.Data.boardingLadders)
                nearest = Mathf.Min(nearest, HorizontalTo(candidate.bottom).magnitude);
            yield return HoldKeys(1, Key.E);
            yield return HoldKeys(2);
            Require(nearest > 2f && Crew.IsSwimming && !Crew.IsClimbing, $"{name} E in open water, nearest ladder {nearest:F2} m");
            if (capture)
            {
                Vector3 sailorAtRest = Crew.transform.position;
                Vector3 vesselAtRest = Ship.transform.position;
                Ship.SetSailStep(2);
                yield return HoldKeys(10);
                var spine = SailorBone(body, "Spine");
                var hand = SailorBone(body, "Hand.R");
                Vector3 idleHand = HandRelativeToSpine(spine, hand);
                yield return HoldKeys(10);
                float idleStroke = Vector3.Distance(idleHand, HandRelativeToSpine(spine, hand));
                yield return HoldKeys(14);
                float vesselTravel = Vector3.ProjectOnPlane(Ship.transform.position - vesselAtRest, Vector3.up).magnitude;
                float swimmerDrift = Vector3.ProjectOnPlane(Crew.transform.position - sailorAtRest, Vector3.up).magnitude;
                Require(Crew.IsSwimming && IsPose(animation, "Swim") && vesselTravel > .45f &&
                    swimmerDrift < .12f && idleStroke > .10f,
                    $"{name} idle swimmer hand stroke={idleStroke:F2} m, drift={swimmerDrift:F2} m, hull travel={vesselTravel:F2} m");
                Ship.SetSailStep(0);
                yield return HoldKeys(28);
            }

            yield return SwimToLadder(ladder);
            float middle = Mathf.Lerp(CrewOnShip.y, top.y, .58f);
            Keyboard(Key.W);
            for (int i = 0; i < 150 && Crew.IsClimbing && CrewOnShip.y < middle; i++) yield return null;
            Require(Crew.IsClimbing && CrewOnShip.y >= middle && (CrewOnShip.x - landing.x) * side > .1f &&
                IsPose(animation, "Run"), $"{name} W climbed exterior to {CrewOnShip:F2} using the imported Run pose");
            if (capture) yield return Photograph("climbing-" + type);

            yield return HoldKeys(1);
            var grip = CrewOnShip;
            var hullStart = Ship.transform.position;
            if (capture) Ship.SetSailStep(2);
            yield return HoldKeys(24);
            Require(Crew.IsClimbing && Vector3.Distance(CrewOnShip, grip) < .015f,
                $"{name} releasing movement held the same rung at {CrewOnShip:F3}");
            if (capture)
            {
                float hullTravel = Vector3.ProjectOnPlane(Ship.transform.position - hullStart, Vector3.up).magnitude;
                Require(hullTravel > .15f, $"{name} held the same position on a hull travelling {hullTravel:F2} m");
                Ship.SetSailStep(0);
            }
            yield return HoldKeys(2, Key.D);
            var right = CrewOnShip;
            yield return HoldKeys(2, Key.A);
            Require(Crew.IsClimbing && (right.z - grip.z) * side > .015f &&
                Mathf.Abs(right.y - grip.y) < .02f && Vector3.Distance(CrewOnShip, grip) < .025f,
                $"{name} D moved right facing the hull and A returned left, local {grip:F3} -> {right:F3} -> {CrewOnShip:F3}");
            yield return HoldKeys(2, Key.W, Key.D);
            var diagonal = CrewOnShip;
            yield return HoldKeys(2, Key.S, Key.A);
            Require(Crew.IsClimbing && diagonal.y > grip.y + .04f && (diagonal.z - grip.z) * side > .025f &&
                Vector3.Distance(CrewOnShip, grip) < .025f, $"{name} diagonal climb and reverse returned to the same rung");
            if (capture || Mathf.Abs(top.z - bottom.z) > .02f)
            {
                Keyboard(Key.W);
                for (int i = 0; i < 120 && Crew.IsClimbing && Mathf.Abs(CrewOnShip.x - top.x) < .34f; i++)
                    yield return null;
                Keyboard();
                var onRim = CrewOnShip;
                yield return HoldKeys(5);
                Require(Crew.IsClimbing && Mathf.Abs(onRim.y - top.y) < .04f && Vector3.Distance(CrewOnShip, onRim) < .02f,
                    $"{name} released W midway across the cap and stayed at {onRim:F3}");
                yield return HoldKeys(3, Key.S);
                Require(Crew.IsClimbing && (CrewOnShip.x - onRim.x) * side > .15f,
                    $"{name} reversed back outward across the cap");
            }
            yield return FinishAscent(ladder);

            // Back out at the landing, then descend below the gunwale on the same ladder.
            Keyboard(Key.S);
            bool caughtTop = false;
            for (int i = 0; i < 150; i++)
            {
                yield return null;
                caughtTop |= Crew.IsClimbing;
                if (Crew.IsClimbing && CrewOnShip.y <= middle) break;
                if (caughtTop && !Crew.IsClimbing) break;
            }
            Require(caughtTop && Crew.IsClimbing && CrewOnShip.y <= middle &&
                (CrewOnShip.x - landing.x) * side > .1f, $"{name} S descended from the deck onto the exterior rungs at {CrewOnShip:F2}");

            Key edge = side < 0f ? Key.A : Key.D;
            // Exercise the opposite end on the second brigantine pair.
            if (!Small && top.z < Ship.Definition.Length * .5f) edge = edge == Key.A ? Key.D : Key.A;
            float edgeSign = edge == Key.D ? side : -side;
            float center = bottom.z;
            float edgeHeight = CrewOnShip.y;
            float lastOffset = (CrewOnShip.z - center) * edgeSign;
            Keyboard(edge);
            for (int i = 0; i < 120 && Crew.IsClimbing; i++)
            {
                lastOffset = (CrewOnShip.z - center) * edgeSign;
                yield return null;
            }
            float releasedOffset = (CrewOnShip.z - center) * edgeSign;
            Require(!Crew.IsClimbing && lastOffset <= ladder.halfWidth + .005f &&
                releasedOffset > ladder.halfWidth && releasedOffset <= ladder.halfWidth + .06f && Mathf.Abs(CrewOnShip.y - edgeHeight) < .15f,
                $"{name} {edge} crossed rung edge at {releasedOffset:F2}m (last grip {lastOffset:F2}m, half-width {ladder.halfWidth:F2}m)");
            Keyboard();
            for (int i = 0; i < 80 && !Crew.IsSwimming; i++) yield return null;
            Require(Crew.IsSwimming && CrewOnShip.y < edgeHeight - .30f, $"{name} fell from the side of the ladder to sea at {CrewOnShip:F2}");
            yield return HoldKeys(10);
            Require(Crew.IsSwimming, $"{name} releasing the keys after a fall left the sailor in the water");
            yield return SwimToLadder(ladder);
            yield return FinishAscent(ladder);
            before = CrewOnShip;
            yield return HoldKeys(4, TowardKey(-outward));
            yield return HoldKeys(1);
            Require((before.x - CrewOnShip.x) * side > .15f && !Crew.IsSwimming,
                $"{name} resumed inward deck walk from {before:F2} to {CrewOnShip:F2}");
        }

        IEnumerator SwimToLadder(BoardingLadder ladder)
        {
            var outward = Vector3.right * Mathf.Sign(ladder.bottom.x);
            var staging = ladder.bottom + outward * 1.35f;
            for (int i = 0; i < 180 && Crew.IsSwimming && HorizontalTo(staging).magnitude > .14f; i++)
                yield return HoldKeys(1, TowardKey(HorizontalTo(staging)));
            Require(Crew.IsSwimming && HorizontalTo(staging).magnitude <= .14f,
                $"{ladder.name} lined up outside the ladder at {CrewOnShip:F2}");
            yield return SwimIntoGrip(ladder);
        }

        IEnumerator SwimIntoGrip(BoardingLadder ladder)
        {
            for (int i = 0; i < 80 && Crew.IsSwimming; i++)
                yield return HoldKeys(1, TowardKey(HorizontalTo(ladder.bottom)));
            Keyboard();
            Require(Crew.IsClimbing && !Crew.IsSwimming && HorizontalTo(ladder.bottom).magnitude < .8f,
                $"{ladder.name} touching the ladder while swimming caught it at {CrewOnShip:F2}");
        }

        IEnumerator FinishAscent(BoardingLadder ladder)
        {
            Keyboard(Key.W);
            for (int i = 0; i < 260 && Crew.IsClimbing; i++) yield return null;
            yield return HoldKeys(4);
            Require(!Crew.IsClimbing && !Crew.IsSwimming && !Crew.IsJumping && !Crew.IsDead &&
                Vector3.Distance(CrewOnShip, ladder.landing) < .62f,
                $"{ladder.name} W crossed the rail and reached the deck at {CrewOnShip:F2}, landing={ladder.landing:F2}");
            Require(Crew.Prompt.Contains("下梯"), $"{ladder.name} deck landing offered S to descend");
        }

        IEnumerator CheckSwimStroke(string name, Animation animation, SkinnedMeshRenderer body, Key swimKey)
        {
            var spine = SailorBone(body, "Spine");
            var leftHand = SailorBone(body, "Hand.L");
            var rightHand = SailorBone(body, "Hand.R");
            var swim = animation["Swim"];
            var baked = new Mesh();
            try
            {
                yield return HoldKeys(5, swimKey);
                Require(Crew.IsSwimming && IsPose(animation, "Swim") && SailorOnCamera(body),
                    $"{name} imported R06 Swim is active on the visible sailor after entry");
                float phase = swim.normalizedTime;
                Vector3 left = HandRelativeToSpine(spine, leftHand);
                Vector3 right = HandRelativeToSpine(spine, rightHand);
                var skin = SailorVertices(body, baked);

                // The standalone driver advances at 20 Hz, so 10 frames separate opposed strokes.
                yield return HoldKeys(10, swimKey);
                float halfPhase = Mathf.Repeat(swim.normalizedTime - phase, 1f);
                float leftTravel = Vector3.Distance(left, HandRelativeToSpine(spine, leftHand));
                float rightTravel = Vector3.Distance(right, HandRelativeToSpine(spine, rightHand));
                float halfSkin = SailorVertexTravel(body, baked, skin);
                Require(Crew.IsSwimming && SailorOnCamera(body) && Mathf.Abs(halfPhase - .5f) < .08f &&
                    leftTravel > .12f && rightTravel > .12f && halfSkin > .16f,
                    $"{name} half stroke advanced {halfPhase:F2}, hands moved {leftTravel:F2}/{rightTravel:F2} m relative to spine, skin moved {halfSkin:F2} m");
                yield return Photograph("swimming-opposite-" + type);

                yield return HoldKeys(10, swimKey);
                float phaseError = Mathf.Abs(Mathf.DeltaAngle(phase * 360f, swim.normalizedTime * 360f)) / 360f;
                float loopSkin = SailorVertexTravel(body, baked, skin);
                Require(Crew.IsSwimming && phaseError < .04f && loopSkin < .09f &&
                    Vector3.Distance(left, HandRelativeToSpine(spine, leftHand)) < .07f &&
                    Vector3.Distance(right, HandRelativeToSpine(spine, rightHand)) < .07f,
                    $"{name} one second of swimming returned to the same stroke, phase error {phaseError:F3}, skin error {loopSkin:F3} m");
                yield return Photograph("swimming-" + type);
                yield return HoldKeys(1, swimKey);
            }
            finally
            {
                Destroy(baked);
            }
        }

        static Vector3 HandRelativeToSpine(Transform spine, Transform hand) =>
            spine.InverseTransformDirection(hand.position - spine.position);

        Vector3 HorizontalTo(Vector3 local) => Vector3.ProjectOnPlane(
            Ship.transform.TransformPoint(local) - Crew.transform.position, Vector3.up);

        Key TowardKey(Vector3 direction)
        {
            var forward = Vector3.ProjectOnPlane(game.MainCamera.transform.forward, Vector3.up).normalized;
            float longitudinal = Vector3.Dot(direction, forward);
            float lateral = Vector3.Dot(direction, Vector3.Cross(Vector3.up, forward));
            if (Mathf.Abs(longitudinal) > Mathf.Abs(lateral)) return longitudinal >= 0 ? Key.W : Key.S;
            return lateral >= 0 ? Key.D : Key.A;
        }

        static bool IsPose(Animation animation, string name)
        {
            AnimationState state = animation[name];
            return state != null && state.enabled && state.weight >= .80f;
        }
    }
}
