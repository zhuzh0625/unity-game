using System.Collections.Generic;
using UnityEngine;

namespace Emberfall
{
    // One reusable flow field serves every pursuer. Rebuild only when the player's grid cell changes.
    public sealed class ZoneNavigation
    {
        const float Cell = .5f;
        const int Width = 85, Height = 45;
        readonly bool[] open = new bool[Width * Height];
        readonly int[] distance = new int[Width * Height], queue = new int[Width * Height];
        readonly List<Rect> walls;
        int lastGoal = -1;
        public ZoneNavigation(List<Rect> walls) { this.walls = walls; }
        public void Rebuild()
        {
            for (int i = 0; i < open.Length; i++) open[i] = Walkable(Point(i));
            lastGoal = -1;
        }
        public bool Walkable(Vector2 p)
        {
            if (Mathf.Abs(p.x) > 21 || Mathf.Abs(p.y) > 11) return false;
            foreach (var r in walls)
                if (new Rect(r.xMin - .35f, r.yMin - .4f, r.width + .7f, r.height + .8f).Contains(p)) return false;
            return true;
        }
        public float FirstWall(Vector2 from, Vector2 to, float padding = .04f)
        {
            float first = 1.001f;
            Vector2 delta = to - from;
            foreach (var r in walls)
            {
                float low = 0, high = 1;
                if (Clip(from.x, delta.x, r.xMin - padding, r.xMax + padding, ref low, ref high) &&
                    Clip(from.y, delta.y, r.yMin - padding, r.yMax + padding, ref low, ref high)) first = Mathf.Min(first, low);
            }
            return first;
        }
        public bool Visible(Vector2 from, Vector2 to) => FirstWall(from, to) > 1;
        static bool Clip(float origin, float delta, float min, float max, ref float low, ref float high)
        {
            if (Mathf.Abs(delta) < .00001f) return origin >= min && origin <= max;
            float a = (min - origin) / delta, b = (max - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            low = Mathf.Max(low, a); high = Mathf.Min(high, b);
            return low <= high;
        }
        static Vector2 Point(int index) => new Vector2(index % Width * Cell - 21, index / Width * Cell - 11);
        static int Index(Vector2 p) => Mathf.Clamp(Mathf.RoundToInt((p.y + 11) / Cell), 0, Height - 1) * Width + Mathf.Clamp(Mathf.RoundToInt((p.x + 21) / Cell), 0, Width - 1);
        bool EdgeOpen(int a, int b) => FirstWall(Point(a), Point(b), .41f) > 1;
        void Fill(Vector2 goal)
        {
            int index = Index(goal);
            if (!open[index])
            {
                float nearest = float.MaxValue;
                for (int i = 0; i < open.Length; i++) if (open[i])
                {
                    float d = (Point(i) - goal).sqrMagnitude;
                    if (d < nearest) { nearest = d; index = i; }
                }
            }
            if (lastGoal == index) return;
            lastGoal = index;
            for (int i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;
            int read = 0, write = 0; queue[write++] = index; distance[index] = 0;
            while (read < write)
            {
                int current = queue[read++], x = current % Width, y = current / Width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dy == 0) || x + dx < 0 || x + dx >= Width || y + dy < 0 || y + dy >= Height) continue;
                    int next = current + dx + dy * Width;
                    if (!open[next] || distance[next] != int.MaxValue || !EdgeOpen(current, next)) continue;
                    distance[next] = distance[current] + 1; queue[write++] = next;
                }
            }
        }
        public Vector2 Direction(Vector2 from, Vector2 goal)
        {
            if (FirstWall(from, goal, .42f) > 1) return (goal - from).normalized;
            Fill(goal);
            int current = Index(from), x = current % Width, y = current / Width;
            Vector2 nextPoint = from; float best = float.MaxValue;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                if (x + dx < 0 || x + dx >= Width || y + dy < 0 || y + dy >= Height) continue;
                int next = current + dx + dy * Width;
                if (!open[next] || distance[next] == int.MaxValue || FirstWall(from, Point(next), .41f) <= 1) continue;
                float score = distance[next] + Vector2.Distance(from, Point(next)) * .2f;
                if (score < best) { best = score; nextPoint = Point(next); }
            }
            return (nextPoint - from).normalized;
        }
    }
}
