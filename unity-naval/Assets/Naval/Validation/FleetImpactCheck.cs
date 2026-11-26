using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        IEnumerator ApproachRepairSite(int index)
        {
            var stand = Ship.Definition.Data.repairStands[index];
            Fixture(stand.position);
            yield return Frames(3);
            Vector3 current = Ship.transform.InverseTransformDirection(game.MainCamera.transform.forward);
            float heading = Mathf.Atan2(current.x, current.z) * Mathf.Rad2Deg;
            float wanted = Mathf.Atan2(stand.forward.x, stand.forward.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(current.y, -1, 1)) * Mathf.Rad2Deg;
            Tick(new CrewCommand { Look = new Vector2(Mathf.DeltaAngle(heading, wanted) / .115f, (pitch - 28) / .10f) });
            yield return Frames(4);
            Require(Crew.Prompt.Contains("破口"), $"visible repair prompt at measured stand {index}: {Crew.Prompt}");
        }

        IEnumerator RepeatedImpact()
        {
            var flooding = Ship.Flooding;
            var anchors = Ship.Rig.LeakOutside;
            var target = anchors[0];
            CheckObstructedStands(target);
            var marks = new Dictionary<string, Mesh>();
            float ratePerBreach = 0f;
            for (int shot = 1; shot <= anchors.Length + 1; shot++)
            {
                int before = flooding.ActiveLeaks;
                flooding.Hit(target.position, target.forward);
                var after = CheckImpactMarks(target);
                CheckSurvivingMarks(marks, after);
                if (flooding.ActiveLeaks == before)
                {
                    Require(before >= 2 && after.Count == marks.Count, $"same point saturated after {before} independent breaches");
                    break;
                }

                Require(flooding.ActiveLeaks == before + 1 && flooding.IsLeaking(0), $"shot {shot} added exactly one local breach");
                marks = after;
                yield return new WaitForEndOfFrame();
                float height = flooding.WaterHeight;
                double start = Time.timeAsDouble;
                yield return Frames(6);
                yield return new WaitForEndOfFrame();
                float rate = (flooding.WaterHeight - height) / (float)(Time.timeAsDouble - start);
                if (before == 0) ratePerBreach = rate;
                Require(ratePerBreach > 0f && Mathf.Abs(rate / flooding.ActiveLeaks - ratePerBreach) < .0004f,
                    $"same point: {flooding.ActiveLeaks} breach(es), ingress={rate:F5} m/s; one breach={ratePerBreach:F5} m/s");
            }

            int full = flooding.ActiveLeaks;
            for (int shot = 0; shot < 3; shot++) flooding.Hit(target.position, target.forward);
            var saturated = CheckImpactMarks(target);
            Require(flooding.ActiveLeaks == full && saturated.Count == marks.Count, "three further impacts leave saturated neighbourhood unchanged");
            CheckSurvivingMarks(marks, saturated);
            yield return PhotographBreaches(target);

            yield return ApproachRepairSite(0);
            yield return UseKey();
            Require(Crew.StationName == "船壳破口", $"keyboard E started independent repair at breach 0; station={Crew.StationName}");
            yield return Frames(HullRepairFrames);
            var survivors = CheckImpactMarks(target);
            Require(Crew.StationName.Length == 0 && !flooding.IsLeaking(0) && flooding.ActiveLeaks == full - 1 &&
                survivors.Count == saturated.Count - 2, "one E repair removes only its breach and two decals");
            CheckSurvivingMarks(survivors, saturated);

            flooding.Hit(target.position, target.forward);
            var reopened = CheckImpactMarks(target);
            Require(flooding.IsLeaking(0) && flooding.ActiveLeaks == full, "next shot reopens the repaired site at the original impact");
            CheckSurvivingMarks(survivors, reopened);
        }

        IEnumerator PhotographBreaches(Transform hit)
        {
            var rig = Ship.Rig;
            var view = game.MainCamera;
            var centre = new Bounds(hit.position, Vector3.zero);
            for (int i = 0; i < rig.LeakOutside.Length; i++)
                if (Ship.Flooding.IsLeaking(i)) centre.Encapsulate(rig.LeakOutside[i].position);
            bool crewEnabled = Crew.enabled;
            var position = view.transform.position;
            var rotation = view.transform.rotation;
            Crew.enabled = false;
            try
            {
                var up = Ship.transform.up;
                var outward = Vector3.ProjectOnPlane(hit.forward, up).normalized;
                view.transform.position = centre.center + outward * (3.6f + centre.extents.magnitude) + up * .85f;
                view.transform.LookAt(centre.center, up);
                for (int frame = 0; frame < 24; frame++)
                {
                    bool lit = true;
                    for (int i = 0; i < rig.LeakOutside.Length; i++)
                        if (Ship.Flooding.IsLeaking(i)) lit &= Ship.Flooding.IsSignalLit(i);
                    if (lit) break;
                    yield return null;
                }
                yield return Photograph("repeated-impact-" + type);
            }
            finally
            {
                view.transform.SetPositionAndRotation(position, rotation);
                Crew.enabled = crewEnabled;
            }
        }

        void CheckObstructedStands(Transform target)
        {
            var flooding = Ship.Flooding;
            var rig = Ship.Rig;
            var obstacles = new GameObject("Acceptance obstacles at local repair posts");
            obstacles.transform.SetParent(Ship.transform, false);
            try
            {
                Vector3 point = Ship.transform.InverseTransformPoint(target.position);
                var blocked = new Dictionary<int, BoxCollider>();
                for (int i = 0; i < rig.LeakOutside.Length; i++)
                {
                    Vector3 other = Ship.transform.InverseTransformPoint(rig.LeakOutside[i].position);
                    if (point.x * other.x <= 0f || Vector3.Distance(point, other) > HullFlooding.NeighbourRadius + .005f) continue;
                    var collider = obstacles.AddComponent<BoxCollider>();
                    collider.center = rig.Definition.Data.repairStands[i].position + Vector3.up * .8f;
                    collider.size = new Vector3(.45f, .8f, .45f);
                    blocked.Add(i, collider);
                }
                Require(blocked.Count >= 2 && blocked.ContainsKey(0), $"test obstacles placed at {blocked.Count} local repair posts");
                Physics.SyncTransforms();
                flooding.Hit(target.position, target.forward);
                Require(flooding.ActiveLeaks == 0 && flooding.ActiveDecals == 0 && flooding.WaterLevel01 == 0f,
                    "solid planking hit with every local repair post obstructed leaves the dry hull intact");

                foreach (var pair in blocked) pair.Value.enabled = pair.Key == 0;
                Physics.SyncTransforms();
                flooding.Hit(target.position, target.forward);
                int alternate = -1;
                foreach (int index in blocked.Keys)
                    if (flooding.IsLeaking(index)) alternate = index;
                Require(alternate > 0 && !flooding.IsLeaking(0) && flooding.ActiveLeaks == 1 && flooding.ActiveDecals == 2,
                    $"blocked nearest post selects independently reachable alternative {alternate}");
                Require(flooding.Repair(alternate) && flooding.ActiveLeaks == 0 && flooding.ActiveDecals == 0 &&
                    flooding.WaterLevel01 == 0, "isolated obstacle fixture reset before repeated-hit measurements");
            }
            finally
            {
                obstacles.SetActive(false);
                Destroy(obstacles);
                Physics.SyncTransforms();
            }
        }

        Dictionary<string, Mesh> CheckImpactMarks(Transform target)
        {
            var flooding = Ship.Flooding;
            var renderMeshes = new Dictionary<string, Mesh>();
            foreach (Transform child in Ship.Rig.transform)
            {
                if (!child.gameObject.activeInHierarchy || !child.name.StartsWith("Hull damage ")) continue;
                var filter = child.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || !child.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled ||
                    !renderMeshes.TryAdd(child.name, filter.sharedMesh))
                    throw new System.InvalidOperationException("Missing or duplicated rendered impact mesh: " + child.name);
            }

            var inside = new List<Bounds>();
            var outside = new List<Bounds>();
            Vector3 origin = Ship.transform.InverseTransformPoint(target.position);
            for (int i = 0; i < Ship.Rig.LeakOutside.Length; i++)
            {
                if (!flooding.IsLeaking(i)) continue;
                var anchor = Ship.Rig.LeakOutside[i];
                Vector3 point = Ship.transform.InverseTransformPoint(anchor.position);
                Require(origin.x * point.x > 0f && Vector3.Distance(origin, point) <= HullFlooding.NeighbourRadius + .005f,
                    $"breach {i} on struck side at local distance {Vector3.Distance(origin, point):F2} m");
                CheckFace(i, "hold", Ship.Rig.LeakInside[i], .92f, .64f, inside, renderMeshes);
                CheckFace(i, "exterior", anchor, 1.56f, 1.12f, outside, renderMeshes);
            }
            Require(inside.Count == flooding.ActiveLeaks && renderMeshes.Count == inside.Count * 2 && renderMeshes.Count == flooding.ActiveDecals,
                $"{flooding.ActiveLeaks} active breaches with {renderMeshes.Count} independently rendered marks");
            return renderMeshes;
        }

        void CheckFace(int index, string face, Transform anchor, float width, float height, List<Bounds> others, Dictionary<string, Mesh> marks)
        {
            string label = $"Hull damage {index + 1} - {face}";
            if (!marks.TryGetValue(label, out var mesh)) throw new System.InvalidOperationException("Missing " + label);
            foreach (var prior in others)
                if (prior.Intersects(mesh.bounds)) throw new System.InvalidOperationException("Overlapping projected marks at " + label);
            others.Add(mesh.bounds);

            // The meshes can be clipped by individual planks. Compare metres per UV unit on the two projection axes.
            var normal = Ship.transform.InverseTransformDirection(anchor.forward).normalized;
            var horizontal = Vector3.Cross(Vector3.up, normal).normalized;
            var vertical = Vector3.Cross(normal, horizontal).normalized;
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            if (vertices.Length < 3 || vertices.Length != uv.Length) throw new System.InvalidOperationException("Empty impact art: " + label);
            for (int i = 1; i < vertices.Length; i++)
            {
                Vector3 delta = vertices[i] - vertices[0];
                Vector2 textureDelta = uv[i] - uv[0];
                if (Mathf.Abs(Vector3.Dot(delta, horizontal) - textureDelta.x * width) > .025f ||
                    Mathf.Abs(Vector3.Dot(delta, vertical) - textureDelta.y * height) > .025f)
                    throw new System.InvalidOperationException("Incorrect projected impact size: " + label);
            }
        }

        static void CheckSurvivingMarks(Dictionary<string, Mesh> existing, Dictionary<string, Mesh> current)
        {
            foreach (var pair in existing)
                if (!current.TryGetValue(pair.Key, out var mesh) || mesh != pair.Value)
                    throw new System.InvalidOperationException("An independent impact mesh was removed or replaced: " + pair.Key);
        }
    }
}
