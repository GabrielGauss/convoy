using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convoy.AI
{
    /// <summary>Result of projecting a world position onto a <see cref="PolylinePath"/>.</summary>
    public struct PathProjection
    {
        /// <summary>Index of the segment the closest point lies on (segment i runs from point i to point i+1).</summary>
        public int Segment;

        /// <summary>Metres along the path from its first point.</summary>
        public float Distance;

        /// <summary>Signed horizontal distance from the path, metres. + = right of the path's direction of travel.</summary>
        public float LateralOffset;

        /// <summary>Closest point on the path.</summary>
        public Vector3 Point;
    }

    /// <summary>
    /// Immutable racing line made of straight segments (D-019, ADR-004). Points are copied once in
    /// the constructor; every query afterwards is allocation-free so the AI can call it every
    /// physics step without creating garbage (garbage → GC spikes → frame hitches).
    /// Distances are metres along the line (3D segment lengths); headings, tangents and
    /// curvature are measured on the ground (XZ) plane because that is what steering cares about.
    /// </summary>
    public sealed class PolylinePath
    {
        // Consecutive points closer than this are merged: a zero-length segment has no direction.
        private const float MinPointSpacing = 1e-3f;
        private const float MinHorizontalLengthSq = 1e-8f;

        private readonly Vector3[] _points;
        // _cumulative[i] = distance from the start to point i; _cumulative[SegmentCount] = Length.
        private readonly float[] _cumulative;
        // Unit horizontal direction of each segment, precomputed so TangentAt/Project never normalise at runtime.
        private readonly Vector3[] _tangents;
        private readonly int _segmentCount;

        /// <param name="points">At least 2 distinct points in driving order.</param>
        /// <param name="closed">True for a loop: a segment joins the last point back to the first.</param>
        /// <exception cref="ArgumentNullException">points is null.</exception>
        /// <exception cref="ArgumentException">Fewer than 2 distinct points.</exception>
        public PolylinePath(IReadOnlyList<Vector3> points, bool closed)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));

            // Copy, dropping consecutive duplicates (and a closing duplicate of the first point).
            var unique = new List<Vector3>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                if (unique.Count > 0 && (p - unique[unique.Count - 1]).sqrMagnitude < MinPointSpacing * MinPointSpacing) continue;
                unique.Add(p);
            }
            if (closed && unique.Count > 2 && (unique[unique.Count - 1] - unique[0]).sqrMagnitude < MinPointSpacing * MinPointSpacing)
                unique.RemoveAt(unique.Count - 1);
            if (unique.Count < 2) throw new ArgumentException("A path needs at least 2 distinct points.", nameof(points));

            _points = unique.ToArray();
            Closed = closed;
            _segmentCount = closed ? _points.Length : _points.Length - 1;

            _cumulative = new float[_segmentCount + 1];
            _tangents = new Vector3[_segmentCount];
            for (int i = 0; i < _segmentCount; i++)
            {
                Vector3 delta = EndOf(i) - _points[i];
                _cumulative[i + 1] = _cumulative[i] + delta.magnitude;
                _tangents[i] = HorizontalDirection(delta);
            }
            Length = _cumulative[_segmentCount];
            FillVerticalSegmentTangents();
        }

        public int PointCount => _points.Length;
        public float Length { get; }
        public bool Closed { get; }

        /// <summary>
        /// Closest point to <paramref name="position"/>, searching only <paramref name="searchWindow"/> segments
        /// either side of <paramref name="segmentHint"/> (normally last tick's segment). O(window), no allocation.
        /// A small window also stops the car "jumping" to a parallel part of the track (hairpins).
        /// </summary>
        public PathProjection Project(Vector3 position, int segmentHint, int searchWindow)
        {
            int window = Math.Max(0, searchWindow);
            if (2 * window + 1 >= _segmentCount) return ProjectRange(position, 0, _segmentCount - 1);

            if (Closed)
            {
                int hint = Wrap(segmentHint, _segmentCount);
                return ProjectRange(position, hint - window, hint + window);
            }

            int clampedHint = Math.Clamp(segmentHint, 0, _segmentCount - 1);
            return ProjectRange(position, Math.Max(0, clampedHint - window), Math.Min(_segmentCount - 1, clampedHint + window));
        }

        /// <summary>Closest point over the whole path. Use after teleports (resets) or on the first tick.</summary>
        public PathProjection ProjectGlobal(Vector3 position)
        {
            return ProjectRange(position, 0, _segmentCount - 1);
        }

        /// <summary>Point at <paramref name="distance"/> metres along the path: clamped to the ends (open) or wrapped (closed).</summary>
        public Vector3 PositionAt(float distance)
        {
            int segment = SegmentAt(distance, out float d);
            float segmentLength = _cumulative[segment + 1] - _cumulative[segment];
            float t = segmentLength > 0f ? (d - _cumulative[segment]) / segmentLength : 0f;
            return Vector3.LerpUnclamped(_points[segment], EndOf(segment), Mathf.Clamp01(t));
        }

        /// <summary>Unit horizontal direction of travel at <paramref name="distance"/> (the segment's direction).</summary>
        public Vector3 TangentAt(float distance)
        {
            return _tangents[SegmentAt(distance, out _)];
        }

        /// <summary>
        /// |heading change| per metre (1/m) around <paramref name="distance"/>, on the XZ plane.
        /// Measured as the angle between the chord coming in (distance − span/2 → distance) and the chord
        /// going out (distance → distance + span/2), divided by the mean chord length. Using chords instead
        /// of segment directions spreads a sharp waypoint over the span, so a 40 m-radius bend reads ≈ 1/40
        /// whatever the waypoint spacing. Straight → 0. Open paths: 0 where a chord runs off an end.
        /// </summary>
        public float CurvatureAt(float distance, float span)
        {
            float half = Math.Max(span, MinPointSpacing) * 0.5f;
            Vector3 centre = PositionAt(distance);
            Vector3 incoming = centre - PositionAt(distance - half);
            Vector3 outgoing = PositionAt(distance + half) - centre;
            incoming.y = 0f;
            outgoing.y = 0f;

            float inLength = incoming.magnitude;
            float outLength = outgoing.magnitude;
            if (inLength < MinPointSpacing || outLength < MinPointSpacing) return 0f;

            float angle = Mathf.Abs(SignedAngleXZ(incoming, outgoing)); // radians
            return angle / (0.5f * (inLength + outLength));
        }

        /// <summary>
        /// Signed angle in radians from <paramref name="from"/> to <paramref name="to"/> seen from above.
        /// Positive = clockwise from above = turning RIGHT in Unity's left-handed axes (+Z forward, +X right),
        /// matching the sign of <c>PilotCommand.Steer</c>. Returns 0 for zero vectors.
        /// </summary>
        public static float SignedAngleXZ(Vector3 from, Vector3 to)
        {
            float cross = from.z * to.x - from.x * to.z;
            float dot = from.x * to.x + from.z * to.z;
            if (cross == 0f && dot == 0f) return 0f;
            return Mathf.Atan2(cross, dot);
        }

        /// <summary>Horizontal unit vector pointing to the right of <paramref name="tangent"/> (Unity: cross(up, forward)).</summary>
        public static Vector3 RightOf(Vector3 tangent)
        {
            return new Vector3(tangent.z, 0f, -tangent.x);
        }

        // --- internals ---------------------------------------------------

        private Vector3 EndOf(int segment)
        {
            int next = segment + 1;
            return _points[next == _points.Length ? 0 : next];
        }

        // Searches segments first..last (may run past either end on closed paths; indices are wrapped).
        private PathProjection ProjectRange(Vector3 position, int first, int last)
        {
            var best = new PathProjection();
            float bestDistanceSq = float.PositiveInfinity;

            for (int k = first; k <= last; k++)
            {
                int i = Closed ? Wrap(k, _segmentCount) : k;
                Vector3 a = _points[i];
                Vector3 ab = EndOf(i) - a;
                float lengthSq = ab.sqrMagnitude;
                float t = lengthSq > 0f ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / lengthSq) : 0f;
                Vector3 closest = a + ab * t;
                float distanceSq = (position - closest).sqrMagnitude;
                if (distanceSq >= bestDistanceSq) continue;

                bestDistanceSq = distanceSq;
                best.Segment = i;
                best.Point = closest;
                best.Distance = _cumulative[i] + (_cumulative[i + 1] - _cumulative[i]) * t;
                best.LateralOffset = Vector3.Dot(position - closest, RightOf(_tangents[i]));
            }
            return best;
        }

        // Segment containing the distance, plus the distance after clamping (open) / wrapping (closed).
        // Binary search over the cumulative lengths: O(log n), no allocation.
        private int SegmentAt(float distance, out float clamped)
        {
            if (Closed)
            {
                clamped = distance % Length;
                if (clamped < 0f) clamped += Length;
            }
            else
            {
                clamped = Mathf.Clamp(distance, 0f, Length);
            }

            int low = 0;
            int high = _segmentCount - 1;
            while (low < high)
            {
                int mid = (low + high + 1) >> 1;
                if (_cumulative[mid] <= clamped) low = mid;
                else high = mid - 1;
            }
            return low;
        }

        private static int Wrap(int index, int count)
        {
            int r = index % count;
            return r < 0 ? r + count : r;
        }

        private static Vector3 HorizontalDirection(Vector3 delta)
        {
            delta.y = 0f;
            float lengthSq = delta.sqrMagnitude;
            return lengthSq > MinHorizontalLengthSq ? delta / Mathf.Sqrt(lengthSq) : Vector3.zero;
        }

        // A purely vertical segment has no horizontal direction: borrow the previous one (or the next one at the start).
        private void FillVerticalSegmentTangents()
        {
            for (int i = 1; i < _segmentCount; i++)
                if (_tangents[i] == Vector3.zero) _tangents[i] = _tangents[i - 1];
            for (int i = _segmentCount - 2; i >= 0; i--)
                if (_tangents[i] == Vector3.zero) _tangents[i] = _tangents[i + 1];
            for (int i = 0; i < _segmentCount; i++)
                if (_tangents[i] == Vector3.zero) _tangents[i] = Vector3.forward; // fully vertical path: arbitrary but valid
        }
    }
}
