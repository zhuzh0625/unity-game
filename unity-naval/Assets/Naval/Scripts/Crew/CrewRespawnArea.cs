using UnityEngine;

namespace Galleon
{
    internal static class CrewRespawnArea
    {
        // Hull-local metres. On the brigantine use the two aisles outside the mast.
        // The sloop's compartment has an open bay forward of its companionway.
        static readonly Rect SloopBay = Rect.MinMaxRect(-.85f, 8.98f, .85f, 10.00f);
        static readonly Rect BrigAisle = Rect.MinMaxRect(.70f, 10.65f, 1.05f, 11.80f);

        internal static bool TryPickLocalFoot(NavalShip ship, out Vector3 foot)
        {
            foot = default;
            if (!ship || !ship.Rig) return false;
            bool sloop = ship.Definition.Type == ShipType.Sloop;
            var deck = ship.Rig.transform.Find(sloop ? "Collision_CabinFloor" : "Collision_Decks");
            if (!deck || !deck.TryGetComponent<MeshCollider>(out var floor)) return false;
            float height = sloop ? floor.sharedMesh.bounds.max.y : ship.Definition.Data.lowerDeck;
            var bay = sloop ? SloopBay : BrigAisle;
            Physics.SyncTransforms();
            for (int i = 0; i < 12; i++)
            {
                float x = UnityEngine.Random.Range(bay.xMin, bay.xMax);
                if (!sloop && UnityEngine.Random.value < .5f) x = -x;
                float z = UnityEngine.Random.Range(bay.yMin, bay.yMax);
                if (TryStand(ship.transform, floor, new Vector3(x, height, z), out foot)) return true;
            }

            // Even if random draws keep landing on a temporary obstruction,
            // search across the whole bay and validate every fallback the same way.
            for (int row = 0; row < 5; row++)
            for (int column = 0; column < 5; column++)
            {
                float z = Mathf.Lerp(bay.yMin, bay.yMax, row / 4f);
                float x = Mathf.Lerp(bay.xMin, bay.xMax, column / 4f);
                if (TryStand(ship.transform, floor, new Vector3(x, height, z), out foot)) return true;
                if (!sloop && TryStand(ship.transform, floor, new Vector3(-x, height, z), out foot)) return true;
            }
            return false;
        }

        static bool TryStand(Transform hull, MeshCollider floor, Vector3 local, out Vector3 foot)
        {
            foot = default;
            var up = hull.up;
            var origin = hull.TransformPoint(local) + up * .14f;
            if (!floor.Raycast(new Ray(origin, -up), out var hit, .28f) ||
                Vector3.Dot(hit.normal, up) < .95f) return false;

            var standing = hit.point + up * (2f * CrewPlayer.SkinWidth);
            if (Physics.CheckCapsule(standing + up * CrewPlayer.BodyRadius,
                    standing + up * (CrewPlayer.BodyHeight - CrewPlayer.BodyRadius), CrewPlayer.BodyRadius,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            foot = hull.InverseTransformPoint(standing);
            return true;
        }
    }
}
