using System;

namespace Convoy.Core
{
    // Seat commands for the Gunner and Engineer stations (ADR-002 section 2).
    // Convoy.Core has "noEngineReferences", so these use plain float/int/bool only (no Vector3).
    // Commands are target-state snapshots: re-sending the same one is harmless, and default(T) is
    // always the safe "nobody is here" command.

    /// <summary>
    /// Gunner seat request for one step. Aim is in WORLD space so the turret stays on target while the
    /// vehicle turns (D-003). HasAim = false means "hold the current turret orientation".
    /// </summary>
    [Serializable]
    public struct GunnerCommand
    {
        public bool HasAim;

        /// <summary>World yaw, degrees, [0, 360). 0 = world +Z, 90 = world +X (Unity's clockwise-from-above convention).</summary>
        public float AimYawDegrees;

        /// <summary>Degrees above the horizon, clamped to [-89, 89] by <see cref="Clamped"/>. The turret applies its own tighter limits.</summary>
        public float AimPitchDegrees;

        public bool Fire;

        public const float MaxPitchDegrees = 89f;

        public static GunnerCommand Neutral => default;

        /// <summary>Wraps yaw into [0, 360) and clamps pitch to [-89, 89]. Other fields are copied unchanged.</summary>
        public GunnerCommand Clamped()
        {
            // C# '%' keeps the sign of the dividend, so negative yaws need +360. The final check guards
            // a float edge case: -1e-9f + 360f rounds to exactly 360f, which would break the [0,360) promise.
            float yaw = AimYawDegrees % 360f;
            if (yaw < 0f) yaw += 360f;
            if (yaw >= 360f) yaw = 0f;

            return new GunnerCommand
            {
                HasAim = HasAim,
                AimYawDegrees = yaw,
                AimPitchDegrees = Math.Clamp(AimPitchDegrees, -MaxPitchDegrees, MaxPitchDegrees),
                Fire = Fire,
            };
        }
    }

    /// <summary>Engineer power focus (M-01). Keep = 0 so default(EngineerCommand) never changes the power split.</summary>
    public enum PowerFocus : byte
    {
        Keep = 0,
        Balanced = 1,
        Engine = 2,
        Weapons = 3,
        Cooling = 4,
    }

    /// <summary>Engineer seat request for one step. N-01 will add ping fields; adding fields is non-breaking.</summary>
    [Serializable]
    public struct EngineerCommand
    {
        /// <summary>Target power focus; Keep = leave as is.</summary>
        public PowerFocus Focus;

        /// <summary>Held = repairing (M-02 decides rate and what it blocks).</summary>
        public bool Repair;

        public static EngineerCommand Neutral => default;
    }

    /// <summary>Anything that accepts Gunner commands (the turret station, G-01).</summary>
    public interface IGunnerCommandReceiver
    {
        void SetGunnerCommand(GunnerCommand command);
    }

    /// <summary>Anything that accepts Engineer commands (power/heat/repair station, M-01).</summary>
    public interface IEngineerCommandReceiver
    {
        void SetEngineerCommand(EngineerCommand command);
    }
}
