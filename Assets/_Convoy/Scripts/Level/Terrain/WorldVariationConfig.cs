using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Per-run world variation for an open map (D-031: retry value comes from the world). Picks dust storm on/off,
    /// which shortcuts are open and which mines are live; the pure rules are in <see cref="WorldVariationRules"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldVariationConfig", menuName = "Convoy/World Variation Config")]
    public sealed class WorldVariationConfig : ScriptableObject
    {
        [Tooltip("0 = new seed every run (scene load). Any other value replays the same world (debugging, playtests).")]
        public int fixedSeed;

        [Header("Weather")]
        [Range(0f, 1f)] public float dustStormChance = 0.3f;
        public Color clearFogColor = new Color(0.78f, 0.62f, 0.45f);
        public Color stormFogColor = new Color(0.62f, 0.45f, 0.30f);
        [Tooltip("Exponential-squared fog density.")]
        [Min(0f)] public float clearFogDensity = 0.0012f;
        [Min(0f)] public float stormFogDensity = 0.008f;

        [Header("Shortcuts")]
        [Tooltip("Bit i of WorldVariation.OpenShortcutMask = shortcutIds[i] (max 32). Ids match RouteDef.shortcutId.")]
        public string[] shortcutIds = { "chasm", "rammer", "cave" };
        [Min(1)] public int minOpenShortcuts = 1;
        [Min(1)] public int maxOpenShortcuts = 2;

        [Header("Minefield")]
        public Vector2 minefieldCentre = new Vector2(-640f, -250f);
        [Min(1f)] public float minefieldRadius = 90f;
        [Min(1)] public int mineSlotCount = 24;
        [Min(1)] public int activeMines = 10;
        [Tooltip("No live mine within this distance of the chosen safe lane's centreline, m.")]
        [Min(0f)] public float safeLaneHalfWidth = 6f;
        [Tooltip("Metres, lateral to the desert route through the field (+ = right of travel).")]
        public float[] safeLaneOffsets = { -40f, 0f, 40f };
        [Tooltip("How far each mine slot may wander from its grid cell centre, as a fraction of the cell size.")]
        [Range(0f, 0.5f)] public float mineSlotJitter = 0.3f;
    }
}
