using System;
using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Everything the Páramo terrain is generated from (TR-01, D-031). Plain serializable data so the pure synthesis
    /// (<see cref="TerrainSynthesis"/>) is testable in EditMode and the editor generator (TR-01b) can rebuild the scene
    /// from the asset at any time. World XZ is centred on the terrain middle (the Monolith at (0, 0)), +Z = north;
    /// heights are metres above the terrain origin. Tune in the <see cref="TerrainLayout"/> asset, never in code.
    /// </summary>
    [Serializable]
    public sealed class TerrainLayoutData
    {
        [Tooltip("Bumped when the synthesis changes meaning; the generator refuses layouts newer than itself.")]
        public int generatorVersion = 1;
        [Tooltip("Noise seed. Same seed + same data = bit-identical heights.")]
        public int seed = 1;

        [Header("Terrain")]
        public float sizeMetres = 2000f;
        public float heightScale = 300f;
        [Tooltip("Mean ground level, m above the terrain origin. Leaves room below for the river, chasm and crater floor.")]
        public float baseHeight = 80f;
        public int heightmapResolution = 1025;
        public int alphamapResolution = 512;

        [Header("Shape")]
        public NoiseRegion[] noise;
        [Tooltip("Width of the world-edge berm, m. It rises to edgeRise at the border: too steep to drive, so no invisible wall is needed.")]
        public float edgeBand = 120f;
        public float edgeRise = 90f;
        public CraterDef crater;
        public RiverDef river;
        public ChasmDef chasm;

        [Header("Gameplay layout")]
        public RouteDef[] routes;
        public ObstaclePlacement[] obstacles;
        public SpawnDef[] spawns;

        [Header("Drivability")]
        [Tooltip("Max longitudinal grade a carved route bed may have, degrees (LV-03 limit is 20).")]
        public float maxRouteSlopeDeg = 18f;
        [Tooltip("Spacing used when sampling route centrelines for checks and exports, m.")]
        public float routeSampleSpacing = 2f;
        [Tooltip("Where two routes overlap (branches, merges, shared segments) a later route's bed may drift from the " +
                 "earlier one by at most tan(this) per metre of separation, so the shared road has no side step. " +
                 "Smaller = branches stay level with the road they leave for longer.")]
        public float maxJunctionSlopeDeg = 2f;

        [Header("Splat")]
        [Tooltip("Slopes above this start to show basalt rock, degrees.")]
        public float basaltSlopeMinDeg = 28f;
        [Tooltip("Slopes above this are fully basalt, degrees.")]
        public float basaltSlopeMaxDeg = 40f;
    }

    /// <summary>One additive fBm layer. <c>radius</c> 0 = global; otherwise full strength inside radius, fading to 0 over <c>falloff</c>.</summary>
    [Serializable]
    public struct NoiseRegion
    {
        public Vector2 centre;
        public float radius, falloff, wavelength, amplitude;
        public int octaves;
        [Tooltip("Ridged noise (sharp crests, 0..amplitude) for canyon country; otherwise smooth -amplitude..+amplitude.")]
        public bool ridged;
    }

    /// <summary>Volcanic crater around the Monolith. Heights are relative to <c>baseHeight</c>; the inside is absolute (noise-free).</summary>
    [Serializable]
    public struct CraterDef
    {
        public Vector2 centre;
        public float rimRadius, rimHeight, floorRadius, floorDepth, outerRadius, plateauRadius, plateauHeight;
    }

    /// <summary>Toxic river: a channel <c>depth</c> below the local ground, <c>width</c> wide, easing out over <c>shoulder</c>.</summary>
    [Serializable]
    public struct RiverDef
    {
        public Vector2[] nodes;
        public float width, depth, shoulder;
    }

    /// <summary>
    /// Jumpable gap cut across a route. <c>yawDeg</c> is the travel direction of the jump (Unity yaw: 0 = +Z, 90 = +X);
    /// <c>gapWidth</c> is measured along it, <c>length</c> across it. The far side lands <c>landingDrop</c> below the near lip.
    /// <c>rampAngleDeg</c> is the take-off wedge the generator places (terrain itself has no ramp).
    /// </summary>
    [Serializable]
    public struct ChasmDef
    {
        public Vector2 centre;
        public float yawDeg, gapWidth, length, depth, rampAngleDeg, landingDrop;
    }

    public enum RouteKind { Main, Shortcut, Rival }

    /// <summary>
    /// A carved, drivable road. Polyline corners are rounded (radius = width + 2 x shoulder) so the bed is smooth.
    /// <c>carveDepth</c> sinks the bed below the surrounding ground (canyon). <c>coveredRange</c> = metres along the route
    /// under a cave roof, (0,0) none. <c>shortcutId</c> names the shortcut this route (or its covered range) belongs to.
    /// </summary>
    [Serializable]
    public struct RouteDef
    {
        public string id;
        public RouteKind kind;
        public string shortcutId;
        public Vector2[] nodes;
        public float width, shoulder, carveDepth;
        public Vector2 coveredRange;
    }

    public enum PlacementKind { Monolith, RammerGate, MudPatch, ChasmRamp, CaveRoof, ShortcutBlocker, Rock }

    /// <summary>Something the generator (TR-01b) places on the ground at <c>position</c>; <c>scale</c> is the object's size, m.</summary>
    [Serializable]
    public struct ObstaclePlacement
    {
        public PlacementKind kind;
        public Vector2 position;
        public float yawDeg;
        public Vector3 scale;
        public string shortcutId;
    }

    /// <summary>Vehicle spawn. The ground is flattened within <c>padRadius</c> and eased back to the terrain over another padRadius.</summary>
    [Serializable]
    public struct SpawnDef
    {
        public string id;
        public Vector2 position;
        public float yawDeg;
        public bool rival;
        public float padRadius;
    }
}
