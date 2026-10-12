using System.Collections.Generic;
using UnityEngine;

namespace Convoy.AI
{
    /// <summary>
    /// One route an AI can drive (D-019, ADR-004). Two ways to define it:
    /// <list type="bullet">
    /// <item><b>Scene authoring</b> (A-01c, TR-01c editor scripts, hand placement): the ordered child
    /// <c>Transform</c>s are the waypoints, in sibling order.</item>
    /// <item><b>Code</b> (PlayMode tests, future runtime spawners): <see cref="Configure"/> with any point list.</item>
    /// </list>
    /// Works for closed loops and for <b>open</b> polylines of any shape (TR-01 rival routes run from a map edge to
    /// the crater and end there); open lines simply end — the brain brakes to a stop at the last point.
    /// The <see cref="PolylinePath"/> is built once (all the allocation happens here, never per physics step).
    /// Moving waypoints at runtime has no effect until <see cref="Rebuild"/> is called.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RacingLine : MonoBehaviour
    {
        [Tooltip("True: the last waypoint connects back to the first (a loop). False: the route ends at the last waypoint.")]
        [SerializeField] private bool _closed;

        [Tooltip("Drivable half-width of the route, metres. AI lane offsets never exceed it.")]
        [Min(0.5f)] [SerializeField] private float _halfWidth = 4f;

        private PolylinePath _path;
        private bool _configuredFromCode;
        private bool _buildFailed; // don't retry (and re-log, re-allocate) on every access after a bad setup

        /// <summary>
        /// The racing line in world space, or null if it has fewer than 2 distinct points (an error is logged).
        /// Built lazily on first access, so it is valid even when another component's Awake asks first.
        /// </summary>
        public PolylinePath Path
        {
            get
            {
                if (_path == null && !_buildFailed) Rebuild();
                return _path;
            }
        }

        public float HalfWidth => _halfWidth;
        public bool Closed => _closed;

        private void Awake()
        {
            // Build at load time so the first FixedUpdate of every rival doesn't pay for it.
            if (!_configuredFromCode && transform.childCount >= 2) Rebuild();
        }

        /// <summary>Re-reads the child waypoints (world positions, sibling order). Allocates: setup time only.</summary>
        public void Rebuild()
        {
            _configuredFromCode = false;
            _path = BuildFromChildren(logErrors: true);
            _buildFailed = _path == null;
        }

        /// <summary>
        /// Defines the line from code instead of child transforms (copies the points). Any open or closed
        /// polyline with ≥ 2 distinct points works. Allocates: setup time only.
        /// </summary>
        /// <param name="points">World-space waypoints in driving order.</param>
        public void Configure(IReadOnlyList<Vector3> points, bool closed, float halfWidth)
        {
            _closed = closed;
            _halfWidth = Mathf.Max(0.5f, halfWidth);
            _path = new PolylinePath(points, closed); // throws on < 2 distinct points: a code bug, fail loudly
            _configuredFromCode = true;
            _buildFailed = false;
        }

        private PolylinePath BuildFromChildren(bool logErrors)
        {
            int count = transform.childCount;
            var points = new List<Vector3>(count);
            for (int i = 0; i < count; i++) points.Add(transform.GetChild(i).position);

            try
            {
                return new PolylinePath(points, _closed);
            }
            catch (System.ArgumentException)
            {
                if (logErrors)
                    Debug.LogError($"{name}: RacingLine needs at least 2 child waypoints at different positions (has {count}).", this);
                return null;
            }
        }

#if UNITY_EDITOR
        private const int MaxGizmoSamples = 600;
        private static readonly Color LineColor = new Color(1f, 0.75f, 0.1f, 1f);
        private static readonly Color BandColor = new Color(1f, 0.75f, 0.1f, 0.35f);

        // Editor only: draw the line and its ±halfWidth band so routes can be checked in the Scene view (the
        // orchestrator eyeballs generated lines this way, A-01c risk list). Allocations here never reach a build.
        private void OnDrawGizmos()
        {
            PolylinePath path = Application.isPlaying || _configuredFromCode ? _path : BuildFromChildren(logErrors: false);
            if (path == null || path.Length <= 0f) return;

            float step = Mathf.Max(1f, path.Length / MaxGizmoSamples);
            Vector3 previous = path.PositionAt(0f);
            Vector3 previousRight = PolylinePath.RightOf(path.TangentAt(0f)) * _halfWidth;
            for (float d = step; ; d += step)
            {
                bool last = d >= path.Length;
                float distance = last ? (path.Closed ? 0f : path.Length) : d;
                Vector3 current = path.PositionAt(distance);
                Vector3 right = PolylinePath.RightOf(path.TangentAt(distance)) * _halfWidth;

                Gizmos.color = LineColor;
                Gizmos.DrawLine(previous, current);
                Gizmos.color = BandColor;
                Gizmos.DrawLine(previous + previousRight, current + right);
                Gizmos.DrawLine(previous - previousRight, current - right);

                if (last) break;
                previous = current;
                previousRight = right;
            }

            // Start marker; open lines also mark the end so the driving direction is obvious.
            Gizmos.color = LineColor;
            Gizmos.DrawWireSphere(path.PositionAt(0f), 1f);
            if (!path.Closed) Gizmos.DrawWireCube(path.PositionAt(path.Length), Vector3.one * 2f);
        }
#endif
    }
}
