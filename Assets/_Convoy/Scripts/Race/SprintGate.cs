using Convoy.Vehicle;
using UnityEngine;

namespace Convoy.Race
{
    /// <summary>
    /// Trigger volume for a sprint checkpoint or the finish (S-01). Its transform's forward is the
    /// driving direction, used as the reset heading. Needs a trigger Collider on the same object.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SprintGate : MonoBehaviour
    {
        public const int FinishIndex = -1;

        [Tooltip("Height above the gate's origin where a reset drops the vehicle, metres.")]
        [Min(0f)] [SerializeField] private float _resetHeight = 0.5f;

        private SprintController _controller;
        private int _index;

        public Pose RecoveryPose => new Pose(transform.position + Vector3.up * _resetHeight,
            Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up));

        internal void Bind(SprintController controller, int index)
        {
            _controller = controller;
            _index = index;
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning($"{name}: gate collider was not a trigger; fixing at runtime.", this);
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_controller == null || other.attachedRigidbody == null) return;
            VehicleController vehicle = other.attachedRigidbody.GetComponent<VehicleController>();
            if (vehicle != null) _controller.OnGateEntered(_index, vehicle);
        }
    }
}
