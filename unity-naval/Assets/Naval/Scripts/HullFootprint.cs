using System;
using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    /// <summary>Convex horizontal envelope of the outer planking about the ship's amidships pivot.</summary>
    internal sealed class HullFootprint
    {
        const int PlaneCount = 64;
        const int HalfCount = PlaneCount / 2;
        static readonly Vector2[] normals = CreateNormals();
        static readonly Dictionary<ShipType, HullFootprint> shapes = new();
        readonly float[] extents = new float[PlaneCount];
        readonly Vector2[] corners = new Vector2[PlaneCount];

        internal float Radius { get; }

        internal static HullFootprint For(GalleonRig rig)
        {
            var type = rig.Definition.Type;
            if (!shapes.TryGetValue(type, out var shape))
                shapes.Add(type, shape = new HullFootprint(rig));
            return shape;
        }

        HullFootprint(GalleonRig rig)
        {
            if (rig.OuterPlanking.Length == 0)
                throw new InvalidOperationException("Ship has no outer planking: " + rig.Definition.DisplayName);
            for (int i = 0; i < PlaneCount; i++) extents[i] = float.NegativeInfinity;
            foreach (MeshFilter plank in rig.OuterPlanking)
            {
                // GalleonRig binds direct children. Stay in model space even when spawned far from the origin.
                Transform part = plank.transform;
                var toModel = Matrix4x4.TRS(part.localPosition, part.localRotation, part.localScale);
                foreach (Vector3 vertex in plank.sharedMesh.vertices)
                {
                    Vector3 point = toModel.MultiplyPoint3x4(vertex);
                    float z = point.z - rig.Definition.PivotZ;
                    // ApplyPose rolls up to 1.75 degrees and pitches 0.48 degrees about keel height.
                    // Bound the horizontal swing of each plank, including the freeboard at the ends.
                    float lateralSwing = Mathf.Abs(point.y) * .032f + .005f;
                    float foreAftSwing = Mathf.Abs(point.y) * .009f + .005f;
                    for (int i = 0; i < PlaneCount; i++)
                    {
                        Vector2 n = normals[i];
                        float furthest = n.x * point.x + n.y * z
                            + Mathf.Abs(n.x) * lateralSwing + Mathf.Abs(n.y) * foreAftSwing;
                        extents[i] = Mathf.Max(extents[i], furthest);
                    }
                }
            }

            // Intersect neighbouring supporting lines. Their polygon encloses every projected plank.
            float radiusSquared = 0f;
            for (int i = 0; i < PlaneCount; i++)
            {
                int j = (i + 1) % PlaneCount;
                Vector2 a = normals[i], b = normals[j];
                float cross = a.x * b.y - a.y * b.x;
                corners[i] = new Vector2(extents[i] * b.y - extents[j] * a.y,
                    a.x * extents[j] - b.x * extents[i]) / cross;
                radiusSquared = Mathf.Max(radiusSquared, corners[i].sqrMagnitude);
            }
            Radius = Mathf.Sqrt(radiusSquared);
        }

        static Vector2[] CreateNormals()
        {
            var result = new Vector2[PlaneCount];
            for (int i = 0; i < HalfCount; i++)
            {
                float angle = i * (2f * Mathf.PI / PlaneCount);
                result[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                result[i + HalfCount] = -result[i];
            }
            return result;
        }

        static Vector2 CourseRotation(float course)
        {
            float radians = course * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        internal bool Overlap(Vector2 centre, float course, HullFootprint other, Vector2 otherCentre,
            float otherCourse, out Vector2 separation, float clearance = 0f)
        {
            separation = Vector2.zero;
            Vector2 offset = otherCentre - centre;
            float reach = Radius + other.Radius + clearance;
            if (offset.sqrMagnitude >= reach * reach) return false;

            Vector2 rotation = CourseRotation(course), otherRotation = CourseRotation(otherCourse);
            float depth = float.PositiveInfinity;
            Vector2 shift = Vector2.zero;
            if (!CheckAxes(rotation, other, otherRotation, offset, 1f, clearance, ref depth, ref shift) ||
                !other.CheckAxes(otherRotation, this, rotation, -offset, -1f, clearance, ref depth, ref shift))
                return false;
            separation = shift;
            return true;
        }

        internal Vector2 ClosestBoundaryPoint(Vector2 centre, float course, Vector2 point)
        {
            Vector2 rotation = CourseRotation(course);
            Vector2 offset = point - centre;
            var local = new Vector2(Vector2.Dot(rotation, offset), rotation.x * offset.y - rotation.y * offset.x);
            float distanceSquared = float.PositiveInfinity;
            Vector2 nearest = Vector2.zero;
            for (int i = 0; i < PlaneCount; i++)
            {
                Vector2 a = corners[i];
                Vector2 edge = corners[(i + 1) % PlaneCount] - a;
                float edgeLengthSquared = edge.sqrMagnitude;
                float along = edgeLengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(local - a, edge) / edgeLengthSquared) : 0f;
                Vector2 candidate = a + edge * along;
                float candidateDistance = (local - candidate).sqrMagnitude;
                if (candidateDistance >= distanceSquared) continue;
                distanceSquared = candidateDistance;
                nearest = candidate;
            }
            return centre + new Vector2(rotation.x * nearest.x - rotation.y * nearest.y,
                rotation.y * nearest.x + rotation.x * nearest.y);
        }

        bool CheckAxes(Vector2 rotation, HullFootprint other, Vector2 otherRotation, Vector2 offset,
            float sign, float clearance, ref float depth, ref Vector2 shift)
        {
            // Opposite faces share an axis. Test both ends, including when one projection contains the other.
            for (int i = 0; i < HalfCount; i++)
            {
                Vector2 n = normals[i];
                var axis = new Vector2(rotation.x * n.x + rotation.y * n.y, rotation.x * n.y - rotation.y * n.x);
                var otherAxis = new Vector2(otherRotation.x * axis.x - otherRotation.y * axis.y,
                    otherRotation.x * axis.y + otherRotation.y * axis.x);
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (Vector2 corner in other.corners)
                {
                    float projection = Vector2.Dot(corner, otherAxis);
                    min = Mathf.Min(min, projection);
                    max = Mathf.Max(max, projection);
                }
                float displacement = Vector2.Dot(offset, axis);
                float negative = displacement + min - extents[i] - clearance;
                float positive = displacement + max + extents[i + HalfCount] + clearance;
                if (negative >= 0f || positive <= 0f) return false;
                float distance = positive < -negative ? positive : negative;
                if (Mathf.Abs(distance) < depth)
                {
                    depth = Mathf.Abs(distance);
                    shift = axis * (sign * distance);
                }
            }
            return true;
        }
    }
}
