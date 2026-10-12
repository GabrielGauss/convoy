using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>Which kind of ground a wheel is on. Byte-sized so VehicleState stays small (LV-02b).</summary>
    public enum SurfaceKind : byte
    {
        Default = 0,
        ToxicMud = 1,
    }

    /// <summary>
    /// Designer-tunable ground surface (LV-02, D-027). A ScriptableObject so every number lives in an asset that
    /// designers tweak in the Inspector — no magic numbers in VehicleController. Defaults are the D-021 toxic mud
    /// ("-40 % traction and speed"). Nitro is deliberately NOT scaled by any field here: it is the counterplay to mud.
    /// </summary>
    [CreateAssetMenu(fileName = "SurfaceProfile", menuName = "Convoy/Surface Profile")]
    public sealed class SurfaceProfile : ScriptableObject
    {
        public SurfaceKind kind = SurfaceKind.ToxicMud;

        [Tooltip("Scales tireFriction (friction circle: max tyre force).")]
        [Range(0f, 2f)] public float frictionMultiplier = 0.6f;

        [Tooltip("Scales front/rear lateral grip.")]
        [Range(0f, 2f)] public float lateralGripMultiplier = 0.6f;

        [Tooltip("Scales engine drive force on this wheel.")]
        [Range(0f, 2f)] public float driveForceMultiplier = 0.6f;

        [Tooltip("Scales the speed where this wheel's drive fades out (nitro bonus is added unscaled).")]
        [Range(0.1f, 2f)] public float topSpeedMultiplier = 0.6f;

        [Tooltip("Scales rolling resistance (mud drag).")]
        [Range(0f, 10f)] public float rollingResistanceMultiplier = 3f;
    }

    /// <summary>
    /// Per-wheel multipliers resolved from a SurfaceProfile. A readonly struct: built on the stack every physics
    /// step, so it never touches the GC heap. <see cref="None"/> has every field exactly 1f — x * 1f is exact in
    /// IEEE 754, which is what keeps the tuned Jackal bit-identical off-surface (D-027, D-01 regression test).
    /// </summary>
    public readonly struct SurfaceModifiers
    {
        public readonly float Friction;
        public readonly float LateralGrip;
        public readonly float DriveForce;
        public readonly float TopSpeed;
        public readonly float RollingResistance;
        public readonly SurfaceKind Kind;

        private SurfaceModifiers(float friction, float lateralGrip, float driveForce, float topSpeed,
            float rollingResistance, SurfaceKind kind)
        {
            Friction = friction;
            LateralGrip = lateralGrip;
            DriveForce = driveForce;
            TopSpeed = topSpeed;
            RollingResistance = rollingResistance;
            Kind = kind;
        }

        /// <summary>Default ground: every multiplier the literal 1f (not a computed value that could be 0.99999).</summary>
        public static SurfaceModifiers None => new SurfaceModifiers(1f, 1f, 1f, 1f, 1f, SurfaceKind.Default);

        /// <summary>True when the wheel is on untagged/default ground (used to count surface wheels in VehicleState).</summary>
        public bool IsDefault => Kind == SurfaceKind.Default;

        /// <summary>
        /// Reads the profile's numbers each call (not cached) so Inspector edits apply live in Play mode, like VehicleConfig.
        /// null (untagged ground, or a destroyed asset — Unity's == treats that as null too) → None.
        /// A NaN or negative field (bad asset data / a script writing garbage) falls back to 1f for that field only,
        /// so one broken number can never make the car un-drivable or push forces the wrong way.
        /// </summary>
        public static SurfaceModifiers From(SurfaceProfile profile)
        {
            if (profile == null) return None;
            return new SurfaceModifiers(
                Sanitize(profile.frictionMultiplier),
                Sanitize(profile.lateralGripMultiplier),
                Sanitize(profile.driveForceMultiplier),
                Sanitize(profile.topSpeedMultiplier),
                Sanitize(profile.rollingResistanceMultiplier),
                profile.kind);
        }

        // NaN fails every comparison, so `!(m >= 0f)` catches both NaN and negatives in one test.
        private static float Sanitize(float multiplier) => !(multiplier >= 0f) ? 1f : multiplier;
    }
}
