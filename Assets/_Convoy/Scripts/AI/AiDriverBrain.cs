using System;
using Convoy.Core;
using Convoy.Vehicle;
using UnityEngine;

namespace Convoy.AI
{
    /// <summary>
    /// Everything the brain senses in one physics step. Filled by <c>AiPilot</c> (A-01b) from
    /// <c>VehicleController.State</c> and three sphere-casts; filled by hand in tests.
    /// </summary>
    public struct AiSensorFrame
    {
        public float DeltaTime;
        public Vector3 Position, Forward, Velocity;

        /// <summary>Signed speed along the car's forward axis, m/s.</summary>
        public float ForwardSpeed;

        public int GroundedWheels;

        /// <summary>Metres to the first obstacle in each lane; <see cref="float.PositiveInfinity"/> = clear.</summary>
        public float ObstacleLeft, ObstacleCentre, ObstacleRight;

        /// <summary>
        /// Seconds until the vehicle accepts another reset (<c>VehicleState.RecoveryCooldown</c>). The vehicle's
        /// auto-right also starts this cooldown, which the brain cannot predict, so it reads it from the vehicle
        /// instead of guessing: a reset sent during the cooldown would be silently dropped (A-01a review).
        /// </summary>
        public float RecoveryCooldown;
    }

    /// <summary>
    /// The AI driver (D-019, ADR-004): turns a sensor frame into a <see cref="PilotCommand"/> — the exact
    /// struct a human's input produces — so rivals drive the same physics through the same
    /// <c>IPilotCommandReceiver</c>. No physics shortcuts, no rubber-banding (nothing here knows where the player is).
    /// <para>Per step: track progress on the path → pick a lane → pure-pursuit steering toward a lookahead point →
    /// throttle from <see cref="SpeedPlanner"/> → let <see cref="StuckDetector"/> override for recovery.</para>
    /// Plain C# (no MonoBehaviour) so it is unit-testable without a scene, and allocation-free per step.
    /// </summary>
    public sealed class AiDriverBrain
    {
        private readonly PolylinePath _path;
        private readonly AiDriverProfile _profile;
        private readonly VehicleConfig _vehicle;
        private readonly LaneSelector _lanes;
        private readonly StuckDetector _stuck;

        private bool _hasProjection;
        private int _segment;
        private float _resetCooldown;
        private AiDriveMode _mode = AiDriveMode.Driving;

        /// <param name="halfWidth">Route half-width, metres (from <c>RacingLine</c>); limits lane offsets.</param>
        public AiDriverBrain(PolylinePath path, AiDriverProfile profile, VehicleConfig vehicle, float halfWidth)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _vehicle = vehicle != null ? vehicle : throw new ArgumentNullException(nameof(vehicle));
            _lanes = new LaneSelector(profile, halfWidth);
            _stuck = new StuckDetector(profile);
        }

        /// <summary>Metres along the path (last projection).</summary>
        public float Progress { get; private set; }

        /// <summary>Open path: within <c>arriveDistance</c> of the end. Always false on a closed path.</summary>
        public bool ReachedEnd => !_path.Closed && Progress >= _path.Length - _profile.arriveDistance;

        public AiDriveMode Mode => Held ? AiDriveMode.Holding : _mode;

        /// <summary>Number of commands sent with <c>Reset = true</c>.</summary>
        public int ResetRequests { get; private set; }

        /// <summary>True → every step returns <see cref="PilotCommand.Neutral"/> (start hold until R-01's countdown).</summary>
        public bool Held { get; set; }

        /// <summary>Current lane offset from the path, metres (+ = right). Exposed for gizmos/debug.</summary>
        public float LaneOffset => _lanes.Offset;

        /// <summary>One decision. Call once per physics step (FixedUpdate). No allocation.</summary>
        public PilotCommand Step(in AiSensorFrame frame)
        {
            float dt = Math.Max(0f, frame.DeltaTime);
            if (_resetCooldown > 0f) _resetCooldown -= dt;

            float progressDelta = TrackProgress(frame.Position, out float lateralOffset);

            // Progress keeps tracking while held so the first released step starts from the right segment.
            if (Held) return PilotCommand.Neutral;

            _lanes.Step(frame.ObstacleLeft, frame.ObstacleCentre, frame.ObstacleRight, dt);

            float steer = SteerTowardLookahead(frame);
            float targetSpeed = SpeedPlanner.TargetSpeed(_path, Progress, _vehicle.topSpeed, _profile);
            float throttle = ThrottleFor(targetSpeed - frame.ForwardSpeed);

            if (ReachedEnd)
            {
                // Past the finish: brake to a stop but never start reversing (the vehicle turns
                // negative throttle into reverse below reverseThreshold), and don't run recovery.
                _mode = AiDriveMode.Driving;
                return new PilotCommand
                {
                    Steer = steer,
                    Throttle = frame.ForwardSpeed > _vehicle.reverseThreshold ? -1f : 0f,
                };
            }

            _stuck.Step(frame.ForwardSpeed, throttle, lateralOffset, progressDelta, dt);
            _mode = _stuck.Mode;

            switch (_mode)
            {
                case AiDriveMode.Reversing:
                    // Reversing flips which way the car yaws for a given wheel angle, so mirror the steer
                    // to keep backing toward the line.
                    return new PilotCommand { Throttle = -1f, Steer = -steer };

                case AiDriveMode.RequestReset:
                    // Send the reset exactly once, and only when the vehicle will accept it: VehicleRecovery
                    // ignores requests during its cooldown, so a request sent then would be lost. Two gates:
                    // the vehicle's real cooldown (also started by auto-right, which we can't predict) and our
                    // own copy, which covers the step before the vehicle's state reflects our last reset.
                    // Until both are clear we sit still.
                    if (_resetCooldown > 0f || frame.RecoveryCooldown > 0f) return PilotCommand.Neutral;
                    _resetCooldown = _vehicle.resetCooldown;
                    ResetRequests++;
                    _stuck.NotifyResetIssued();
                    return new PilotCommand { Reset = true };

                default:
                    bool boost = throttle > 0f && frame.GroundedWheels > 0
                        && SpeedPlanner.ClearForBoost(_path, Progress, _vehicle.topSpeed, _profile);
                    return new PilotCommand { Throttle = throttle, Steer = steer, Boost = boost };
            }
        }

        // Updates Progress and the segment hint; returns the change in progress (wrap-aware on loops).
        private float TrackProgress(Vector3 position, out float lateralOffset)
        {
            PathProjection projection = _hasProjection
                ? _path.Project(position, _segment, _profile.projectionWindow)
                : _path.ProjectGlobal(position);

            // Far from the windowed result usually means we were teleported (V-02 reset): search everything once.
            if (_hasProjection && Math.Abs(projection.LateralOffset) > _profile.offPathResetDistance)
                projection = _path.ProjectGlobal(position);

            float delta = _hasProjection ? projection.Distance - Progress : 0f;
            if (_path.Closed)
            {
                // Crossing the start line of a loop jumps progress by ±Length; that is not real movement.
                float half = _path.Length * 0.5f;
                if (delta > half) delta -= _path.Length;
                else if (delta < -half) delta += _path.Length;
            }

            _hasProjection = true;
            _segment = projection.Segment;
            Progress = projection.Distance;
            lateralOffset = projection.LateralOffset;
            return delta;
        }

        // Pure pursuit: aim at a point `lookahead` metres further along the path, shifted into the current lane.
        private float SteerTowardLookahead(in AiSensorFrame frame)
        {
            float lookahead = _profile.lookaheadBase + _profile.lookaheadPerSpeed * Math.Abs(frame.ForwardSpeed);
            float targetDistance = Progress + lookahead;
            Vector3 target = _path.PositionAt(targetDistance)
                + PolylinePath.RightOf(_path.TangentAt(targetDistance)) * _lanes.Offset;

            float angleDeg = PolylinePath.SignedAngleXZ(frame.Forward, target - frame.Position) * Mathf.Rad2Deg;

            // Same formula the vehicle uses for its wheels, so "full lock" here is exactly the car's current full lock
            // (it shrinks at speed via highSpeedSteerFactor).
            float maxSteerDeg = VehicleMath.SteerAngle(1f, _vehicle.maxSteerAngle, frame.ForwardSpeed,
                _vehicle.topSpeed, _vehicle.highSpeedSteerFactor);
            if (maxSteerDeg <= 0f) return 0f;

            return Mathf.Clamp(angleDeg / maxSteerDeg * _profile.steerGain, -1f, 1f);
        }

        // Proportional throttle with a shrinking dead zone: errors inside ±coastBand give 0, larger errors
        // ramp from 0 (no jump at the band edge, which is what would cause pedal chatter).
        // Negative = brake while moving forward (VehicleMath.ResolveLongitudinal).
        private float ThrottleFor(float speedError)
        {
            float magnitude = Math.Abs(speedError) - _profile.coastBand;
            if (magnitude <= 0f) return 0f;
            return Mathf.Clamp(Math.Sign(speedError) * magnitude * _profile.throttleGain, -1f, 1f);
        }
    }
}
