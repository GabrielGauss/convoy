using System;
using Convoy.Vehicle;
using UnityEngine;

namespace Convoy.Race
{
    /// <summary>
    /// V-02 recovery on open maps (TR-01c, D-031): there are no checkpoints, so a reset puts the vehicle back on the
    /// nearest authored route (main roads and shortcuts), a few metres behind where it left it, facing along the road.
    /// The route points are baked in by the editor setup (<c>TR01_ParamoRuntimeSetup</c>) from the terrain layout, with
    /// the chasm gap and its landing left out so a reset never drops a car into the hole it just fell into.
    /// Thin adapter: the maths is <see cref="RouteRecoveryMath"/> (0 B GC).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RouteRecoveryPoints : MonoBehaviour, IRecoveryPointProvider
    {
        [Tooltip("The vehicle body whose position picks the nearest route point. Empty = this transform.")]
        [SerializeField] private Rigidbody _body;
        [Tooltip("All route points, routes packed one after another in driving order (generated: do not edit by hand).")]
        [SerializeField] private Vector3[] _routePoints = Array.Empty<Vector3>();
        [Tooltip("Index of each route's first point in Route Points, ascending (generated).")]
        [SerializeField] private int[] _routeStarts = Array.Empty<int>();
        [Tooltip("How far back along the route from the nearest point the vehicle is reset, m (a run-up, not the crash spot).")]
        [Min(0f)] [SerializeField] private float _backoffMetres = 10f;

        private VehicleController _vehicle;

        public int PointCount => _routePoints != null ? _routePoints.Length : 0;

        /// <summary>Editor/test wiring. Copies nothing: the arrays are owned by this component afterwards.</summary>
        public void Configure(Rigidbody body, Vector3[] routePoints, int[] routeStarts)
        {
            _body = body;
            _routePoints = routePoints ?? Array.Empty<Vector3>();
            _routeStarts = routeStarts ?? Array.Empty<int>();
        }

        private void Awake()
        {
            _vehicle = GetComponentInParent<VehicleController>();
            if (_vehicle == null)
            {
                Debug.LogError($"{name}: RouteRecoveryPoints needs a VehicleController on this object or a parent.", this);
                return;
            }
            if (PointCount == 0)
                Debug.LogWarning($"{name}: no route points baked; resets fall back to the last safe pose. " +
                                 "Re-run Convoy/Integration/TR-01c Páramo Runtime Setup.", this);
            _vehicle.SetRecoveryPointProvider(this);
        }

        private void OnDestroy()
        {
            if (_vehicle != null) _vehicle.SetRecoveryPointProvider(null);
        }

        /// <summary>
        /// Nearest route point (ground plane) to the vehicle, backed off along its route, facing the route direction.
        /// The vehicle lifts the pose by its own <c>recoveryLift</c> (V-02), so the point is returned on the road.
        /// False (vehicle uses its last safe pose) when no points are baked.
        /// </summary>
        public bool TryGetRecoveryPose(out Pose pose)
        {
            Vector3 from = _body != null ? _body.position : transform.position;
            int nearest = RouteRecoveryMath.Nearest(from, _routePoints, _routeStarts, out _);
            if (nearest < 0)
            {
                pose = default;
                return false;
            }
            int index = RouteRecoveryMath.BackOff(nearest, _routePoints, _routeStarts, _backoffMetres);
            Vector3 tangent = RouteRecoveryMath.TangentAt(index, _routePoints, _routeStarts);
            pose = new Pose(_routePoints[index], Quaternion.LookRotation(tangent, Vector3.up));
            return true;
        }
    }
}
