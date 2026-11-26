using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Galleon
{
    public static class HoldRouteCheck
    {
        const float Step = .05f;

        // Follow the source models' walkable decks. The brigantine route includes both flights of stairs.
        public static IEnumerator Walk(NavalGame game, Action<bool, string, string> result)
        {
            var crew = game.Crew;
            var ship = game.Pirate;
            Vector3 start = ship.transform.InverseTransformPoint(ship.Rig.Spawn.position);
            if (Vector3.Distance(crew.transform.localPosition, start) > .3f)
                throw new InvalidOperationException($"Route must begin at the helm. Current {crew.transform.localPosition:F2}, expected {start:F2}");
            var stops = game.Pirate.Definition.Type == ShipType.Sloop
                ? new[] { new Vector3(.95f,3.1f,2), new Vector3(.95f,2.95f,4.8f), new Vector3(.97f,2.85f,7.2f),
                    new Vector3(.95f,2.85f,10.55f),new Vector3(.10f,2.86f,11.02f) }
                : new[] { new Vector3(.86f,6.45f,2.68f),new Vector3(1.60f,6.45f,3.41f),new Vector3(1.92f,6.45f,4.78f),
                    new Vector3(1.92f,6.45f,5.20f),new Vector3(1.92f,5.30f,6.82f),new Vector3(1.92f,4.15f,8.84f),
                    new Vector3(1.23f,4.15f,9.38f),new Vector3(1.23f,4.15f,16.50f),new Vector3(0,4.15f,16.50f),
                    new Vector3(0,2.90f,14.35f),new Vector3(0,1.65f,12.23f),new Vector3(.68f,1.65f,11.81f),
                    new Vector3(.99f,1.65f,11.31f),new Vector3(.99f,1.65f,10.38f),new Vector3(.77f,1.65f,9.13f) };
            foreach (var foot in stops)
                yield return WalkTo(game, foot, result);

            crew.Tick(default, Step);
            crew.Tick(new CrewCommand { UsePressed = true }, Step);
            bool dry = ship.Flooding.WaterLevel01 == 0 && ship.Flooding.ActiveLeaks == 0;
            result(dry && crew.StationName.Length == 0 && crew.Prompt.Contains("已排干"),
                "Continuous walk from spawn to the dry pump; E leaves the crew free", crew.Prompt);
            yield return WalkTo(game, stops[stops.Length - 2], result);
        }

        // Continue from the pump walk. Both decks have an aisle inboard of each battery.
        // Cross between them in the open bay, clear of the sloop's mast and brigantine's stairs.
        public static IEnumerator WalkRepairSites(NavalGame game, Action<bool, string, string> result)
        {
            var ship = game.Pirate;
            var crew = game.Crew;
            var flooding = ship.Flooding;
            var stands = ship.Definition.Data.repairStands;
            var start = crew.transform.localPosition;
            var initialLook = ship.transform.InverseTransformDirection(game.MainCamera.transform.forward);
            bool sloop = ship.Definition.Type == ShipType.Sloop;
            float aisleX = sloop ? .95f : 1.20f;
            var crossing = new Vector3(aisleX, start.y, sloop ? 10.95f : 11.60f);
            result(flooding.ActiveLeaks == 0 && flooding.WaterLevel01 == 0 && stands.Length == ship.Rig.LeakInside.Length,
                "Repair route starts on dry deck", $"sites={stands.Length}");

            foreach (int side in new[] { 1, -1 })
            {
                crossing.x = side * aisleX;
                yield return WalkTo(game, crossing, result);
                var sites = Enumerable.Range(0, stands.Length)
                    .Where(i => Mathf.Sign(stands[i].position.x) == side)
                    .OrderBy(i => stands[i].position.z);
                foreach (int index in sites)
                {
                    var stand = stands[index];
                    var aisle = new Vector3(crossing.x, stand.position.y, stand.position.z);
                    yield return WalkTo(game, aisle, result);
                    yield return WalkTo(game, stand.position, result);
                    var actual = crew.transform.localPosition;
                    var outside = ship.Rig.LeakOutside[index];
                    result(flooding.Hit(outside.position, outside.forward) && flooding.ActiveLeaks == 1 && flooding.IsLeaking(index),
                        "Opened only the approached repair site", stand.name);
                    Look(game, stand.forward, 28f);
                    yield return new WaitForEndOfFrame();
                    crew.Tick(default, Step);
                    result(Vector3.Distance(actual, stand.position) < .16f && crew.Prompt.Contains("E  修补船壳破口"),
                        "Walked to visible, operable breach " + index, $"{stand.name} foot={actual:F2} prompt={crew.Prompt}");
                    // The independent damage-control case exercises the timed E action.
                    result(flooding.Repair(index), "Cleared repair-route fixture", stand.name);
                    yield return WalkTo(game, aisle, result);
                }
                yield return WalkTo(game, crossing, result);
            }
            crossing.x = aisleX;
            yield return WalkTo(game, crossing, result);
            yield return WalkTo(game, start, result);
            Look(game, initialLook, -Mathf.Asin(initialLook.y) * Mathf.Rad2Deg);
            flooding.Pump(60);
            yield return new WaitForEndOfFrame();
            result(flooding.ActiveLeaks == 0 && flooding.WaterLevel01 == 0, "Finished repair route on dry deck", "");
        }

        static void Look(NavalGame game, Vector3 forward, float pitch)
        {
            var gaze = game.Pirate.transform.InverseTransformDirection(game.MainCamera.transform.forward);
            float currentYaw = Mathf.Atan2(gaze.x, gaze.z) * Mathf.Rad2Deg;
            float wantedYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float currentPitch = -Mathf.Asin(Mathf.Clamp(gaze.y, -1f, 1f)) * Mathf.Rad2Deg;
            game.Crew.Tick(new CrewCommand { Look = new Vector2(Mathf.DeltaAngle(currentYaw, wantedYaw) / .115f,
                (currentPitch - pitch) / .10f) }, Step);
        }

        internal static IEnumerator WalkTo(NavalGame game, Vector3 target, Action<bool, string, string> result)
        {
            for (int step = 0; step < 170; step++)
            {
                var current = game.Crew.transform.localPosition;
                var delta = target - current;
                delta.y = 0;
                if (delta.magnitude < .095f)
                {
                    result(Mathf.Abs(current.y-target.y)<.48f, "Walk reached " + target.ToString("F2"), "actual=" + current.ToString("F2"));
                    yield break;
                }
                var gaze = game.Pirate.transform.InverseTransformDirection(game.MainCamera.transform.forward);
                float yaw = Mathf.Atan2(gaze.x,gaze.z)*Mathf.Rad2Deg;
                var move = Quaternion.Euler(0,-yaw,0) * Vector3.ClampMagnitude(delta / (2.24f*Step),1);
                game.Crew.Tick(new CrewCommand { Move = new Vector2(move.x, move.z) }, Step);
                if (Vector3.Distance(current,game.Crew.transform.localPosition) > .5f)
                    throw new InvalidOperationException("route recovered or dropped " + current + " -> " + game.Crew.transform.localPosition);
                if (step % 3 == 0) yield return null;
            }
            throw new InvalidOperationException($"blocked walking to {target:F2}; stopped at {game.Crew.transform.localPosition:F2}");
        }

    }
}
