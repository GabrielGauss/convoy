using UnityEngine;

namespace Convoy.Race
{
    /// <summary>What happens to uplink progress when the vehicle leaves the zone (or is too fast).</summary>
    public enum UplinkExitBehaviour : byte
    {
        /// <summary>Progress drops to zero at once (D-025 default).</summary>
        Reset = 0,
        /// <summary>Progress drains at <see cref="UplinkConfig.decayPerSecond"/>.</summary>
        Decay = 1,
    }

    /// <summary>
    /// Tuning for the Monolith uplink finale (D-025, ADR-006 § 5). A ScriptableObject so the
    /// hold length can be tuned in the Inspector without touching code (design data rule).
    /// </summary>
    [CreateAssetMenu(fileName = "UplinkConfig", menuName = "Convoy/Uplink Config")]
    public sealed class UplinkConfig : ScriptableObject
    {
        [Tooltip("Seconds the vehicle must stay inside the Monolith zone to finish.")]
        [Min(0.1f)] public float holdSeconds = 4f;
        [Tooltip("Above this speed the hold does not advance, km/h. 0 = no limit.")]
        [Min(0f)] public float maxHoldSpeedKmh = 0f;
        [Tooltip("What happens to progress when the vehicle leaves the zone (or is too fast).")]
        public UplinkExitBehaviour exitBehaviour = UplinkExitBehaviour.Reset;
        [Tooltip("Decay mode only: held seconds lost per second outside.")]
        [Min(0f)] public float decayPerSecond = 2f;
    }
}
