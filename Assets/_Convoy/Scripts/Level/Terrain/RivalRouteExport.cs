using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Turns an authored rival route (TR-01, the dashed lines on the Páramo map) into AI waypoints on the carved road
    /// (TR-01c). Pure so the editor setup and the EditMode tests (spec AC 9) produce exactly the same line.
    /// </summary>
    public static class RivalRouteExport
    {
        /// <summary>
        /// Evenly spaced points along <paramref name="route"/> (corners rounded as carved, y = ground), roughly
        /// <paramref name="spacing"/> apart, ending at the first point that is within <paramref name="stopRadius"/> of
        /// <paramref name="stopCentre"/> (ground plane). Why stop short: rival routes end at the Monolith itself; a rival
        /// told to drive to the last point would ram the pillar. With a stop radius = the plateau radius it parks on the
        /// plateau edge instead. <paramref name="stopRadius"/> &lt;= 0 keeps the whole route. Clears
        /// <paramref name="into"/>; returns the point count (editor/test time: allocates freely).
        /// </summary>
        public static int Waypoints(HeightField h, RouteDef route, float spacing, Vector2 stopCentre, float stopRadius,
            List<Vector3> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            int count = TerrainSynthesis.SampleRouteEven(h, route, spacing, into);
            if (stopRadius <= 0f || count < 2) return count;

            float r2 = stopRadius * stopRadius;
            for (int i = 1; i < count; i++)
            {
                float dx = into[i].x - stopCentre.x, dz = into[i].z - stopCentre.y;
                if (dx * dx + dz * dz > r2) continue;
                // Keep this first point inside the radius as the final waypoint (it is on the plateau edge).
                into.RemoveRange(i + 1, count - i - 1);
                break;
            }
            return into.Count;
        }
    }
}
