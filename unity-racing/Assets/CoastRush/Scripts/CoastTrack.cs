using System;
using UnityEngine;

namespace CoastRush
{
    public sealed class CoastTrack : MonoBehaviour
    {
        public Vector3[] points;
        public float[] distances;
        public float length;
        public float halfWidth = 7f;
        public const int GateCount = 24;

        public static Vector3[] ControlPoints => new[] {
            new Vector3(0,3,0), new Vector3(0,3,110), new Vector3(28,4,195),
            new Vector3(112,6,235), new Vector3(207,12,211), new Vector3(265,17,150),
            new Vector3(247,18,72), new Vector3(187,15,32), new Vector3(215,12,-29),
            new Vector3(287,10,-78), new Vector3(307,8,-145), new Vector3(254,6,-211),
            new Vector3(157,4,-231), new Vector3(76,3,-208), new Vector3(24,3,-157),
            new Vector3(0,3,-85)
        };

        public void Generate()
        {
            var control = ControlPoints;
            const int steps = 40;
            points = new Vector3[control.Length * steps + 1];
            distances = new float[points.Length];
            for (int i = 0; i < points.Length - 1; i++)
            {
                int n = i / steps;
                float t = (i % steps) / (float)steps;
                var a = control[(n + control.Length - 1) % control.Length];
                var b = control[n]; var c = control[(n + 1) % control.Length];
                var d = control[(n + 2) % control.Length];
                points[i] = .5f * ((2*b) + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t);
            }
            points[points.Length-1] = points[0];
            for (int i = 1; i < points.Length; i++) distances[i] = distances[i-1] + Vector3.Distance(points[i-1],points[i]);
            length = distances[distances.Length-1];
        }

        public Vector3 Position(float distance, float lateral = 0)
        {
            distance = Mathf.Repeat(distance,length);
            int i = Array.BinarySearch(distances,distance);
            if (i < 0) i = ~i - 1;
            i = Mathf.Clamp(i,0,points.Length-2);
            float t = (distance-distances[i]) / (distances[i+1]-distances[i]);
            if(lateral==0)return Vector3.Lerp(points[i],points[i+1],t);
            // Interpolate shared cross-sections, not a different lateral vector on each segment.
            // This keeps offset curves continuous at every sample and at the lap seam.
            return Vector3.Lerp(points[i]+RightAt(i)*lateral,points[i+1]+RightAt(i+1)*lateral,t);
        }

        Vector3 RightAt(int index)
        {
            int count=points.Length-1;index%=count;
            var incoming=(points[index]-points[(index+count-1)%count]).normalized;
            var outgoing=(points[(index+1)%count]-points[index]).normalized;
            return Vector3.Cross(Vector3.up,incoming+outgoing).normalized;
        }

        public Vector3 Forward(float distance) => (Position(distance+1)-Position(distance-1)).normalized;
        public Quaternion Rotation(float distance) => Quaternion.LookRotation(Forward(distance),Vector3.up);

        public float Project(Vector3 position, out float lateral, out float separation)
        {
            float best = float.MaxValue, along = 0; Vector3 nearest = default, tangent = Vector3.forward;
            for (int i = 0; i < points.Length-1; i++)
            {
                var edge = points[i+1]-points[i];
                float t = Mathf.Clamp01(Vector3.Dot(position-points[i],edge)/edge.sqrMagnitude);
                var p = points[i]+edge*t;
                float score = (position-p).sqrMagnitude;
                if (score < best) { best=score; nearest=p; tangent=edge.normalized; along=distances[i]+edge.magnitude*t; }
            }
            lateral = Vector3.Dot(position-nearest,Vector3.Cross(Vector3.up,tangent).normalized);
            separation = Mathf.Sqrt(best);
            return along;
        }
    }
}
