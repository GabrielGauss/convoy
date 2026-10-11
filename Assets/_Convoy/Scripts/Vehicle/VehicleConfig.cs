using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>
    /// All tunable numbers for one vehicle archetype (D-004). Create via
    /// Assets ▸ Create ▸ Convoy ▸ Vehicle Config. Values can be changed in Play mode
    /// and take effect on the next physics step.
    /// Defaults are a starting point for the R-01 JACKAL (light, agile), not final tuning.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleConfig", menuName = "Convoy/Vehicle Config")]
    public sealed class VehicleConfig : ScriptableObject
    {
        [Header("Body")]
        [Tooltip("Rigidbody mass in kg.")]
        [Min(1f)] public float mass = 1200f;
        [Tooltip("Centre of mass relative to the vehicle root. Lower = harder to flip.")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);
        [Tooltip("Rigidbody angular damping. Higher = less spinning out.")]
        [Min(0f)] public float angularDamping = 0.6f;

        [Header("Suspension")]
        [Tooltip("Spring travel when fully extended, metres.")]
        [Min(0.01f)] public float suspensionRestLength = 0.45f;
        [Min(0.05f)] public float wheelRadius = 0.38f;
        [Tooltip("Spring stiffness per wheel, N/m.")]
        [Min(0f)] public float springStiffness = 35000f;
        [Tooltip("Damper strength per wheel, N·s/m.")]
        [Min(0f)] public float springDamping = 3500f;

        [Header("Engine & brakes")]
        [Tooltip("Total drive force at standstill, N (split across driven wheels).")]
        [Min(0f)] public float engineForce = 9000f;
        [Tooltip("Forward speed where drive force fades to zero, m/s (40 m/s = 144 km/h).")]
        [Min(1f)] public float topSpeed = 40f;
        [Min(1f)] public float reverseTopSpeed = 12f;
        [Tooltip("Total brake force, N (split across all wheels).")]
        [Min(0f)] public float brakeForce = 14000f;
        [Tooltip("Below this speed, holding brake starts reversing, m/s.")]
        [Min(0f)] public float reverseThreshold = 0.6f;
        [Tooltip("Coasting deceleration per wheel, as a fraction of wheel speed per second.")]
        [Min(0f)] public float rollingResistance = 0.05f;

        [Header("Tyres")]
        [Tooltip("Tyre friction coefficient. Max tyre force = mu × normal force.")]
        [Min(0f)] public float tireFriction = 1.25f;
        [Tooltip("Fraction of sideways sliding removed per step at the front axle (0..1).")]
        [Range(0f, 1f)] public float frontLateralGrip = 0.45f;
        [Tooltip("Fraction of sideways sliding removed per step at the rear axle (0..1).")]
        [Range(0f, 1f)] public float rearLateralGrip = 0.40f;

        [Header("Steering")]
        [Range(0f, 60f)] public float maxSteerAngle = 32f;
        [Tooltip("Fraction of steering left at top speed.")]
        [Range(0f, 1f)] public float highSpeedSteerFactor = 0.35f;

        [Header("Handbrake / drift")]
        [Tooltip("Rear grip multiplier while the handbrake is held. Lower = bigger drifts.")]
        [Range(0f, 1f)] public float handbrakeRearGripMultiplier = 0.3f;
        [Tooltip("Brake force applied to each rear wheel while the handbrake is held, N.")]
        [Min(0f)] public float handbrakeBrakeForce = 2500f;

        [Header("Aerodynamics")]
        [Tooltip("Downforce = this × speed², N. Keeps the car planted at speed.")]
        [Min(0f)] public float downforce = 2.5f;

        [Header("Air control")]
        [Tooltip("Pitch/yaw/roll acceleration while airborne, rad/s².")]
        [Min(0f)] public float airControl = 2.5f;
    }
}
