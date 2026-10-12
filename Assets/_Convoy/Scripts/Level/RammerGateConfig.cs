using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Tuning for a Rammer gate (speed-breakable rock wall, LV-01, D-022). All gameplay numbers live here
    /// (CLAUDE.md rule 4) so Gabriel can feel-tune in the Inspector without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "RammerGateConfig", menuName = "Convoy/Rammer Gate Config")]
    public sealed class RammerGateConfig : ScriptableObject
    {
        [Header("Break rule")]
        [Tooltip("Closing speed along the wall normal needed to break it, km/h. Glancing hits count only their normal component.")]
        [Min(1f)] public float breakSpeedKmh = 80f;
        [Tooltip("Lighter bodies (barrels, debris) never break it, kg. Jackal = 1200.")]
        [Min(0f)] public float minBreakerMassKg = 800f;
        [Tooltip("Breaker keeps this fraction of its horizontal speed after smashing through.")]
        [Range(0f, 1f)] public float speedRetention = 0.85f;
        [Tooltip("Depth of the trigger sensor in front of each face, m. Must exceed max speed x fixed step x 2 (52 m/s x 0.02 x 2 = 2.1).")]
        [Min(0.5f)] public float sensorDepth = 2.5f;

        [Header("Debris")]
        [Min(1)] public int columns = 4;
        [Min(1)] public int rows = 3;
        [Tooltip("Seam irregularity as a fraction of chunk size (visual only).")]
        [Range(0f, 0.3f)] public float jitter = 0.12f;
        [Min(1f)] public float chunkMassKg = 40f;
        [Tooltip("Chunk velocity = breaker velocity x factor + spread.")]
        [Range(0f, 1.5f)] public float debrisSpeedFactor = 0.6f;
        [Min(0f)] public float debrisSpreadMps = 3f;
        [Tooltip("After this long chunks freeze (kinematic, colliders off).")]
        [Min(0.5f)] public float debrisSettleSeconds = 6f;
    }
}
