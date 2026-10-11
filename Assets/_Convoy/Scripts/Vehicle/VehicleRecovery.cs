using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Supplies the pose a vehicle should reset to (V-02). The race module implements this with
    /// the last valid checkpoint (T-01), so a reset can never skip checkpoints.
    /// When no provider is present the vehicle falls back to its own last safe pose.
    /// </summary>
    public interface IRecoveryPointProvider
    {
        bool TryGetRecoveryPose(out Pose pose);
    }

    public enum RecoveryAction
    {
        None,
        /// <summary>Flip upright where the vehicle is (it was stuck upside down).</summary>
        AutoRight,
        /// <summary>Teleport to the recovery point (manual request).</summary>
        Reset,
    }

    /// <summary>Per-step input to <see cref="VehicleRecovery"/>. Plain data so the logic is testable without physics.</summary>
    public struct RecoveryInput
    {
        public float DeltaTime;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Speed;
        public int GroundedWheels;
        public int TotalWheels;
        public bool ResetRequested;
    }

    /// <summary>
    /// Pure recovery logic (V-02): auto-right when stuck upside down, manual reset with cooldown,
    /// and tracking of the last safe pose. No Unity physics calls, so it runs in EditMode tests.
    /// </summary>
    public sealed class VehicleRecovery
    {
        private readonly VehicleConfig _config;

        private float _upsideDownTime;
        private float _cooldown;
        private float _safeSampleTimer;
        private bool _hasSafePose;
        private bool _hasOlderSafePose;
        private Pose _latestSafePose;
        private Pose _olderSafePose;

        public VehicleRecovery(VehicleConfig config)
        {
            _config = config;
        }

        public float UpsideDownTime => _upsideDownTime;
        public float CooldownRemaining => _cooldown;
        public bool HasSafePose => _hasSafePose;

        /// <summary>
        /// The safe pose to reset to. Uses the second-newest sample when available, so a car that just
        /// crashed is not dropped back onto the crash spot.
        /// </summary>
        public Pose SafePose => _hasOlderSafePose ? _olderSafePose : _latestSafePose;

        /// <summary>True when the vehicle's up axis points more than ~72° away from world up (default threshold).</summary>
        public bool IsUpsideDown(Quaternion rotation)
        {
            return Vector3.Dot(rotation * Vector3.up, Vector3.up) < _config.upsideDownDot;
        }

        /// <summary>Advance one physics step and decide whether to recover.</summary>
        public RecoveryAction Step(in RecoveryInput input)
        {
            float dt = Mathf.Max(0f, input.DeltaTime);
            _cooldown = Mathf.Max(0f, _cooldown - dt);

            bool upsideDown = IsUpsideDown(input.Rotation);
            bool stuck = upsideDown && input.Speed < _config.autoRightMaxSpeed;
            _upsideDownTime = stuck ? _upsideDownTime + dt : 0f;

            TrackSafePose(input, upsideDown, dt);

            if (input.ResetRequested && _cooldown <= 0f)
            {
                Consume();
                return RecoveryAction.Reset;
            }

            if (_upsideDownTime >= _config.autoRightDelay)
            {
                Consume();
                return RecoveryAction.AutoRight;
            }

            return RecoveryAction.None;
        }

        /// <summary>Upright pose at the current spot: keeps heading, removes pitch/roll, lifts clear of the ground.</summary>
        public Pose AutoRightPose(Vector3 position, Quaternion rotation)
        {
            return new Pose(position + Vector3.up * _config.recoveryLift, UprightYaw(rotation));
        }

        /// <summary>Lift a recovery point so the suspension settles instead of spawning inside the ground.</summary>
        public Pose LiftedPose(Pose pose)
        {
            return new Pose(pose.position + Vector3.up * _config.recoveryLift, UprightYaw(pose.rotation));
        }

        public static Quaternion UprightYaw(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                // Nose pointing straight up or down: fall back to the roof/floor direction.
                forward = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            }
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void Consume()
        {
            _cooldown = _config.resetCooldown;
            _upsideDownTime = 0f;
            _safeSampleTimer = 0f;
        }

        private void TrackSafePose(in RecoveryInput input, bool upsideDown, float dt)
        {
            bool safe = !upsideDown && input.TotalWheels > 0 && input.GroundedWheels == input.TotalWheels;
            if (!safe)
            {
                _safeSampleTimer = 0f;
                return;
            }

            _safeSampleTimer += dt;
            if (_safeSampleTimer < _config.safePoseInterval && _hasSafePose) return;

            _safeSampleTimer = 0f;
            if (_hasSafePose)
            {
                _olderSafePose = _latestSafePose;
                _hasOlderSafePose = true;
            }
            _latestSafePose = new Pose(input.Position, input.Rotation);
            _hasSafePose = true;
        }
    }
}
