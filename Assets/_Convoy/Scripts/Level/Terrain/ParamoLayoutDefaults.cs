using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Starting values for Páramo_01, transcribed from the TR-01 spec table. Only used to seed a new
    /// <see cref="TerrainLayout"/> asset; after that the asset is the source of truth (tune there, not here).
    /// Coordinates: world XZ centred on the Monolith, +Z = north, player starts on the south edge.
    /// </summary>
    public static class ParamoLayoutDefaults
    {
        public const string Canyon = "canyon", River = "river", Desert = "desert";
        public const string ShortcutChasm = "chasm", ShortcutRammer = "rammer", ShortcutCave = "cave";

        public static TerrainLayoutData Create()
        {
            var start = new Vector2(0f, -860f);
            var centre = Vector2.zero;
            const float mainWidth = 12f, shoulder = 10f, rivalWidth = 10f;

            // Chasm shortcut runs (520,-520) -> (600,-300); the gap sits where it crosses x = 560 (half way).
            var chasmFrom = new Vector2(520f, -520f);
            var chasmTo = new Vector2(600f, -300f);
            Vector2 chasmDir = (chasmTo - chasmFrom).normalized;
            float chasmYaw = Mathf.Atan2(chasmDir.x, chasmDir.y) * Mathf.Rad2Deg;
            Vector2 chasmCentre = (chasmFrom + chasmTo) * 0.5f;
            const float chasmGap = 24f, rampLength = 10f, rampAngle = 12f;

            var rammerFrom = new Vector2(-300f, -750f);
            var rammerTo = new Vector2(-480f, -480f);
            Vector2 rammerDir = (rammerTo - rammerFrom).normalized;
            float rammerYaw = Mathf.Atan2(rammerDir.x, rammerDir.y) * Mathf.Rad2Deg;

            return new TerrainLayoutData
            {
                generatorVersion = 1,
                seed = 1,
                sizeMetres = 2000f,
                heightScale = 300f,
                baseHeight = 80f,
                heightmapResolution = 1025,
                alphamapResolution = 512,
                noise = new[]
                {
                    new NoiseRegion { centre = Vector2.zero, radius = 0f, falloff = 0f, wavelength = 600f, amplitude = 14f, octaves = 5 },
                    new NoiseRegion { centre = new Vector2(600f, -300f), radius = 450f, falloff = 150f, wavelength = 220f, amplitude = 40f, octaves = 4, ridged = true },
                    new NoiseRegion { centre = new Vector2(-600f, -250f), radius = 450f, falloff = 150f, wavelength = 120f, amplitude = 6f, octaves = 3 },
                },
                edgeBand = 120f,
                edgeRise = 90f,
                crater = new CraterDef
                {
                    centre = centre,
                    rimRadius = 260f, rimHeight = 35f,
                    floorRadius = 110f, floorDepth = 25f,
                    outerRadius = 520f,
                    plateauRadius = 30f, plateauHeight = 3f,
                },
                river = new RiverDef
                {
                    nodes = new[]
                    {
                        new Vector2(-1000f, -300f), new Vector2(-500f, -380f), new Vector2(-100f, -420f),
                        new Vector2(200f, -450f), new Vector2(380f, -560f),
                    },
                    width = 40f, depth = 6f, shoulder = 15f,
                },
                chasm = new ChasmDef
                {
                    centre = chasmCentre,
                    yawDeg = chasmYaw,
                    gapWidth = chasmGap,
                    // Across the shortcut. Capped at 16 m because at x = 560 the canyon main line (the safe detour) is only
                    // ~18 m away; a longer cut would sever it.
                    length = 16f,
                    depth = 30f,
                    rampAngleDeg = rampAngle,
                    landingDrop = 2f,
                },
                routes = new[]
                {
                    new RouteDef
                    {
                        id = Canyon, kind = RouteKind.Main, width = mainWidth, shoulder = shoulder, carveDepth = 8f,
                        nodes = new[]
                        {
                            start, new Vector2(250f, -760f), chasmFrom, new Vector2(640f, -300f),
                            new Vector2(600f, -120f), new Vector2(420f, 40f), centre,
                        },
                    },
                    new RouteDef
                    {
                        // The cave is this route's covered stretch through the crater rim (~160 m around (-40,-250)).
                        id = River, kind = RouteKind.Main, shortcutId = ShortcutCave, width = mainWidth, shoulder = shoulder,
                        nodes = new[] { start, new Vector2(-60f, -600f), new Vector2(-60f, -430f), new Vector2(-40f, -250f), centre },
                        coveredRange = new Vector2(538f, 698f),
                    },
                    new RouteDef
                    {
                        id = Desert, kind = RouteKind.Main, width = mainWidth, shoulder = shoulder,
                        nodes = new[]
                        {
                            start, rammerFrom, new Vector2(-620f, -450f), new Vector2(-650f, -100f),
                            new Vector2(-400f, 80f), centre,
                        },
                    },
                    new RouteDef
                    {
                        id = ShortcutChasm, kind = RouteKind.Shortcut, shortcutId = ShortcutChasm,
                        width = mainWidth, shoulder = shoulder, nodes = new[] { chasmFrom, chasmTo },
                    },
                    new RouteDef
                    {
                        id = ShortcutRammer, kind = RouteKind.Shortcut, shortcutId = ShortcutRammer,
                        width = mainWidth, shoulder = shoulder, nodes = new[] { rammerFrom, rammerTo },
                    },
                    new RouteDef
                    {
                        id = "rival_w", kind = RouteKind.Rival, width = rivalWidth, shoulder = shoulder,
                        nodes = new[] { new Vector2(-860f, 150f), new Vector2(-400f, 80f), centre },
                    },
                    new RouteDef
                    {
                        id = "rival_n", kind = RouteKind.Rival, width = rivalWidth, shoulder = shoulder,
                        nodes = new[] { new Vector2(0f, 860f), new Vector2(0f, 300f), centre },
                    },
                    new RouteDef
                    {
                        id = "rival_e", kind = RouteKind.Rival, width = rivalWidth, shoulder = shoulder,
                        nodes = new[] { new Vector2(860f, 250f), new Vector2(420f, 40f), centre },
                    },
                },
                obstacles = new[]
                {
                    new ObstaclePlacement { kind = PlacementKind.Monolith, position = centre, scale = new Vector3(8f, 80f, 8f) },
                    // Gate on the rammer shortcut 180 m after the branch: >= 120 m straight run-up (LV-01).
                    new ObstaclePlacement
                    {
                        kind = PlacementKind.RammerGate, position = new Vector2(-400f, -600f), yawDeg = rammerYaw,
                        scale = Vector3.one, shortcutId = ShortcutRammer,
                    },
                    // Mud at both river fords (LV-02): river route at x ~ -60, desert route at x ~ -628. Long axis along the road.
                    new ObstaclePlacement { kind = PlacementKind.MudPatch, position = new Vector2(-59.3f, -424f), yawDeg = 6.3f, scale = new Vector3(16f, 1f, 48f) },
                    new ObstaclePlacement { kind = PlacementKind.MudPatch, position = new Vector2(-627.8f, -359.5f), yawDeg = -4.9f, scale = new Vector3(16f, 1f, 48f) },
                    // Take-off wedge ending at the near lip: scale = (road width, length x tan(ramp), length).
                    new ObstaclePlacement
                    {
                        kind = PlacementKind.ChasmRamp, position = chasmCentre - chasmDir * (chasmGap * 0.5f + rampLength * 0.5f),
                        yawDeg = chasmYaw, scale = new Vector3(mainWidth, rampLength * Mathf.Tan(rampAngle * Mathf.Deg2Rad), rampLength),
                        shortcutId = ShortcutChasm,
                    },
                    // Rockfall blockers (active = shortcut closed this run, TR-01c).
                    new ObstaclePlacement
                    {
                        kind = PlacementKind.ShortcutBlocker, position = chasmCentre - chasmDir * 37f, yawDeg = chasmYaw,
                        scale = new Vector3(16f, 6f, 10f), shortcutId = ShortcutChasm,
                    },
                    new ObstaclePlacement
                    {
                        kind = PlacementKind.ShortcutBlocker, position = rammerFrom + rammerDir * 80f, yawDeg = rammerYaw,
                        scale = new Vector3(16f, 6f, 10f), shortcutId = ShortcutRammer,
                    },
                    new ObstaclePlacement
                    {
                        kind = PlacementKind.ShortcutBlocker, position = new Vector2(-48.8f, -329.6f), yawDeg = 6.3f,
                        scale = new Vector3(16f, 6f, 10f), shortcutId = ShortcutCave,
                    },
                },
                spawns = new[]
                {
                    new SpawnDef { id = "player", position = start, yawDeg = 0f, rival = false, padRadius = 25f },
                    new SpawnDef { id = "rival_w", position = new Vector2(-860f, 150f), yawDeg = 90f, rival = true, padRadius = 25f },
                    new SpawnDef { id = "rival_n", position = new Vector2(0f, 860f), yawDeg = 180f, rival = true, padRadius = 25f },
                    new SpawnDef { id = "rival_e", position = new Vector2(860f, 250f), yawDeg = -90f, rival = true, padRadius = 25f },
                },
                maxRouteSlopeDeg = 18f,
                routeSampleSpacing = 2f,
                maxJunctionSlopeDeg = 2f,
                basaltSlopeMinDeg = 28f,
                basaltSlopeMaxDeg = 40f,
            };
        }
    }
}
