namespace Convoy.Level
{
    /// <summary>
    /// What this run's world looks like. Value type so the runner (TR-01c) and presentation can read it without
    /// allocations. Bit i of <see cref="OpenShortcutMask"/> = <c>WorldVariationConfig.shortcutIds[i]</c> is open;
    /// <see cref="SafeLane"/> indexes <c>safeLaneOffsets</c> (-1 = none configured).
    /// </summary>
    public struct WorldVariation
    {
        public int Seed;
        public bool DustStorm;
        public uint OpenShortcutMask;
        public int SafeLane;

        public bool IsShortcutOpen(int index) => index >= 0 && index < 32 && (OpenShortcutMask & (1u << index)) != 0;
    }
}
