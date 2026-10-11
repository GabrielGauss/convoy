using System;
using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Read-only snapshot of the vehicle simulation after each physics step (D-007).
    /// Presentation (camera, HUD, debug overlay) reads this; it never writes to the simulation.
    /// </summary>
    [Serializable]
    public struct VehicleState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;

        /// <summary>Signed speed along the vehicle's forward axis, m/s.</summary>
        public float ForwardSpeed;

        /// <summary>Sideways speed at the centre of mass, m/s (a rough drift indicator).</summary>
        public float LateralSpeed;

        /// <summary>Angle between heading and direction of travel, degrees (0 when below walking speed).</summary>
        public float SlipAngle;

        public int GroundedWheels;
        public int TotalWheels;

        /// <summary>True when any tyre hit its friction limit this step.</summary>
        public bool Sliding;

        /// <summary>Seconds the vehicle has been stuck upside down (V-02 auto-right triggers at the configured delay).</summary>
        public float UpsideDownTime;

        /// <summary>Seconds until another reset is allowed.</summary>
        public float RecoveryCooldown;

        public bool IsAirborne => GroundedWheels == 0;
        public float SpeedKmh => Velocity.magnitude * 3.6f;
    }
}
