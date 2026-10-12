using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Tuning for the hull-health pool, impact damage and handling degradation (ADR-003 / D-018).
    /// All gameplay numbers live here, never in behaviour code. Defaults are the Jackal's.
    /// A ScriptableObject (not a plain class) so designers edit it in the Inspector and
    /// several vehicles can share or fork one asset.
    /// </summary>
    [CreateAssetMenu(fileName = "DamageConfig", menuName = "Convoy/Damage Config")]
    public sealed class DamageConfig : ScriptableObject
    {
        [Header("Health")]
        [Tooltip("Hull points.")]
        [Min(1f)] public float maxHealth = 100f;

        [Header("Impact damage (collision delta-v)")]
        [Tooltip("Collision delta-v (m/s) below this is free (about a 29 km/h nudge).")]
        [Min(0f)] public float impactSpeedThreshold = 8f;

        [Tooltip("Higher threshold (m/s) for contacts whose normal points up: landing a jump, settling after a reset.")]
        [Min(0f)] public float landingSpeedThreshold = 14f;

        [Tooltip("Contact normal dot world-up above this counts as a landing contact.")]
        [Range(0f, 1f)] public float landingNormalDot = 0.7f;

        [Tooltip("HP per m/s of delta-v above the threshold.")]
        [Min(0f)] public float impactDamagePerMps = 2.5f;

        [Tooltip("Cap on damage from a single impact.")]
        [Min(0f)] public float maxImpactDamage = 35f;

        [Tooltip("Multi-contact bounces inside this window (s) count once; the largest wins.")]
        [Min(0f)] public float impactWindowSeconds = 0.25f;

        [Header("Handling degradation")]
        [Tooltip("At or above this health fraction there is NO handling effect (multipliers are exactly 1).")]
        [Range(0f, 1f)] public float degradeBelowHealth01 = 0.6f;

        [Tooltip("Top speed multiplier at 0 HP.")]
        [Range(0f, 1f)] public float minTopSpeedMultiplier = 0.75f;

        [Tooltip("Engine force multiplier at 0 HP.")]
        [Range(0f, 1f)] public float minEngineForceMultiplier = 0.70f;

        [Tooltip("Lateral grip multiplier at 0 HP (front and rear).")]
        [Range(0f, 1f)] public float minGripMultiplier = 0.80f;

        [Tooltip("Max steer angle multiplier at 0 HP.")]
        [Range(0f, 1f)] public float minSteerMultiplier = 0.90f;
    }
}
