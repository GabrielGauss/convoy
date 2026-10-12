using UnityEngine;

namespace Convoy.Crew
{
    /// <summary>
    /// Tuning numbers for crew/seat logic. A ScriptableObject is a data asset that lives in the project,
    /// so designers can change these values in the Inspector without touching code (project rule: no magic numbers).
    /// </summary>
    [CreateAssetMenu(fileName = "CrewConfig", menuName = "Convoy/Crew Config")]
    public sealed class CrewConfig : ScriptableObject
    {
        [Tooltip("A Pilot throttle/steer axis counts as 'active' only above this magnitude (ignores stick drift).")]
        [Range(0f, 0.5f)] public float activityAxisDeadzone = 0.05f;

        [Tooltip("The Gunner counts as 'active' when the aim moved more than this many degrees since the previous step.")]
        [Min(0f)] public float activityAimDegrees = 0.5f;

        // K-01 adds: switchSeconds = 1f
    }
}
