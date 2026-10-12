using System;
using Convoy.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Convoy.Race
{
    /// <summary>
    /// Runs one point-to-point sprint (S-01, D-014): clock, ordered checkpoints, finish at the monolith.
    /// Registers itself as the vehicle's <see cref="IRecoveryPointProvider"/>, so V-02 resets go to the
    /// last checkpoint passed (or the start line). Thin adapter around <see cref="SprintRun"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SprintController : MonoBehaviour, IRecoveryPointProvider
    {
        [SerializeField] private VehicleController _vehicle;
        [Tooltip("Checkpoints in driving order. The finish trigger is separate.")]
        [SerializeField] private SprintGate[] _checkpoints = Array.Empty<SprintGate>();
        [SerializeField] private SprintGate _finish;
        [Tooltip("Horizontal speed that starts the clock, m/s. Vertical motion (suspension settling on spawn) is ignored.")]
        [Min(0f)] [SerializeField] private float _startSpeed = 1.5f;
        [Tooltip("Monolith uplink zone. Empty = legacy instant finish at the Finish gate.")]
        [SerializeField] private UplinkZone _uplink;          // UP-01: uplink finish (D-025)
        [SerializeField] private UplinkConfig _uplinkConfig;  // UP-01: uplink finish (D-025)

        private SprintRun _run;
        private Pose _startPose;
        private UplinkHold _hold;                             // UP-01: uplink finish (D-025)
        private string BestTimeKey => "convoy.sprint.best." + SceneManager.GetActiveScene().name;

        public SprintRun Run => _run;
        public VehicleController Vehicle => _vehicle;
        public int CheckpointCount => _checkpoints.Length;
        public bool UsesUplink => _hold != null;              // UP-01: uplink finish (D-025)
        /// <summary>Null when !UsesUplink. Presentation reads Progress01/Inside only.</summary>
        public UplinkHold Uplink => _hold;                    // UP-01: uplink finish (D-025)
        public UplinkZone UplinkZone => _uplink;              // UP-01: uplink finish (D-025)

        /// <summary>Raised when the vehicle reaches the finish. Presentation listens (result screen, audio).</summary>
        public event Action Finished;

        private void Awake()
        {
            _run = new SprintRun(_checkpoints.Length, PlayerPrefs.GetFloat(BestTimeKey, 0f));
            for (int i = 0; i < _checkpoints.Length; i++)
            {
                if (_checkpoints[i] == null) { Debug.LogError($"{name}: checkpoint {i} is not assigned.", this); continue; }
                _checkpoints[i].Bind(this, i);
            }
            if (_finish != null) _finish.Bind(this, SprintGate.FinishIndex);
            else Debug.LogError($"{name}: no finish gate assigned.", this);

            // UP-01: uplink finish (D-025) — both refs or neither; a half-wired uplink stays legacy and says so.
            if (_uplink != null && _uplinkConfig != null) _hold = new UplinkHold(_uplinkConfig);
            else if (_uplink != null || _uplinkConfig != null)
                Debug.LogError($"{name}: uplink needs both Uplink and Uplink Config; using the instant finish.", this);

            if (_vehicle == null) { Debug.LogError($"{name}: no vehicle assigned.", this); return; }
            _startPose = new Pose(_vehicle.transform.position, _vehicle.transform.rotation);
            _vehicle.SetRecoveryPointProvider(this);
        }

        private void OnDestroy()
        {
            if (_vehicle != null) _vehicle.SetRecoveryPointProvider(null);
        }

        private void FixedUpdate()
        {
            if (_run == null || _vehicle == null) return;
            Vector3 horizontal = Vector3.ProjectOnPlane(_vehicle.State.Velocity, Vector3.up);
            _run.Tick(Time.fixedDeltaTime, horizontal.magnitude > _startSpeed);

            // UP-01: uplink finish (D-025) — polled, not a trigger: a parked Rigidbody sleeps and stops sending trigger events.
            if (_hold != null && _run.Phase == SprintPhase.Running && _run.AllCheckpointsPassed)
            {
                bool inside = _uplink.Contains(_vehicle.State.Position);
                if (_hold.Tick(Time.fixedDeltaTime, inside, _vehicle.State.Velocity.magnitude))
                {
                    _uplink.ReportCompleted(_vehicle);        // UP-01: D-031 — the zone records the first vehicle to uplink
                    CompleteFinish();
                }
            }
        }

        /// <summary>Called by a <see cref="SprintGate"/> when a vehicle drives through it.</summary>
        internal void OnGateEntered(int index, VehicleController vehicle)
        {
            if (vehicle != _vehicle || _run == null) return;

            if (index == SprintGate.FinishIndex)
            {
                if (_hold != null) return;                    // UP-01: uplink finish (D-025) — the gate is ignored, the zone finishes
                CompleteFinish();
                return;
            }

            _run.PassCheckpoint(index);
        }

        // UP-01: uplink finish (D-025) — shared by the legacy gate and the uplink; behaviour unchanged.
        private void CompleteFinish()
        {
            if (!_run.TryFinish()) return;
            if (_run.IsNewBest) { PlayerPrefs.SetFloat(BestTimeKey, _run.BestTime); PlayerPrefs.Save(); }
            Finished?.Invoke();
        }

        public bool TryGetRecoveryPose(out Pose pose)
        {
            if (_run != null && _run.LastCheckpoint >= 0 && _run.LastCheckpoint < _checkpoints.Length)
            {
                pose = _checkpoints[_run.LastCheckpoint].RecoveryPose;
                return true;
            }
            pose = _startPose;
            return true;
        }

        /// <summary>Reload the scene for another run.</summary>
        public void Restart()
        {
            // The scene must be in Build Settings (Sprint_01 is added there).
            SceneManager.LoadScene(gameObject.scene.name);
        }
    }
}
