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

            // Shortcuts must (TR-01c review) start and end ON their main route (within its width) and be genuinely
            // shorter than the main stretch they bypass, otherwise nobody takes the risk.
            // Chasm: branches 42 % along the canyon's (520,-520)->(740,-400) leg and runs straight to the canyon's
            // (420,40) node. 545 m against 733 m round the canyon loop (-26 %). The gap sits 150 m in, >= 150 m from
            // the canyon line, which stays the safe detour. Both junctions sit where the canyon bed is nearly level:
            // a branch leaving a steep stretch at an angle tilts the shared road sideways (drivability test, AC 3).
            var canyonBend = new Vector2(520f, -520f);
            var canyonEast = new Vector2(740f, -400f);
            var canyonJoin = new Vector2(420f, 40f);
            Vector2 chasmFrom = Vector2.Lerp(canyonBend, canyonEast, 0.42f);
            Vector2 chasmTo = canyonJoin;
            Vector2 chasmDir = (chasmTo - chasmFrom).normalized;
            float chasmYaw = Mathf.Atan2(chasmDir.x, chasmDir.y) * Mathf.Rad2Deg;
            const float chasmGap = 24f, rampLength = 10f, rampAngle = 12f, chasmFromBranch = 150f;
            Vector2 chasmCentre = chasmFrom + chasmDir * chasmFromBranch;

            // Rammer: desert branch (-300,-750) diagonally to 80 % along the desert's (-660,-690)->(-620,-450) leg,
            // (-628,-498). 414 m against 560 m via the desert's west bend (-26 %). Gate 180 m after the branch on the
            // straight: >= 120 m run-up (LV-01).
            var rammerFrom = new Vector2(-300f, -750f);
            var rammerTo = new Vector2(-628f, -498f);
            Vector2 rammerDir = (rammerTo - rammerFrom).normalized;
            float rammerYaw = Mathf.Atan2(rammerDir.x, rammerDir.y) * Mathf.Rad2Deg;
            const float rammerGateFromBranch = 180f;

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
                    // Across the shortcut: the whole corridor (12 m road + 2 x 10 m shoulders) plus margin, so the gap
                    // cannot be skirted along a shoulder. Safe now that the canyon line is >= 150 m away.
                    length = 36f,
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
                            // Loops east round the chasm shortcut: the long, safe way.
                            start, new Vector2(250f, -760f), canyonBend, canyonEast,
                            new Vector2(700f, -160f), canyonJoin, centre,
                        },
                    },
                    new RouteDef
                    {
                        // The cave is this route's covered stretch through the crater rim (~160 m around (-40,-250)).
                        // No shortcutId on purpose: the cave is on the ONLY river route, so world variation must never
                        // close it (TR-01c review). It is a fixed, always-open covered section.
                        id = River, kind = RouteKind.Main, width = mainWidth, shoulder = shoulder,
                        nodes = new[] { start, new Vector2(-60f, -600f), new Vector2(-60f, -430f), new Vector2(-40f, -250f), centre },
                        coveredRange = new Vector2(538f, 698f),
                    },
                    new RouteDef
                    {
                        id = Desert, kind = RouteKind.Main, width = mainWidth, shoulder = shoulder,
                        nodes = new[]
                        {
                            // West bend first (the long way the rammer shortcut cuts), then north through the ford
                            // and the minefield (the (-620,-450)->(-650,-100) leg passes the field centre).
                            start, rammerFrom, new Vector2(-660f, -690f), new Vector2(-620f, -450f),
                            new Vector2(-650f, -100f), new Vector2(-400f, 80f), centre,
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
                        kind = PlacementKind.RammerGate, position = rammerFrom + rammerDir * rammerGateFromBranch, yawDeg = rammerYaw,
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
                    // Rockfall blockers (active = shortcut closed this run, TR-01c). Only real shortcuts get one: the
                    // cave is on the only river route and is never closed.
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
