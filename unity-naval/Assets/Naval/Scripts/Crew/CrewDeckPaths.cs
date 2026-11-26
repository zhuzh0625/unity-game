using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    // All route points are feet in hull space. The ship can translate, heel and turn
    // without moving or rebaking its navigation data.
    internal sealed class CrewDeckPaths
    {
        const float Cell = .30f;
        const float Radius = CrewPlayer.BodyRadius - CrewPlayer.SkinWidth;
        const int Mask = Physics.DefaultRaycastLayers;

        sealed class Node
        {
            internal Vector3 foot;
            internal readonly List<Node> neighbours = new(8);
            internal int search;
            internal Node previous;
        }

        readonly NavalShip ship;
        readonly Transform hull;
        readonly Dictionary<Vector2Int, List<Node>> cells = new();
        readonly List<Node> nodes = new();
        readonly Queue<Node> frontier = new();
        readonly Dictionary<int, Vector3> berths = new();
        readonly List<Vector3> measuredRoute = new();
        int search;

        internal CrewDeckPaths(NavalShip ship)
        {
            this.ship = ship;
            hull = ship.transform;
            Physics.SyncTransforms();
            bool small = ship.Definition.Type == ShipType.Sloop;
            var data = ship.Definition.Data;
            var floors = new List<MeshCollider>();
            foreach (Transform part in ship.Rig.transform)
                if ((part.name == "Collision_Decks" || part.name == "Collision_WeatherDeck" ||
                    part.name == "Collision_Stairs" || part.name == "Collision_Companionway" ||
                    part.name == "Collision_CabinFloor") && part.TryGetComponent(out MeshCollider mesh))
                    floors.Add(mesh);

            int half = Mathf.CeilToInt(data.beam * .5f / Cell);
            for (int z = 0; z <= Mathf.CeilToInt(data.length / Cell); z++)
            for (int x = -half; x <= half; x++)
            {
                var cell = new Vector2Int(x, z);
                foreach (var floor in floors)
                {
                    AddFloor(cell, floor, data.maxHullHeight + .45f);
                    // The brigantine exports all three decks in the same collider.
                    if (!small && floor.name == "Collision_Decks")
                    {
                        AddFloor(cell, floor, data.mainDeck + .30f);
                        AddFloor(cell, floor, data.lowerDeck + .30f);
                    }
                }
            }
            foreach (var entry in cells)
            {
                Connect(entry.Key + Vector2Int.right, entry.Value);
                Connect(entry.Key + Vector2Int.up, entry.Value);
                Connect(entry.Key + new Vector2Int(-1, 1), entry.Value);
                Connect(entry.Key + Vector2Int.one, entry.Value);
            }
        }

        void AddFloor(Vector2Int cell, MeshCollider floor, float height)
        {
            var origin = new Vector3(cell.x * Cell, height, cell.y * Cell);
            if (!floor.Raycast(new Ray(hull.TransformPoint(origin), -hull.up), out var hit, height + .5f) ||
                Vector3.Dot(hit.normal, hull.up) < .48f) return;
            Vector3 foot = hull.InverseTransformPoint(hit.point);
            if (cells.TryGetValue(cell, out var column))
                foreach (var other in column)
                    if (Mathf.Abs(other.foot.y - foot.y) < .08f) return;
            Capsule(foot, out var bottom, out var top);
            if (Physics.CheckCapsule(bottom, top, Radius, Mask, QueryTriggerInteraction.Ignore)) return;
            if (column == null) cells.Add(cell, column = new List<Node>(2));
            var node = new Node { foot = foot };
            nodes.Add(node);
            column.Add(node);
        }

        void Capsule(Vector3 localFoot, out Vector3 bottom, out Vector3 top)
        {
            var foot = hull.TransformPoint(localFoot);
            // The sloop companionway rises 1.80 m in 1.17 m. The sphere's front
            // needs .165 m of clearance on that incline before it can travel uphill.
            bottom = foot + hull.up * (Radius + .18f);
            top = foot + hull.up * (CrewPlayer.BodyHeight - Radius);
        }

        void Connect(Vector2Int key, List<Node> from)
        {
            if (!cells.TryGetValue(key, out var to)) return;
            foreach (var a in from)
            foreach (var b in to)
            {
                if (Mathf.Abs(a.foot.y - b.foot.y) > Cell * 1.65f || !ClearPassage(a.foot, b.foot)) continue;
                a.neighbours.Add(b);
                b.neighbours.Add(a);
            }
        }

        bool ClearPassage(Vector3 from, Vector3 to)
        {
            var delta = hull.TransformDirection(to - from);
            if (delta.sqrMagnitude < .00001f) return true;
            Capsule(from, out var bottom, out var top);
            return !Physics.CapsuleCast(bottom, top, Radius, delta.normalized, delta.magnitude,
                Mask, QueryTriggerInteraction.Ignore);
        }

        Node Nearest(Vector3 foot, float radius, bool requirePassage)
        {
            int x = Mathf.RoundToInt(foot.x / Cell), z = Mathf.RoundToInt(foot.z / Cell);
            int reach = Mathf.CeilToInt(radius / Cell);
            float best = radius * radius;
            Node found = null;
            for (int i = x - reach; i <= x + reach; i++)
            for (int j = z - reach; j <= z + reach; j++)
            {
                if (!cells.TryGetValue(new Vector2Int(i, j), out var column)) continue;
                foreach (var node in column)
                {
                    float d = (node.foot - foot).sqrMagnitude;
                    if (node.neighbours.Count == 0 || d >= best || Mathf.Abs(node.foot.y - foot.y) > .42f ||
                        (requirePassage && !ClearPassage(node.foot, foot))) continue;
                    found = node;
                    best = d;
                }
            }
            return found;
        }

        internal bool TryRoute(Vector3 from, Vector3 to, List<Vector3> route)
        {
            var start = Nearest(from, .69f, true);
            var end = Nearest(to, .69f, true);
            if (start == null || end == null) return false;
            ++search;
            frontier.Clear();
            frontier.Enqueue(start);
            start.search = search;
            start.previous = null;
            while (frontier.Count > 0)
            {
                var node = frontier.Dequeue();
                if (node == end) break;
                foreach (var next in node.neighbours)
                {
                    if (next.search == search) continue;
                    next.search = search;
                    next.previous = node;
                    frontier.Enqueue(next);
                }
            }
            if (end.search != search) return false;
            route.Clear();
            for (var node = end; node != null; node = node.previous) route.Add(node.foot);
            route.Reverse();
            route.Add(to);
            return true;
        }

        internal float WalkingDistance(Vector3 from, Vector3 to)
        {
            if (!TryRoute(from, to, measuredRoute)) return float.PositiveInfinity;
            float distance = 0f;
            foreach (var point in measuredRoute)
            {
                distance += Vector3.Distance(from, point);
                from = point;
            }
            return distance;
        }

        internal Vector3 Spawn(int slot, CrewDuty duty)
        {
            if (duty == CrewDuty.Captain && CrewStationFit.TryGetPose(ship, CrewPlayer.Role.Helm, -1, ship.Rig.Helm,
                ship.Rig.Spawn.position, out var foot, out _)) return hull.InverseTransformPoint(foot);
            return Berth(slot);
        }

        internal Vector3 Berth(int slot)
        {
            if (berths.TryGetValue(slot, out var existing)) return existing;
            bool small = ship.Definition.Type == ShipType.Sloop;
            float z = small ? 4.9f + (slot / 2 % 3) * 4.3f : 5.5f + (slot / 2 % 6) * 2.8f;
            float deck = small ? ship.Definition.Data.mainDeck + .13f : ship.Definition.Data.lowerDeck;
            Vector3 wanted = new((slot % 2 == 0 ? 1 : -1) * ship.Definition.Beam * .32f, deck, z);
            if (!small && (slot == 8 || slot == 9))
            {
                // The brigantine has two weather-deck gunners per watch.
                wanted.y = deck = ship.Definition.Data.mainDeck;
                wanted.z = slot == 8 ? 10.3f : 13.1f;
            }
            if (!small && slot == 11)
            {
                // One damage-control hand keeps the upper watch clear of the wheel
                // and stair landing; the other starts by the lower battery.
                wanted = new Vector3(-1.45f, ship.Definition.Data.spawn.position.y, 2.25f);
                deck = wanted.y;
            }
            float best = float.PositiveInfinity;
            Vector3 result = ship.Definition.Data.spawn.position;
            foreach (var node in nodes)
            {
                float distance = (node.foot - wanted).sqrMagnitude;
                if (distance >= best || node.neighbours.Count < 3 || Mathf.Abs(node.foot.y - deck) > .65f) continue;
                bool taken = false;
                foreach (var other in berths.Values)
                    if ((other - node.foot).sqrMagnitude < .55f * .55f) { taken = true; break; }
                if (taken) continue;
                result = node.foot;
                best = distance;
            }
            berths.Add(slot, result);
            return result;
        }
    }
}
