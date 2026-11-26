using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    /// <summary>Projects impact art onto the triangles already forming the hull.</summary>
    internal sealed class HullDecalSurface
    {
        private const float ProjectionDepth = 0.55f;
        private const float SurfaceBias = 0.005f;

        private readonly struct Triangle
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly Vector3 C;
            public readonly Vector3 Normal;

            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                A = a;
                B = b;
                C = c;
                Normal = Vector3.Cross(b - a, c - a).normalized;
            }
        }

        private readonly List<Triangle> surface = new();

        public HullDecalSurface(Transform hull, MeshFilter[] sources)
        {
            foreach (MeshFilter source in sources)
            {
                if (source == null || source.sharedMesh == null || !source.sharedMesh.isReadable)
                    continue;

                Mesh mesh = source.sharedMesh;
                Matrix4x4 toHull = hull.worldToLocalMatrix * source.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = toHull.MultiplyPoint3x4(vertices[i]);

                int[] indices = mesh.triangles;
                bool mirrored = toHull.determinant < 0f;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]];
                    Vector3 b = vertices[indices[i + 1]];
                    Vector3 c = vertices[indices[i + 2]];
                    surface.Add(mirrored ? new Triangle(a, c, b) : new Triangle(a, b, c));
                }
            }
        }

        public Mesh Project(Vector3 center, Vector3 normal, float halfWidth, float halfHeight, out bool fitsArtwork)
        {
            fitsArtwork = false;
            bool centerBacked = false;
            Vector2 uvMin = Vector2.one, uvMax = Vector2.zero;
            Vector3 alongGrain = Vector3.Cross(Vector3.up, normal).normalized;
            if (alongGrain.sqrMagnitude < 0.01f)
                alongGrain = Vector3.right;

            Vector3 up = Vector3.Cross(normal, alongGrain).normalized;
            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<Vector2> uv = new();
            List<int> indices = new();

            // Clip in the decal's coordinate space to hug each plank and preserve openings.
            List<Vector3> input = new(10);
            List<Vector3> output = new(10);
            foreach (Triangle triangle in surface)
            {
                if (Vector3.Dot(triangle.Normal, normal) < 0.35f)
                    continue;

                Vector3 a = ToProjection(triangle.A - center, alongGrain, up, normal);
                Vector3 b = ToProjection(triangle.B - center, alongGrain, up, normal);
                Vector3 c = ToProjection(triangle.C - center, alongGrain, up, normal);
                if (Outside(a.x, b.x, c.x, halfWidth) || Outside(a.y, b.y, c.y, halfHeight) ||
                    Outside(a.z, b.z, c.z, ProjectionDepth))
                    continue;

                input.Clear();
                input.Add(a);
                input.Add(b);
                input.Add(c);
                Clip(ref input, ref output, 0, halfWidth, 1f);
                Clip(ref input, ref output, 0, halfWidth, -1f);
                Clip(ref input, ref output, 1, halfHeight, 1f);
                Clip(ref input, ref output, 1, halfHeight, -1f);
                Clip(ref input, ref output, 2, ProjectionDepth, 1f);
                Clip(ref input, ref output, 2, ProjectionDepth, -1f);
                if (input.Count < 3)
                    continue;

                int start = vertices.Count;
                bool containsCenter = true;
                Vector3 previous = input[input.Count - 1];
                foreach (Vector3 corner in input)
                {
                    // The clipped polygon is counterclockwise in the projector plane.
                    containsCenter &= previous.x * corner.y - previous.y * corner.x >= -.000001f;
                    previous = corner;
                    vertices.Add(center + alongGrain * corner.x + up * corner.y + normal * corner.z
                        + triangle.Normal * SurfaceBias);
                    normals.Add(triangle.Normal);
                    Vector2 texcoord = new(0.5f + corner.x / (2f * halfWidth), 0.5f + corner.y / (2f * halfHeight));
                    uv.Add(texcoord);
                    uvMin = Vector2.Min(uvMin, texcoord);
                    uvMax = Vector2.Max(uvMax, texcoord);
                }
                centerBacked |= containsCenter;

                for (int i = 1; i < input.Count - 1; i++)
                {
                    indices.Add(start);
                    indices.Add(start + i);
                    indices.Add(start + i + 1);
                }
            }

            if (indices.Count == 0)
                return null;

            // SplitOak's painted splinters span almost the full width and the middle
            // three quarters of the height. A sliver at a rail or port cannot hold a breach.
            fitsArtwork = centerBacked && uvMin.x <= .015f && uvMax.x >= .98f &&
                          uvMin.y <= .12f && uvMax.y >= .86f;
            Mesh result = new() { name = "Projected split oak" };
            result.SetVertices(vertices);
            result.SetNormals(normals);
            result.SetUVs(0, uv);
            result.SetTriangles(indices, 0);
            return result;
        }

        private static Vector3 ToProjection(Vector3 delta, Vector3 right, Vector3 up, Vector3 normal)
        {
            return new Vector3(Vector3.Dot(delta, right), Vector3.Dot(delta, up), Vector3.Dot(delta, normal));
        }

        private static bool Outside(float a, float b, float c, float range)
        {
            return (a > range && b > range && c > range) || (a < -range && b < -range && c < -range);
        }

        private static void Clip(ref List<Vector3> input, ref List<Vector3> output, int axis, float range, float sign)
        {
            output.Clear();
            if (input.Count != 0)
            {
                Vector3 previous = input[input.Count - 1];
                float previousDistance = range - sign * previous[axis];
                foreach (Vector3 vertex in input)
                {
                    float distance = range - sign * vertex[axis];
                    if ((previousDistance >= 0f) != (distance >= 0f))
                        output.Add(Vector3.LerpUnclamped(previous, vertex, previousDistance / (previousDistance - distance)));

                    if (distance >= 0f)
                        output.Add(vertex);

                    previous = vertex;
                    previousDistance = distance;
                }
            }

            (input, output) = (output, input);
        }
    }
}
