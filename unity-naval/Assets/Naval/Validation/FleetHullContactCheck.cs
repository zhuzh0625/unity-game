using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        const float NearHull = .85f;

        public static void BeginHullContact(NavalGame game, string output)
        {
            var check = game.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(game, output, "hull-contact-check", "Ship hull contact", check.RunHullContact());
        }

        IEnumerator RunHullContact()
        {
            yield return null;
            Require(game.IsInMenu && !Ship && !Crew, "starts at the fleet menu");
            foreach (var choice in new[] { ShipType.Sloop, ShipType.Brigantine })
            {
                type = choice;
                Click(game.Menu.GetShipButton(type));
                Click(game.Menu.StartButton);
                var navy = game.Navy;
                Require(!game.IsCombatTest && Ship && Ship.Definition.Type == type && navy &&
                    navy.Definition.Type == type && Crew, $"selected voyage loaded with two opposing {type} ships");

                // Keep the existing crew actors and run their normal lifecycle. Isolate company orders and cannon fire.
                using (new AcceptanceCompanyFixture(Ship, navy))
                {
                    Note("FIXTURE company AI orders and cannon fire isolated; both captains and all mates remain in the voyage");
                    var state = new UndamagedFleet(Ship, navy, Crew);
                    Require(state.IsUnchanged && state.CrewCount == Ship.Company.Capacity + navy.Company.Capacity,
                        $"both crews present at full health and holds dry, crew={state.CrewCount}");
                    Crew.ReadDeviceInput = false;
                    Require(CrewStationFit.TryGetPose(Ship, CrewPlayer.Role.Helm, -1, Ship.Rig.Helm,
                        Crew.transform.position, out var foot, out _), "existing helm stance fits the player captain");
                    Fixture(Ship.transform.InverseTransformPoint(foot));
                    Enter("舵轮");
                    Require(Ship.Anchored && Ship.SailStep == 0, "helm starts with anchor down and sail furled");
                    Tick(new CrewCommand { SailChange = 1 });
                    Require(!Ship.Anchored && Ship.SailStep == 1, "helm weighs anchor at half sail before the contact cases");

                    var probe = new ModelHullProbe(Ship, navy);
                    yield return MovingBows(probe, state);
                    yield return HeldAnchor(probe, state);
                    if (!Small)
                    {
                        yield return AlongsideGlance(probe, state);
                        yield return SteeringSternSweep(probe, state);
                    }
                    Require(state.IsUnchanged, $"all {state.CrewCount} crew health readings, breach counts and bilge levels unchanged");
                }
                game.ReturnToMenu();
                yield return null;
            }
            yield return CaptainApproach();
        }

        void PlaceContactCase(ModelHullProbe probe, string name, Vector3 playerPosition, float playerCourse,
            Vector3 navyPosition, float navyCourse, bool navyUnderway)
        {
            Ship.SetSailStep(0);
            game.Navy.SetSailStep(navyUnderway ? 2 : 0);
            Ship.PlaceAtSea(playerPosition, playerCourse);
            game.Navy.PlaceAtSea(navyPosition, navyCourse);
            Note($"FIXTURE {name} player={playerPosition:F2}/{playerCourse:F0} navy={navyPosition:F2}/{navyCourse:F0} " +
                $"navy anchor down={game.Navy.Anchored}; this is the only placement in this case");
            probe.Reset();
            float gap = probe.Sample();
            Require(gap > .4f && Ship.Anchored && Ship.SailStep == 0 && Ship.Speed < .01f &&
                game.Navy.Anchored == !navyUnderway && game.Navy.SailStep == (navyUnderway ? 2 : 0) && game.Navy.Speed < .01f,
                $"{name} starts stopped with visibly separate hulls, projected gap={gap:F3} m");
            Tick(new CrewCommand { SailChange = 1 });
            Require(!Ship.Anchored && Ship.SailStep == 1, $"{name} weighs player anchor and sets half sail at the helm");
            Tick(new CrewCommand { SailChange = 1 });
            Require(!Ship.Anchored && Ship.SailStep == 2, $"{name} begins under full player sail");
        }

        float MeasureContactFrame(ModelHullProbe probe, UndamagedFleet state, string name)
        {
            float gap = MeasureHullFrame(probe, name);
            if (!state.IsUnchanged)
                throw new InvalidOperationException($"{name}: crew, hull damage, bilge or shot count changed at frame {probe.Frames}");
            if (Crew.StationName != "舵轮")
                throw new InvalidOperationException($"{name}: captain left the helm at frame {probe.Frames}");
            return gap;
        }

        float MeasureHullFrame(ModelHullProbe probe, string name)
        {
            float gap = probe.Sample();
            if (probe.MaxOverlap > .005f)
                throw new InvalidOperationException($"{name}: level planform overlap {probe.MaxOverlap:F5} m² " +
                    $"at frame {probe.Frames} player={HullCentre(Ship):F2}/{Ship.Course:F1} " +
                    $"navy={HullCentre(game.Navy):F2}/{game.Navy.Course:F1}");
            return gap;
        }

        IEnumerator MovingBows(ModelHullProbe probe, UndamagedFleet state)
        {
            const string name = "moving bow-to-bow";
            float startZ = probe.PlayerReach(0f, Vector2.up) + probe.NavyReach(180f, Vector2.down) + 9f;
            PlaceContactCase(probe, name, Vector3.zero, 0f, new Vector3(0f, 0f, startZ), 180f, true);
            float nearTime = 0f;
            for (int frame = 0; frame < 280 && nearTime < 1f; frame++)
            {
                yield return null;
                nearTime = MeasureContactFrame(probe, state, name) <= NearHull ? nearTime + Time.deltaTime : 0f;
                if (HullCentre(Ship).y >= HullCentre(game.Navy).y)
                    throw new InvalidOperationException($"{name}: bows passed through each other at frame {probe.Frames}");
            }
            float playerTravel = HullCentre(Ship).y;
            float navyTravel = startZ - HullCentre(game.Navy).y;
            Require(!Ship.Anchored && !game.Navy.Anchored && playerTravel > 2f && navyTravel > 2f && nearTime >= 1f,
                $"{name} both ships advanced to contact, player={playerTravel:F2} m navy={navyTravel:F2} m " +
                $"near={nearTime:F2} s {probe.Evidence}");
            VerifyPlankingPoses(probe, name);
        }

        IEnumerator HeldAnchor(ModelHullProbe probe, UndamagedFleet state)
        {
            const string name = "bow pressing anchored broadside";
            float startZ = -(probe.PlayerReach(0f, Vector2.up) + probe.NavyReach(90f, Vector2.down) + 5f);
            PlaceContactCase(probe, name, new Vector3(0f, 0f, startZ), 0f, Vector3.zero, 90f, false);
            float held = 0f, startHoldZ = 0f, maxPush = 0f, anchorTravel = 0f, anchorTurn = 0f;
            for (int frame = 0; frame < 340 && held < 6f; frame++)
            {
                yield return null;
                float gap = MeasureContactFrame(probe, state, name);
                anchorTravel = Mathf.Max(anchorTravel, HullCentre(game.Navy).magnitude);
                anchorTurn = Mathf.Max(anchorTurn, Mathf.Abs(Mathf.DeltaAngle(90f, game.Navy.Course)));
                if (gap > NearHull)
                {
                    held = maxPush = 0f;
                    continue;
                }
                if (held == 0f) startHoldZ = HullCentre(Ship).y;
                held += Time.deltaTime;
                maxPush = Mathf.Max(maxPush, Mathf.Abs(HullCentre(Ship).y - startHoldZ));
            }
            Require(held >= 6f && maxPush < .8f && Ship.SailStep == 2 && !Ship.Anchored &&
                game.Navy.Anchored && game.Navy.Speed < .01f && anchorTravel < .03f && anchorTurn < .03f,
                $"{name} held={held:F2} s push={maxPush:F3} m anchor drift={anchorTravel:F3} m/{anchorTurn:F3}° " +
                probe.Evidence);
            VerifyPlankingPoses(probe, name);
            Tick(new CrewCommand { SailChange = -1 });
            Require(!Ship.Anchored && Ship.SailStep == 1, "helm reefs to half sail after holding contact");
            Tick(new CrewCommand { SailChange = -1 });
            Require(Ship.Anchored && Ship.SailStep == 0, "helm furls sail and drops player anchor after holding contact");
        }

        IEnumerator AlongsideGlance(ModelHullProbe probe, UndamagedFleet state)
        {
            const string name = "oblique glance and separation";
            const float heading = 12f;
            float startX = -(probe.PlayerReach(heading, Vector2.right) + probe.NavyReach(0f, Vector2.left) + .7f);
            PlaceContactCase(probe, name, new Vector3(startX, 0f, -10.4f), heading, Vector3.zero, 0f, false);
            float? firstNearZ = null;
            float alongside = 0f, gap = 0f;
            for (int frame = 0; frame < 600; frame++)
            {
                Tick(new CrewCommand { Move = alongside >= 4f ? Vector2.left : Vector2.zero });
                yield return null;
                gap = MeasureContactFrame(probe, state, name);
                if (gap <= NearHull)
                {
                    float z = HullCentre(Ship).y;
                    firstNearZ ??= z;
                    alongside = Mathf.Max(alongside, z - firstNearZ.Value);
                }
                if (alongside >= 4f && gap >= 2f) break;
            }
            Require(alongside >= 4f && gap >= 2f && Mathf.Abs(Mathf.DeltaAngle(heading, Ship.Course)) > 4f,
                $"{name} travelled {alongside:F2} m tangentially near the hull before steering clear to {gap:F2} m " +
                $"course={Ship.Course:F1}° {probe.Evidence}");
            VerifyPlankingPoses(probe, name);
        }

        IEnumerator SteeringSternSweep(ModelHullProbe probe, UndamagedFleet state)
        {
            const string name = "turn-away stern sweep";
            float startX = -(probe.PlayerReach(0f, Vector2.right) + probe.NavyReach(0f, Vector2.left) + .9f);
            PlaceContactCase(probe, name, new Vector3(startX, 0f, 0f), 0f, new Vector3(0f, 0f, -6f), 0f, false);
            float closestSternGap = float.PositiveInfinity, sweptAngle = 0f;
            for (int frame = 0; frame < 170; frame++)
            {
                Tick(new CrewCommand { Move = Vector2.left });
                yield return null;
                float gap = MeasureContactFrame(probe, state, name);
                float turn = -Mathf.DeltaAngle(0f, Ship.Course);
                if (turn > 3f && Ship.Rudder < -.4f && probe.PlayerClosestZ < -Ship.Definition.Length * .15f &&
                    gap < closestSternGap)
                {
                    closestSternGap = gap;
                    sweptAngle = turn;
                }
            }
            Require(closestSternGap <= NearHull,
                $"{name} stern approached to {closestSternGap:F3} m during {sweptAngle:F1}° of turn {probe.Evidence}");
            VerifyPlankingPoses(probe, name);
        }

        IEnumerator CaptainApproach()
        {
            const string name = "navy captain approaches anchored player";
            type = ShipType.Brigantine;
            Click(game.Menu.GetShipButton(type));
            Click(game.Menu.StartButton);
            var navy = game.Navy;
            Require(Ship && navy && Ship.Company.enabled && navy.Company.enabled && navy.Company.Captain is CrewMate,
                "normal voyage keeps both companies and the actual navy captain active");
            Crew.ReadDeviceInput = false;
            var captain = (CrewMate)navy.Company.Captain;
            var probe = new ModelHullProbe(Ship, navy);
            const float playerCourse = 90f, navyCourse = 0f;
            float startZ = -(probe.NavyReach(navyCourse, Vector2.up) + probe.PlayerReach(playerCourse, Vector2.down) + 1.55f);
            var navyPosition = new Vector3(-1.7f, 0f, startZ);
            Ship.PlaceAtSea(Vector3.zero, playerCourse);
            navy.PlaceAtSea(navyPosition, navyCourse);
            Note($"FIXTURE {name} player=(0,0,0)/{playerCourse:F0} navy={navyPosition:F2}/{navyCourse:F0}; " +
                "one initial placement, both anchors down, normal crew and gunfire enabled");
            probe.Reset();
            float startGap = MeasureHullFrame(probe, name);
            Require(Ship.Anchored && Ship.SailStep == 0 && navy.Anchored && navy.SailStep == 0 && startGap > 1f,
                $"AI approach starts separated by {startGap:F3} m with both sails furled");

            bool captainAtWheel = false, sailed = false;
            float maxRudder = 0f, maxTurn = 0f, playerDrift = 0f, gap = startGap;
            float nearTime = 0f;
            for (int frame = 0; frame < 360 && nearTime < .65f; frame++)
            {
                yield return null;
                gap = MeasureHullFrame(probe, name);
                if (captain.Job == CrewPlayer.Role.Helm && captain.AtPost)
                {
                    captainAtWheel = true;
                    sailed |= !navy.Anchored && navy.SailStep > 0 && navy.Speed > .5f;
                    maxRudder = Mathf.Max(maxRudder, Mathf.Abs(navy.Rudder));
                    maxTurn = Mathf.Max(maxTurn, Mathf.Abs(Mathf.DeltaAngle(navyCourse, navy.Course)));
                }
                playerDrift = Mathf.Max(playerDrift, HullCentre(Ship).magnitude);
                nearTime = gap <= NearHull ? nearTime + Time.deltaTime : 0f;
            }
            float navyAdvance = HullCentre(navy).y - startZ;
            Require(captainAtWheel && sailed && maxRudder > .2f && maxTurn > 3f && navyAdvance > 1f && nearTime >= .65f &&
                Ship.Anchored && playerDrift < .03f,
                $"{name} helm={captainAtWheel} under sail={sailed} rudder={maxRudder:F2} turn={maxTurn:F1}° " +
                $"advance={navyAdvance:F2} m near={nearTime:F2} s player drift={playerDrift:F3} m " +
                $"AI shots player/navy={Ship.Company.ShotsFired}/{navy.Company.ShotsFired} {probe.Evidence}");
            VerifyPlankingPoses(probe, name);
        }

        void VerifyPlankingPoses(ModelHullProbe probe, string name)
        {
            var (count, area, tilt) = probe.VerifyPosedOutline();
            Require(count >= 2 && area <= .005f,
                $"{name} actual pitched/rolled planking frames={count} max tilt={tilt:F2}° max overlap={area:F5} m²");
        }

        static Vector2 HullCentre(NavalShip ship)
        {
            var point = ship.transform.TransformPoint(0f, 0f, ship.Definition.PivotZ);
            return new Vector2(point.x, point.z);
        }

        sealed class UndamagedFleet
        {
            readonly NavalShip player, navy;
            readonly List<CrewDamageTarget> crew = new();
            public int CrewCount => crew.Count;

            public UndamagedFleet(NavalShip player, NavalShip navy, CrewPlayer captain)
            {
                this.player = player;
                this.navy = navy;
                crew.Add(captain);
                crew.AddRange(player.Company.Mates);
                crew.AddRange(navy.Company.Mates);
            }

            public bool IsUnchanged
            {
                get
                {
                    foreach (var sailor in crew)
                        if (!sailor || sailor.Health != CrewHealth.Maximum) return false;
                    return DryAndQuiet(player) && DryAndQuiet(navy);
                }
            }

            static bool DryAndQuiet(NavalShip ship) => ship && ship.Flooding.ActiveLeaks == 0 &&
                ship.Flooding.WaterLevel01 == 0 && ship.Flooding.WaterHeight == ship.Flooding.DryHeight &&
                ship.Company.ShotsFired == 0 && ship.HitsLanded == 0;
        }

        sealed class ModelHullProbe
        {
            const int PoseWindow = 8, MaxPosedChecks = 6;
            readonly struct PoseSample
            {
                public readonly int Frame;
                public readonly float Gap, Tilt;
                public readonly Matrix4x4 PlayerPose, NavyPose;

                public PoseSample(int frame, float gap, NavalShip player, NavalShip navy)
                {
                    Frame = frame;
                    Gap = gap;
                    PlayerPose = player.transform.localToWorldMatrix;
                    NavyPose = navy.transform.localToWorldMatrix;
                    Tilt = Mathf.Max(Vector3.Angle(player.transform.up, Vector3.up),
                        Vector3.Angle(navy.transform.up, Vector3.up));
                }
            }

            readonly NavalShip player, navy;
            readonly Vector3[] playerPlanking, navyPlanking;
            readonly Vector2[] playerLocal, navyLocal, playerWorld, navyWorld;
            readonly Vector2[] clipBufferA, clipBufferB;
            readonly List<PoseSample> nearPoses = new();
            float minGap, nearestSquared;
            Vector2 playerClosest;
            public int Frames { get; private set; }
            public float MaxOverlap { get; private set; }
            public float PlayerClosestZ { get; private set; }
            public string Evidence => $"samples={Frames} min gap={minGap:F3} m max overlap area={MaxOverlap:F5} m²";

            public ModelHullProbe(NavalShip player, NavalShip navy)
            {
                this.player = player;
                this.navy = navy;
                playerPlanking = ReadPlanking(player);
                navyPlanking = ReadPlanking(navy);
                playerLocal = MakeOutline(playerPlanking, Matrix4x4.identity, new Vector2(0f, player.Definition.PivotZ));
                navyLocal = MakeOutline(navyPlanking, Matrix4x4.identity, new Vector2(0f, navy.Definition.PivotZ));
                playerWorld = new Vector2[playerLocal.Length];
                navyWorld = new Vector2[navyLocal.Length];
                clipBufferA = new Vector2[playerLocal.Length + navyLocal.Length + 4];
                clipBufferB = new Vector2[clipBufferA.Length];
            }

            public void Reset()
            {
                Frames = 0;
                MaxOverlap = 0f;
                minGap = float.PositiveInfinity;
                nearPoses.Clear();
            }

            public float PlayerReach(float course, Vector2 direction) => Reach(playerLocal, course, direction);
            public float NavyReach(float course, Vector2 direction) => Reach(navyLocal, course, direction);

            static float Reach(Vector2[] outline, float course, Vector2 direction)
            {
                float radians = course * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians), farthest = float.NegativeInfinity;
                foreach (var point in outline)
                    farthest = Mathf.Max(farthest, Vector2.Dot(Rotate(point, cos, sin), direction));
                return farthest;
            }

            public float Sample()
            {
                // Measure the level planform for every completed frame, storing actual poses as the hulls get close.
                var origin = HullCentre(player);
                Project(playerLocal, playerWorld, player.Course, Vector2.zero);
                Project(navyLocal, navyWorld, navy.Course, HullCentre(navy) - origin);
                float area = IntersectionArea(playerWorld, navyWorld, clipBufferA, clipBufferB);
                MaxOverlap = Mathf.Max(MaxOverlap, area);
                nearestSquared = float.PositiveInfinity;
                MeasureEdges(playerWorld, navyWorld, true);
                MeasureEdges(navyWorld, playerWorld, false);
                float radians = -player.Course * Mathf.Deg2Rad;
                PlayerClosestZ = Rotate(playerClosest, Mathf.Cos(radians), Mathf.Sin(radians)).y;
                float gap = area > .0001f ? 0f : Mathf.Sqrt(nearestSquared);
                minGap = Mathf.Min(minGap, gap);
                if (gap <= NearHull) RecordNearPose(gap);
                Frames++;
                return gap;
            }

            void RecordNearPose(float gap)
            {
                int last = nearPoses.Count - 1;
                if (last < 0 || Frames / PoseWindow != nearPoses[last].Frame / PoseWindow)
                    nearPoses.Add(new PoseSample(Frames, gap, player, navy));
                else if (gap < nearPoses[last].Gap)
                    nearPoses[last] = new PoseSample(Frames, gap, player, navy);
            }

            public (int count, float overlap, float tilt) VerifyPosedOutline()
            {
                // Take the nearest rendered pose from each time window, then check the closest of those windows.
                // All planking vertices are projected with the tilted render transforms captured on those frames.
                nearPoses.Sort((a, b) => a.Gap.CompareTo(b.Gap));
                int count = Mathf.Min(nearPoses.Count, MaxPosedChecks);
                float overlap = 0f, tilt = 0f;
                for (int i = 0; i < count; i++)
                {
                    var pose = nearPoses[i];
                    var playerOrigin = new Vector2(pose.PlayerPose.m03, pose.PlayerPose.m23);
                    var first = MakeOutline(playerPlanking, pose.PlayerPose, playerOrigin);
                    var second = MakeOutline(navyPlanking, pose.NavyPose, playerOrigin);
                    var a = new Vector2[first.Length + second.Length + 4];
                    var b = new Vector2[a.Length];
                    overlap = Mathf.Max(overlap, IntersectionArea(first, second, a, b));
                    tilt = Mathf.Max(tilt, pose.Tilt);
                }
                return (count, overlap, tilt);
            }

            static Vector3[] ReadPlanking(NavalShip ship)
            {
                var points = new HashSet<Vector3>();
                foreach (var part in ship.Rig.OuterPlanking)
                {
                    if (!part.sharedMesh.isReadable)
                        throw new InvalidOperationException("Cannot read hull planking mesh " + part.name);
                    var relative = ship.transform.worldToLocalMatrix * part.transform.localToWorldMatrix;
                    foreach (var vertex in part.sharedMesh.vertices)
                        points.Add(relative.MultiplyPoint3x4(vertex));
                }
                if (points.Count < 3) throw new InvalidOperationException("Missing hull planking geometry " + ship.name);
                var result = new Vector3[points.Count];
                points.CopyTo(result);
                return result;
            }

            static Vector2[] MakeOutline(Vector3[] planking, Matrix4x4 pose, Vector2 origin)
            {
                var points = new Vector2[planking.Length];
                for (int i = 0; i < planking.Length; i++)
                {
                    var point = pose.MultiplyPoint3x4(planking[i]);
                    points[i] = new Vector2(point.x - origin.x, point.z - origin.y);
                }
                Array.Sort(points, (a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                var outline = new List<Vector2>();
                foreach (var point in points) AppendCorner(outline, point, 0);
                int lower = outline.Count - 1;
                for (int i = points.Length - 2; i >= 0; i--) AppendCorner(outline, points[i], lower);
                if (outline.Count <= 3) throw new InvalidOperationException("Degenerate hull planking outline");
                outline.RemoveAt(outline.Count - 1);
                return outline.ToArray();
            }

            static void AppendCorner(List<Vector2> outline, Vector2 point, int first)
            {
                while (outline.Count >= first + 2 &&
                    Cross(outline[outline.Count - 1] - outline[outline.Count - 2], point - outline[outline.Count - 1]) <= 0f)
                    outline.RemoveAt(outline.Count - 1);
                outline.Add(point);
            }

            static void Project(Vector2[] local, Vector2[] world, float course, Vector2 offset)
            {
                float radians = course * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
                for (int i = 0; i < local.Length; i++) world[i] = Rotate(local[i], cos, sin) + offset;
            }

            static Vector2 Rotate(Vector2 point, float cos, float sin) =>
                new(point.x * cos + point.y * sin, point.y * cos - point.x * sin);

            static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

            static float IntersectionArea(Vector2[] first, Vector2[] second, Vector2[] input, Vector2[] output)
            {
                // Clip against each enemy edge and measure the resulting polygon, independent of contact resolution.
                int count = first.Length;
                Array.Copy(first, input, count);
                for (int edge = 0; edge < second.Length && count >= 3; edge++)
                {
                    var a = second[edge];
                    var line = second[(edge + 1) % second.Length] - a;
                    var previous = input[count - 1];
                    float prior = Cross(line, previous - a);
                    int written = 0;
                    for (int i = 0; i < count; i++)
                    {
                        var current = input[i];
                        float side = Cross(line, current - a);
                        if ((side > 0f && prior < 0f) || (side < 0f && prior > 0f))
                            output[written++] = Vector2.LerpUnclamped(previous, current, prior / (prior - side));
                        if (side >= 0f) output[written++] = current;
                        previous = current;
                        prior = side;
                    }
                    count = written;
                    (input, output) = (output, input);
                }
                if (count < 3) return 0f;
                float area = 0f;
                for (int i = 1; i + 1 < count; i++)
                    area += Cross(input[i] - input[0], input[i + 1] - input[0]);
                return Mathf.Abs(area) * .5f;
            }

            void MeasureEdges(Vector2[] points, Vector2[] outline, bool playerVertices)
            {
                for (int edge = 0; edge < outline.Length; edge++)
                {
                    var start = outline[edge];
                    var side = outline[(edge + 1) % outline.Length] - start;
                    float lengthSquared = side.sqrMagnitude;
                    if (lengthSquared <= 0f) continue;
                    foreach (var vertex in points)
                    {
                        var projection = start + side * Mathf.Clamp01(Vector2.Dot(vertex - start, side) / lengthSquared);
                        float square = (vertex - projection).sqrMagnitude;
                        if (square >= nearestSquared) continue;
                        nearestSquared = square;
                        playerClosest = playerVertices ? vertex : projection;
                    }
                }
            }
        }
    }
}
