using UnityEngine;

namespace Convoy.Race
{
    /// <summary>
    /// Pure helpers for V-02 resets on open maps (TR-01c): with no checkpoints, a reset goes to the nearest point of
    /// the authored routes instead. Routes are packed into one point array: route r owns
    /// <c>pts[starts[r] .. starts[r+1]-1]</c> (the last route runs to the end), in driving order. Everything here is
    /// allocation-free (0 B GC) because it runs when a player presses reset mid-race.
    /// </summary>
    public static class RouteRecoveryMath
    {
        /// <summary>
        /// Index of the route point closest to <paramref name="p"/> on the ground plane (height ignored: a car that fell
        /// into a chasm or river is far below the road it should go back to). -1 when there are no points.
        /// <paramref name="tangent"/> = unit horizontal direction of travel at that point (forward if undefined).
        /// </summary>
        public static int Nearest(Vector3 p, Vector3[] pts, int[] starts, out Vector3 tangent)
        {
            tangent = Vector3.forward;
            if (pts == null || pts.Length == 0) return -1;
            int best = -1;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < pts.Length; i++)
            {
                float dx = pts[i].x - p.x, dz = pts[i].z - p.z;
                float sq = dx * dx + dz * dz;
                if (sq >= bestSq) continue;
                bestSq = sq;
                best = i;
            }
            tangent = TangentAt(best, pts, starts);
            return best;
        }

        /// <summary>Which route owns point <paramref name="index"/> (0 when <paramref name="starts"/> is empty: one route).</summary>
        public static int RouteOf(int index, int[] starts)
        {
            if (starts == null) return 0;
            int route = 0;
            for (int r = 1; r < starts.Length; r++)
            {
                if (starts[r] > index) break;
                route = r;
            }
            return route;
        }

        /// <summary>First point index of the route owning <paramref name="index"/>.</summary>
        public static int RouteFirst(int index, int[] starts)
        {
            if (starts == null || starts.Length == 0) return 0;
            return starts[RouteOf(index, starts)];
        }

        /// <summary>Last point index of the route owning <paramref name="index"/>.</summary>
        public static int RouteLast(int index, int[] starts, int pointCount)
        {
            if (starts == null || starts.Length == 0) return pointCount - 1;
            int route = RouteOf(index, starts);
            return route + 1 < starts.Length ? starts[route + 1] - 1 : pointCount - 1;
        }

        /// <summary>
        /// Unit horizontal direction of travel at <paramref name="index"/>: from the previous to the next point of the
        /// same route (never across a route boundary). Forward when the route has a single point.
        /// </summary>
        public static Vector3 TangentAt(int index, Vector3[] pts, int[] starts)
        {
            if (pts == null || index < 0 || index >= pts.Length) return Vector3.forward;
            int first = RouteFirst(index, starts);
            int last = RouteLast(index, starts, pts.Length);
            int a = Mathf.Max(first, index - 1);
            int b = Mathf.Min(last, index + 1);
            var d = new Vector3(pts[b].x - pts[a].x, 0f, pts[b].z - pts[a].z);
            float m = d.magnitude;
            return m > 1e-4f ? d / m : Vector3.forward;
        }

        /// <summary>
        /// Walks back toward the route start from <paramref name="index"/> until at least <paramref name="metres"/>
        /// (ground distance) are covered or the route's first point is reached. Why: resetting exactly beside a crash
        /// spot or a chasm lip drops the car straight back into it; a few metres back gives a run-up.
        /// </summary>
        public static int BackOff(int index, Vector3[] pts, int[] starts, float metres)
        {
            if (pts == null || index < 0 || index >= pts.Length) return index;
            int first = RouteFirst(index, starts);
            float covered = 0f;
            int i = index;
            while (i > first && covered < metres)
            {
                float dx = pts[i].x - pts[i - 1].x, dz = pts[i].z - pts[i - 1].z;
                covered += Mathf.Sqrt(dx * dx + dz * dz);
                i--;
            }
            return i;
        }
    }
}
