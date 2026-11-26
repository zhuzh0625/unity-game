using UnityEngine;

namespace Galleon
{
    internal static class CrewStationFit
    {
        internal static bool TryGetPose(NavalShip ship, CrewPlayer.Role role, int index, Transform target,
            Vector3 crewFoot, out Vector3 footWorld, out Vector3 forwardWorld)
        {
            footWorld = crewFoot;
            forwardWorld = Vector3.zero;
            if (!ship || !target) return false;

            Transform hull = ship.transform;
            ShipMeasurements data = ship.Definition.Data;
            bool sloop = ship.Definition.Type == ShipType.Sloop;
            Vector3 mark = hull.InverseTransformPoint(target.position);
            Vector3 forward = Planar(hull.InverseTransformDirection(target.forward));
            Vector3 foot;
            switch (role)
            {
                case CrewPlayer.Role.Helm:
                    // The sloop's interaction marker is .10 m aft of the wheel pivot. Its
                    // cambered deck is .859 m below the hub at the authored .60 m stance.
                    foot = mark - forward * (sloop ? .50f : .68f);
                    foot.y = sloop ? mark.y - .859f : data.spawn.position.y;
                    break;
                case CrewPlayer.Role.Pump:
                    // The exported axes have opposite signs, but both grips extend aft.
                    // The baked pose stands beside the grip with both hands along the lever.
                    foot = mark + new Vector3(.45f, 0f, sloop ? .030f : -.015f);
                    foot.y = sloop ? mark.y - 1.269f : data.lowerDeck;
                    forward = Vector3.left;
                    break;
                case CrewPlayer.Role.Gun:
                    bool upper = mark.y > data.mainDeck;
                    foot = mark - forward * (sloop ? .525f : upper ? .4882f : .4432f);
                    if (sloop)
                    {
                        // The sloop exporter marks each gun .32 m above the deck edge and
                        // 1.01 m inboard. Restore the local deck camber under the feet.
                        float breadth = Mathf.Abs(mark.x) + 1.01f;
                        float across = foot.x / breadth;
                        foot.y = mark.y - .32f + .07f * (1f - across * across);
                    }
                    else foot.y = upper ? data.mainDeck : data.lowerDeck;
                    break;
                case CrewPlayer.Role.Leak:
                    if (index < 0 || index >= data.repairStands.Length) return false;
                    foot = data.repairStands[index].position;
                    forward = Planar(mark - foot);
                    Vector3 candidate = mark - forward * .61f;
                    candidate.y = foot.y;
                    if (CrewPlayer.CanStandAndRepair(hull, candidate, target))
                        foot = candidate;
                    break;
                default:
                    return false;
            }

            if (forward.sqrMagnitude < .5f) return false;
            footWorld = hull.TransformPoint(foot);
            forwardWorld = hull.TransformDirection(forward);
            return true;
        }

        static Vector3 Planar(Vector3 direction)
        {
            direction.y = 0f;
            return direction.normalized;
        }
    }
}
