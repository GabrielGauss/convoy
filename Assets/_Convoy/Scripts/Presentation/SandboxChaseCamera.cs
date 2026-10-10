using Convoy.Vehicle;
using UnityEngine;

namespace Convoy.Presentation
{
    /// <summary>
    /// Minimal chase camera so V-01 can be driven and judged. Placeholder only:
    /// C-01 replaces it with a Cinemachine 3 rig. Reads vehicle state, never writes it (D-007).
    /// Put this on the Main Camera and assign the vehicle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxChaseCamera : MonoBehaviour
    {
        [SerializeField] private VehicleController _target;

        [Header("Framing")]
        [SerializeField] private float _distance = 6.5f;
        [SerializeField] private float _height = 2.4f;
        [Tooltip("Extra distance added at top speed.")]
        [SerializeField] private float _speedDistance = 2.5f;
        [SerializeField] private float _lookAhead = 4f;

        [Header("Smoothing")]
        [Tooltip("Higher = camera follows heading changes faster.")]
        [Min(0.1f)] [SerializeField] private float _rotationSharpness = 4f;
        [Min(0.1f)] [SerializeField] private float _positionSharpness = 10f;

        [Header("Field of view")]
        [SerializeField] private float _baseFov = 60f;
        [SerializeField] private float _speedFov = 12f;

        [Header("Collision")]
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [SerializeField] private float _collisionRadius = 0.3f;

        private Camera _camera;
        private Quaternion _heading;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_target != null) _heading = YawOnly(_target.transform.rotation);
        }

        private void LateUpdate()
        {
            if (_target == null || _target.Config == null) return;

            VehicleState state = _target.State;
            Transform t = _target.transform;
            float speedRatio = Mathf.Clamp01(Mathf.Abs(state.ForwardSpeed) / _target.Config.topSpeed);

            // Follow travel direction when moving forward fast, otherwise the car's heading.
            Vector3 flatVelocity = Vector3.ProjectOnPlane(state.Velocity, Vector3.up);
            Quaternion desired = state.ForwardSpeed > 3f && flatVelocity.sqrMagnitude > 1f
                ? Quaternion.LookRotation(flatVelocity.normalized, Vector3.up)
                : YawOnly(t.rotation);
            _heading = Quaternion.Slerp(_heading, desired, 1f - Mathf.Exp(-_rotationSharpness * Time.deltaTime));

            Vector3 pivot = t.position + Vector3.up * 1f;
            float distance = _distance + _speedDistance * speedRatio;
            Vector3 desiredPos = pivot - _heading * Vector3.forward * distance + Vector3.up * (_height - 1f);

            // Pull in when terrain is between the car and the camera.
            Vector3 toCam = desiredPos - pivot;
            if (Physics.SphereCast(pivot, _collisionRadius, toCam.normalized, out RaycastHit hit, toCam.magnitude,
                    _obstacleMask, QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
            {
                desiredPos = pivot + toCam.normalized * Mathf.Max(0.5f, hit.distance - 0.1f);
            }

            transform.position = Vector3.Lerp(transform.position, desiredPos,
                1f - Mathf.Exp(-_positionSharpness * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(pivot + _heading * Vector3.forward * _lookAhead - transform.position, Vector3.up);

            if (_camera != null) _camera.fieldOfView = _baseFov + _speedFov * speedRatio;
        }

        private static Quaternion YawOnly(Quaternion q)
        {
            Vector3 f = Vector3.ProjectOnPlane(q * Vector3.forward, Vector3.up);
            return f.sqrMagnitude > 0.001f ? Quaternion.LookRotation(f.normalized, Vector3.up) : Quaternion.identity;
        }
    }
}
