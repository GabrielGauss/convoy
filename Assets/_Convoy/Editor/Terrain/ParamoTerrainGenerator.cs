using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Convoy.Level;
using Convoy.Presentation;
using Convoy.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Convoy.Editor
{
    /// <summary>
    /// TR-01b: builds <c>Paramo_01.unity</c> from the <see cref="TerrainLayout"/> asset (spec docs/specs/TR-01.md,
    /// D-030, D-031). Nobody hand-edits the scene: change the layout asset, re-run the menu item.
    ///
    /// What it owns (and therefore may replace on every run):
    ///   - assets under Assets/_Convoy/Terrain/ (TerrainData updated in place so its GUID never changes),
    ///   - the scene object <c>Paramo_Terrain</c> (reused, never recreated once it exists),
    ///   - every child of the root <c>Paramo_Generated</c> (deleted and rebuilt: the idempotency contract).
    /// What it only creates when missing and otherwise leaves alone: the layout and world-variation assets, the terrain
    /// layer textures, the directional light, the player Jackal, the main camera and the global post-process volume.
    /// Nothing else in the scene is touched.
    /// </summary>
    public static class ParamoTerrainGenerator
    {
        /// <summary>Layouts with a higher <c>generatorVersion</c> were authored for a newer generator: refused.</summary>
        public const int GeneratorVersion = 1;

        public const string DefaultScenePath = "Assets/_Convoy/Scenes/Paramo_01.unity";
        public const string RootName = "Paramo_Generated";
        public const string TerrainObjectName = "Paramo_Terrain";
        public const string RoutesGroupName = "Routes";

        // ------------------------------------------------------------------ asset paths
        private const string LayoutPath = "Assets/_Convoy/Config/Level/Paramo_01_Layout.asset";
        private const string VariationPath = "Assets/_Convoy/Config/Level/Paramo_WorldVariation.asset";
        private const string TerrainFolder = "Assets/_Convoy/Terrain";
        private const string TerrainDataPath = TerrainFolder + "/Paramo_01_TerrainData.asset";
        private const string LayerFolder = TerrainFolder + "/Layers";
        private const string MaterialFolder = TerrainFolder + "/Materials";
        private const string RiverMeshPath = TerrainFolder + "/Paramo_01_River.asset";
        private const string ToxicMaterialPath = MaterialFolder + "/M_ToxicRiver.mat";
        private const string MineMaterialPath = MaterialFolder + "/M_MineSlot.mat";

        private const string JackalPrefabPath = "Assets/_Convoy/Prefabs/Jackal.prefab";
        private const string RammerGatePrefabPath = "Assets/_Convoy/Prefabs/RammerGate.prefab"; // LV-01c, soft dependency
        private const string MudPatchPrefabPath = "Assets/_Convoy/Prefabs/MudPatch.prefab";     // LV-02c, soft dependency
        private const string MonolithModelPath = "Assets/_Convoy/Art/Props/Monolith.fbx";
        private const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";   // same look as Sprint_01

        private const string ArtMaterials = "Assets/_Convoy/Art/Materials/";
        private const string MonolithGlowMat = ArtMaterials + "M_MonolithGlow.mat";
        private const string BasaltMat = ArtMaterials + "M_CliffDark.mat";
        private const string RockMat = ArtMaterials + "M_Rock.mat";
        private const string SteelMat = ArtMaterials + "M_Steel.mat";
        private const string GateGlowMat = ArtMaterials + "M_GateGlow.mat";
        private const string DirtMat = ArtMaterials + "M_DirtRoad.mat";

        // ------------------------------------------------------------------ scene object names
        private const string PlayerName = "Jackal";
        private const string CameraName = "Main Camera";
        private const string SunName = "Sun";
        private const string VolumeName = "Global Volume";

        // ------------------------------------------------------------------ render / look constants
        // These are rendering and greybox-art values fixed by the TR-01 spec (§ TR-01b generator, Performance budget),
        // not gameplay tuning, so they live here rather than in the layout asset.
        private const float HeightmapPixelError = 5f;  // spec: LOD error in pixels; 5 keeps 60 fps on a mid PC
        private const float BasemapDistance = 400f;    // spec: beyond this the terrain draws one baked colour map
        private const float CameraFarClip = 1500f;     // spec: hidden by the dust fog anyway
        private const float BeamDiameter = 6f, BeamHeight = 400f; // Monolith beam: 6 m wide so it still reads ~1 km away through the fog
        // Why ×4: the Sprint_01 FBX is sized for a 300 m course; on a 2 km map the Monolith must be the landmark you steer by.
        private const float MonolithLandmarkScale = 4f;
        private const float CaveClearance = 9f;        // spec: free height under the cave roof
        private const float CaveSlabLength = 20f;      // roof built from slabs this long (one box per slab)
        private const float CaveSlabThickness = 2f;
        private const float CaveSlabOverlap = 1f;      // slabs overlap so no light gaps show between them
        private const float RampThickness = 0.6f;
        private const float RampSink = 0.05f;          // tucks the ramp's leading edge under the road: no bump on entry
        private const float RockBury = 0.3f;           // fraction of a rock's height sunk into the ground
        private const float MineDiscRadius = 1.2f, MineDiscHeight = 0.12f;
        private const float RiverRibbonStep = 5f;      // ribbon cross-section spacing along the river, m
        private const float RiverFill = 1.5f;          // toxic liquid surface above the river bed, m
        private const int RiverSurfaceWindow = 6;      // ± samples used to find the bed level, so fords don't lift it
        private const float PlaceholderPoleHeight = 6f;
        private const float MudPlaceholderThickness = 0.05f;
        private const float SpawnDropHeight = 1.2f;    // Jackal spawns this far above the pad (as in Sprint_01) and settles
        private const int BlockerBoxes = 5;            // spec: 5-box rockfall
        private const float CameraStartBack = 7f, CameraStartUp = 3f; // ≈ SandboxChaseCamera's settled framing

        // Monolith fill light, copied from Sprint_01 so the landmark reads the same.
        private static readonly Vector3 MonolithLightOffset = new Vector3(0f, 12f, -4f);
        private static readonly Color MonolithLightColour = new Color(0.6f, 0.9f, 1f);
        private const float MonolithLightIntensity = 6f, MonolithLightRange = 40f;

        // Sun, copied from Sprint_01's directional light.
        private static readonly Color SunColour = new Color(1f, 0.84f, 0.68f);
        private const float SunIntensity = 1.6f;
        private static readonly Vector3 SunEuler = new Vector3(50f, -30f, 0f);

        private static readonly Color ToxicColour = new Color(0.30f, 0.55f, 0.08f);
        private static readonly Color ToxicEmission = new Color(0.35f, 1.2f, 0.10f);
        private static readonly Color MineColour = new Color(0.6f, 0.05f, 0.05f);
        private static readonly Color MineEmission = new Color(2.5f, 0.15f, 0.1f);

        [MenuItem("Convoy/Terrain/Generate Páramo_01")]
        public static void GenerateDefault()
        {
            ApplyToScene(DefaultScenePath);
        }

        /// <summary>
        /// Opens (or creates) <paramref name="scenePath"/>, regenerates the Páramo terrain and placements into it and
        /// saves it. Safe to run any number of times (D-030 integration entry point; the orchestrator calls it).
        /// </summary>
        public static void ApplyToScene(string scenePath)
        {
            var clock = Stopwatch.StartNew();

            // Layout first: refuse before touching the scene or any asset.
            EnsureFolder("Assets/_Convoy/Config/Level");
            TerrainLayout layoutAsset = LoadOrCreate<TerrainLayout>(LayoutPath);
            TerrainLayoutData layout = layoutAsset.data;
            if (layout.generatorVersion > GeneratorVersion)
            {
                Debug.LogError($"[Paramo] {LayoutPath} has generatorVersion {layout.generatorVersion}, this generator is " +
                               $"{GeneratorVersion}. Update the generator; nothing was changed.", layoutAsset);
                return;
            }
            WorldVariationConfig variation = LoadOrCreate<WorldVariationConfig>(VariationPath);

            if (!OpenOrCreateScene(scenePath, out Scene scene)) return;

            // ---- terrain assets
            EnsureFolder(LayerFolder);
            EnsureFolder(MaterialFolder);
            HeightField heights = TerrainSynthesis.Build(layout);
            TerrainLayer[] layers = TerrainTextureFactory.EnsureLayers(LayerFolder, layout.seed);
            TerrainData data = BuildTerrainData(layout, heights, layers);
            if (data == null) return;
            Terrain terrain = EnsureTerrainObject(scene, data, layout);

            // ---- scene environment (created once, then left to humans)
            EnsureSun(scene);
            ApplyFog(variation);

            // ---- generated content (rebuilt every run)
            int placeholders = 0;
            GameObject root = RebuildRoot(scene);
            Transform landmarks = Group(root, "Landmarks");
            Transform spawns = Group(root, "Spawns");
            Transform obstacles = Group(root, "Obstacles");
            Transform blockers = Group(root, "ShortcutBlockers");
            Transform mines = Group(root, "MineSlots");
            Transform routes = Group(root, RoutesGroupName);

            Transform playerSpawn = BuildSpawns(layout, heights, spawns);
            BuildObstacles(layout, heights, landmarks, obstacles, blockers, ref placeholders);
            BuildCaveRoofs(layout, heights, obstacles);
            BuildMineSlots(layout, heights, variation, mines);
            BuildRiver(layout, heights, landmarks);
            BuildRouteMarkers(layout, heights, routes);

            // ---- player rig (what Sprint_01 uses: Jackal prefab with its own PilotInputSource, chase camera, volume;
            // the HUD/audio/debug overlay spawn themselves at runtime). F-03c/C-01b will own this once they land.
            VehicleController jackal = EnsurePlayer(scene, playerSpawn);
            if (jackal != null) F03_InputSeatsSetup.ConfigureJackal(jackal.gameObject); // F-03c: crew input, no keyboard on the prefab
            EnsureCamera(scene, jackal);
            EnsureVolume(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                Debug.LogError($"[Paramo] Could not save {scenePath}.");
                return;
            }
            AssetDatabase.SaveAssets();

            int generated = root.GetComponentsInChildren<Transform>(true).Length - 1;
            Debug.Log($"[Paramo] Generated {scenePath}: terrain {heights.Resolution}² over {layout.sizeMetres:0} m " +
                      $"(TerrainData GUID {AssetDatabase.AssetPathToGUID(TerrainDataPath)}), {generated} objects under " +
                      $"{RootName}, {placeholders} placeholder(s), player {(jackal != null ? "ok" : "MISSING")}, " +
                      $"terrain '{terrain.name}', {clock.Elapsed.TotalSeconds:0.0} s.");
        }

        // ================================================================== scene & assets

        private static bool OpenOrCreateScene(string scenePath, out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.path == scenePath && scene.isLoaded) return true;

            // Why: opening a scene in Single mode unloads the current one. Give a human the chance to save it.
            // (Only shows a dialog when something is actually dirty; returns false if they cancel.)
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Paramo] Generation cancelled: unsaved scene changes were not resolved.");
                return false;
            }

            if (File.Exists(TerrainTextureFactory.ToFullPath(scenePath)))
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureFolder(Path.GetDirectoryName(scenePath)?.Replace('\\', '/'));
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            return scene.IsValid();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            // New assets start from code defaults (TerrainLayout's field initializer = ParamoLayoutDefaults);
            // after that the asset is the source of truth and is never overwritten here.
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Creates "Assets/a/b/c" folder by folder (AssetDatabase.CreateFolder makes one level at a time).</summary>
        private static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        private static TerrainData BuildTerrainData(TerrainLayoutData layout, HeightField heights, TerrainLayer[] layers)
        {
            EnsureFolder(TerrainFolder);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            // Order matters: the heightmap resolution setter rescales the terrain, so size is set after it.
            int res = heights.Resolution;
            data.heightmapResolution = res;
            data.size = new Vector3(layout.sizeMetres, layout.heightScale, layout.sizeMetres);
            if (data.heightmapResolution != res)
            {
                Debug.LogError($"[Paramo] Unity rejected heightmapResolution {res} (must be 2^n + 1); got {data.heightmapResolution}.");
                return null;
            }

            // Unity stores heights normalised to [0, 1] of size.y, indexed [z, x]: HeightField is already row-major by z.
            float[] src = heights.Heights;
            float inv = 1f / layout.heightScale;
            var normalised = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    normalised[z, x] = src[z * res + x] * inv;
            data.SetHeights(0, 0, normalised);

            // Layers before alphamaps: SetAlphamaps needs one channel per terrain layer.
            data.terrainLayers = layers;
            int alphaRes = layout.alphamapResolution;
            data.alphamapResolution = alphaRes;
            if (data.alphamapResolution != alphaRes || layers.Length != TerrainTextureFactory.LayerCount)
            {
                Debug.LogError($"[Paramo] Alphamap setup mismatch (res {data.alphamapResolution} vs {alphaRes}, layers {layers.Length}).");
                return null;
            }
            const int channels = 4; // ComputeSplat: sand, basalt, rust, toxic
            var splat = new float[alphaRes * alphaRes * channels];
            TerrainSynthesis.ComputeSplat(heights, layout, alphaRes, splat);
            var alphas = new float[alphaRes, alphaRes, channels];
            for (int z = 0; z < alphaRes; z++)
                for (int x = 0; x < alphaRes; x++)
                    for (int l = 0; l < channels; l++)
                        alphas[z, x, l] = splat[(z * alphaRes + x) * channels + l];
            data.SetAlphamaps(0, 0, alphas);

            EditorUtility.SetDirty(data);
            return data;
        }

        private static Terrain EnsureTerrainObject(Scene scene, TerrainData data, TerrainLayoutData layout)
        {
            GameObject go = FindRoot(scene, TerrainObjectName);
            if (go == null)
            {
                // CreateTerrainGameObject adds Terrain + TerrainCollider already pointing at the same TerrainData.
                go = Terrain.CreateTerrainGameObject(data);
                go.name = TerrainObjectName;
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            var terrain = go.GetComponent<Terrain>();
            if (terrain == null) terrain = go.AddComponent<Terrain>();
            var collider = go.GetComponent<TerrainCollider>();
            if (collider == null) collider = go.AddComponent<TerrainCollider>();
            terrain.terrainData = data;
            collider.terrainData = data; // shares the heights: what you see is what you drive on

            // Terrain origin is its south-west corner; layout coordinates are centred on the Monolith.
            float half = layout.sizeMetres * 0.5f;
            go.transform.SetPositionAndRotation(new Vector3(-half, 0f, -half), Quaternion.identity);
            go.transform.localScale = Vector3.one;

            terrain.drawInstanced = true;           // GPU instancing: far fewer draw calls for a big terrain
            terrain.heightmapPixelError = HeightmapPixelError;
            terrain.basemapDistance = BasemapDistance;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            terrain.drawTreesAndFoliage = false;    // no trees/details in TR-01
            // materialTemplate left empty on purpose: URP then uses its default Terrain/Lit material.
            return terrain;
        }

        private static void EnsureSun(Scene scene)
        {
            foreach (GameObject r in scene.GetRootGameObjects())
                foreach (Light l in r.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Directional) return;

            var go = new GameObject(SunName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(SunEuler);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColour;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
        }

        private static void ApplyFog(WorldVariationConfig variation)
        {
            // RenderSettings belong to the active scene (the one just opened). TR-01c's ParamoAtmosphere re-applies
            // clear or storm fog at runtime; this is the edit-time / clear-weather baseline.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = variation.clearFogColor;
            RenderSettings.fogDensity = variation.clearFogDensity;
        }

        private static GameObject RebuildRoot(Scene scene)
        {
            GameObject root = FindRoot(scene, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            // Idempotency contract: only this root's children are deleted and recreated.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return root;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        private static Transform Group(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.transform;
        }

        // ================================================================== generated content

        private static Transform BuildSpawns(TerrainLayoutData layout, HeightField h, Transform parent)
        {
            Transform player = null;
            if (layout.spawns == null) return null;
            foreach (SpawnDef s in layout.spawns)
            {
                var go = new GameObject(SpawnName(s));
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(Ground(h, s.position), Quaternion.Euler(0f, s.yawDeg, 0f));
                if (!s.rival && player == null) player = go.transform;
            }
            return player;
        }

        /// <summary>"player" → Spawn_Player, "rival_w" → Spawn_Rival_W (names TR-01c looks up).</summary>
        private static string SpawnName(SpawnDef s)
        {
            string id = s.id ?? string.Empty;
            if (s.rival)
            {
                int cut = id.IndexOf('_');
                string suffix = cut >= 0 ? id.Substring(cut + 1) : id;
                return "Spawn_Rival_" + suffix.ToUpperInvariant();
            }
            return "Spawn_" + (id.Length > 0 ? char.ToUpperInvariant(id[0]) + id.Substring(1) : "Player");
        }

        private static void BuildObstacles(TerrainLayoutData layout, HeightField h, Transform landmarks, Transform parent,
            Transform blockers, ref int placeholders)
        {
            if (layout.obstacles == null) return;
            var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RammerGatePrefabPath);
            var mudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MudPatchPrefabPath);
            bool warnedGate = false, warnedMud = false;

            for (int i = 0; i < layout.obstacles.Length; i++)
            {
                ObstaclePlacement p = layout.obstacles[i];
                Quaternion yaw = Quaternion.Euler(0f, p.yawDeg, 0f);
                switch (p.kind)
                {
                    case PlacementKind.Monolith:
                        BuildMonolith(p, h, landmarks);
                        break;

                    case PlacementKind.RammerGate:
                        if (gatePrefab != null) PlacePrefab(gatePrefab, $"RammerGate_{p.shortcutId}", p, h, parent);
                        else
                        {
                            placeholders++;
                            if (!warnedGate) Debug.LogWarning($"[Paramo] {RammerGatePrefabPath} missing: placeholder marker used. Re-run after LV-01c.");
                            warnedGate = true;
                            GameObject marker = Box($"RammerGate_Placeholder_{p.shortcutId}", parent, GateGlowMat, false);
                            Vector3 size = new Vector3(Mathf.Max(p.scale.x, 1f), PlaceholderPoleHeight, Mathf.Max(p.scale.z, 1f));
                            marker.transform.SetPositionAndRotation(Ground(h, p.position) + Vector3.up * size.y * 0.5f, yaw);
                            marker.transform.localScale = size;
                        }
                        break;

                    case PlacementKind.MudPatch:
                        if (mudPrefab != null) PlacePrefab(mudPrefab, $"MudPatch_{i}", p, h, parent);
                        else
                        {
                            placeholders++;
                            if (!warnedMud) Debug.LogWarning($"[Paramo] {MudPatchPrefabPath} missing: placeholder marker used. Re-run after LV-02c.");
                            warnedMud = true;
                            // No collider: a thin box would be a bump; the real MudPatch is a ground-tag surface (D-027).
                            GameObject marker = Box($"MudPatch_Placeholder_{i}", parent, DirtMat, false);
                            marker.transform.SetPositionAndRotation(Ground(h, p.position), yaw);
                            marker.transform.localScale = new Vector3(p.scale.x, MudPlaceholderThickness, p.scale.z);
                        }
                        break;

                    case PlacementKind.ChasmRamp:
                        BuildRamp(p, h, parent);
                        break;

                    case PlacementKind.CaveRoof:
                    {
                        GameObject slab = Box($"CaveRoof_{i}", parent, BasaltMat, true);
                        float y = Ground(h, p.position).y + CaveClearance + p.scale.y * 0.5f;
                        slab.transform.SetPositionAndRotation(new Vector3(p.position.x, y, p.position.y), yaw);
                        slab.transform.localScale = p.scale;
                        MarkStatic(slab);
                        break;
                    }

                    case PlacementKind.ShortcutBlocker:
                        BuildBlocker(p, h, blockers, layout.seed, i);
                        break;

                    case PlacementKind.Rock:
                    {
                        GameObject rock = Box($"Rock_{i}", parent, RockMat, true);
                        Vector3 pos = Ground(h, p.position) + Vector3.up * p.scale.y * (0.5f - RockBury);
                        rock.transform.SetPositionAndRotation(pos, yaw);
                        rock.transform.localScale = p.scale;
                        MarkStatic(rock);
                        break;
                    }
                }
            }
        }

        private static void BuildMonolith(ObstaclePlacement p, HeightField h, Transform parent)
        {
            Vector3 ground = Ground(h, p.position);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MonolithModelPath);
            GameObject monolith;
            if (model != null)
            {
                // Same setup as Sprint_01: the FBX at its authored scale, glow material on slot 1, mesh collider.
                monolith = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                monolith.name = "Monolith";
                var glow = AssetDatabase.LoadAssetAtPath<Material>(MonolithGlowMat);
                foreach (MeshRenderer r in monolith.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    if (glow != null && mats.Length > 1)
                    {
                        mats[1] = glow;
                        r.sharedMaterials = mats;
                    }
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter != null && r.GetComponent<Collider>() == null)
                        r.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                }
                monolith.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, p.yawDeg, 0f));
                monolith.transform.localScale *= MonolithLandmarkScale;
            }
            else
            {
                // Spec fallback: a basalt box of the placement size.
                monolith = Box("Monolith", parent, BasaltMat, true);
                monolith.transform.SetPositionAndRotation(ground + Vector3.up * p.scale.y * 0.5f, Quaternion.Euler(0f, p.yawDeg, 0f));
                monolith.transform.localScale = p.scale;
            }
            MarkStatic(monolith);

            // Beam: a separate object (not a child) so the model's scale never stretches it. No collider: purely visual.
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Monolith_Beam";
            beam.transform.SetParent(parent, false);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            SetMaterial(beam, MonolithGlowMat);
            // Unity's cylinder primitive is 2 units tall, hence height * 0.5 for the Y scale.
            beam.transform.SetPositionAndRotation(ground + Vector3.up * BeamHeight * 0.5f, Quaternion.identity);
            beam.transform.localScale = new Vector3(BeamDiameter, BeamHeight * 0.5f, BeamDiameter);
            beam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            MarkStatic(beam);

            var lightGo = new GameObject("MonolithGlow");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = ground + MonolithLightOffset * MonolithLandmarkScale;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = MonolithLightColour;
            light.intensity = MonolithLightIntensity;
            light.range = MonolithLightRange * MonolithLandmarkScale;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Take-off wedge. Placement: position = ramp centre, yaw = jump direction, scale = (width, rise, run).
        /// Built as a pitched slab whose top surface rises from the road at the start to <c>rise</c> at the lip.
        /// </summary>
        private static void BuildRamp(ObstaclePlacement p, HeightField h, Transform parent)
        {
            float run = Mathf.Max(p.scale.z, 0.01f), rise = p.scale.y;
            float yawRad = p.yawDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(yawRad), Mathf.Cos(yawRad));
            float baseY = Ground(h, p.position - dir * (run * 0.5f)).y - RampSink;
            float pitchDeg = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            // Negative X rotation tilts local +Z (travel) upward.
            Quaternion rotation = Quaternion.Euler(-pitchDeg, p.yawDeg, 0f);
            Vector3 topCentre = new Vector3(p.position.x, baseY + rise * 0.5f, p.position.y);
            Vector3 centre = topCentre - rotation * Vector3.up * (RampThickness * 0.5f);

            GameObject ramp = Box($"ChasmRamp_{p.shortcutId}", parent, SteelMat, true);
            ramp.transform.SetPositionAndRotation(centre, rotation);
            ramp.transform.localScale = new Vector3(p.scale.x, RampThickness, Mathf.Sqrt(run * run + rise * rise));
            MarkStatic(ramp);
        }

        /// <summary>Rockfall that closes a shortcut. Inactive here; TR-01c's WorldVariationRunner activates it per run.</summary>
        private static void BuildBlocker(ObstaclePlacement p, HeightField h, Transform parent, int seed, int index)
        {
            var go = new GameObject($"Blocker_{p.shortcutId}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Ground(h, p.position), Quaternion.Euler(0f, p.yawDeg, 0f));

            // Seeded so a re-run produces the exact same boulders (idempotency) without storing them in data.
            var rng = new DeterministicRng(seed * 7919 + index * 104729 + 1);
            for (int b = 0; b < BlockerBoxes; b++)
            {
                GameObject box = Box($"Boulder_{b}", go.transform, RockMat, true);
                var size = new Vector3(
                    p.scale.x * Mathf.Lerp(0.25f, 0.4f, rng.NextFloat01()),
                    p.scale.y * Mathf.Lerp(0.6f, 1f, rng.NextFloat01()),
                    p.scale.z * Mathf.Lerp(0.5f, 0.9f, rng.NextFloat01()));
                // Spread across the road (local X) so the whole width is blocked; slight overlap between boulders.
                float lateral = ((b + 0.5f) / BlockerBoxes - 0.5f) * p.scale.x + (rng.NextFloat01() - 0.5f) * 1.5f;
                float along = (rng.NextFloat01() - 0.5f) * p.scale.z * 0.4f;
                Vector2 local = new Vector2(lateral, along);
                Vector3 world = go.transform.TransformPoint(new Vector3(local.x, 0f, local.y));
                float y = h.Sample(world.x, world.z) + size.y * (0.5f - RockBury);
                box.transform.position = new Vector3(world.x, y, world.z);
                box.transform.localRotation = Quaternion.Euler(0f, (rng.NextFloat01() - 0.5f) * 50f, 0f);
                box.transform.localScale = size;
                MarkStatic(box);
            }
            go.SetActive(false);
        }

        /// <summary>Cave roof slabs over every route's <c>coveredRange</c> (heightmaps cannot overhang).</summary>
        private static void BuildCaveRoofs(TerrainLayoutData layout, HeightField h, Transform parent)
        {
            if (layout.routes == null) return;
            var points = new List<Vector3>(1024);
            float spacing = Mathf.Max(layout.routeSampleSpacing, 0.1f);
            foreach (RouteDef route in layout.routes)
            {
                Vector2 range = route.coveredRange;
                if (range.y <= range.x) continue;
                int count = TerrainSynthesis.SampleRoute(h, route, spacing, points);
                if (count < 2) continue;

                int slabs = Mathf.Max(1, Mathf.CeilToInt((range.y - range.x) / CaveSlabLength));
                float slabLen = (range.y - range.x) / slabs;
                for (int s = 0; s < slabs; s++)
                {
                    float s0 = range.x + s * slabLen, s1 = s0 + slabLen;
                    int i0 = Mathf.Clamp(Mathf.FloorToInt(s0 / spacing), 0, count - 1);
                    int i1 = Mathf.Clamp(Mathf.CeilToInt(s1 / spacing), 0, count - 1);
                    if (i1 <= i0) continue;
                    float bedMax = float.MinValue;
                    for (int k = i0; k <= i1; k++) bedMax = Mathf.Max(bedMax, points[k].y);
                    Vector3 a = points[i0], b = points[i1];
                    Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                    if (flat.sqrMagnitude < 1e-6f) continue;

                    GameObject slab = Box($"CaveRoof_{route.id}_{s}", parent, BasaltMat, true);
                    Vector3 mid = (a + b) * 0.5f;
                    slab.transform.SetPositionAndRotation(
                        new Vector3(mid.x, bedMax + CaveClearance + CaveSlabThickness * 0.5f, mid.z),
                        Quaternion.LookRotation(flat.normalized, Vector3.up));
                    // Spans road + shoulders so it rests over the trench edges rather than floating over the lane.
                    slab.transform.localScale = new Vector3(route.width + 2f * route.shoulder, CaveSlabThickness,
                        flat.magnitude + CaveSlabOverlap);
                    MarkStatic(slab);
                }
            }
        }

        /// <summary>Every possible mine position (WorldVariationRules.MineSlots); all inactive and inert until TR-01c / a mine task.</summary>
        private static void BuildMineSlots(TerrainLayoutData layout, HeightField h, WorldVariationConfig variation, Transform parent)
        {
            var slots = new List<Vector2>(variation.mineSlotCount);
            WorldVariationRules.MineSlots(layout.seed, variation, slots);
            for (int i = 0; i < slots.Count; i++)
            {
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = $"MineSlot_{i:00}";
                disc.transform.SetParent(parent, false);
                Object.DestroyImmediate(disc.GetComponent<Collider>()); // mines are inert markers in TR-01
                disc.GetComponent<MeshRenderer>().sharedMaterial =
                    EnsureEmissiveMaterial(MineMaterialPath, MineColour, MineEmission);
                disc.transform.position = Ground(h, slots[i]) + Vector3.up * (MineDiscHeight * 0.5f);
                disc.transform.localScale = new Vector3(MineDiscRadius * 2f, MineDiscHeight * 0.5f, MineDiscRadius * 2f);
                MarkStatic(disc);
                disc.SetActive(false);
            }
        }

        /// <summary>Toxic river surface: a flat ribbon mesh saved as an asset (updated in place), no collider.</summary>
        private static void BuildRiver(TerrainLayoutData layout, HeightField h, Transform parent)
        {
            RiverDef river = layout.river;
            if (river.nodes == null || river.nodes.Length < 2 || river.width <= 0f) return;

            // Resample the polyline at a fixed step.
            var centres = new List<Vector2>(512);
            for (int n = 0; n < river.nodes.Length - 1; n++)
            {
                Vector2 a = river.nodes[n], b = river.nodes[n + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / RiverRibbonStep));
                for (int s = 0; s < steps; s++) centres.Add(Vector2.Lerp(a, b, (float)s / steps));
            }
            centres.Add(river.nodes[river.nodes.Length - 1]);
            int count = centres.Count;
            var bed = new float[count];
            for (int i = 0; i < count; i++) bed[i] = h.Sample(centres[i].x, centres[i].y);

            // Ribbon reaches part way up the bank shoulders so its edges tuck into the slope instead of floating.
            float halfWidth = river.width * 0.5f + river.shoulder * Mathf.Clamp01(RiverFill / Mathf.Max(river.depth, 0.01f));
            var vertices = new Vector3[count * 2];
            var uvs = new Vector2[count * 2];
            var triangles = new int[(count - 1) * 6];
            float along = 0f;
            for (int i = 0; i < count; i++)
            {
                // Lowest bed nearby: road fords raise the centreline locally, the liquid surface should not follow.
                float y = float.MaxValue;
                for (int k = Mathf.Max(0, i - RiverSurfaceWindow); k <= Mathf.Min(count - 1, i + RiverSurfaceWindow); k++)
                    y = Mathf.Min(y, bed[k]);
                y += RiverFill;

                Vector2 prev = centres[Mathf.Max(0, i - 1)], next = centres[Mathf.Min(count - 1, i + 1)];
                Vector2 t = (next - prev).normalized;
                var side = new Vector2(t.y, -t.x); // right of travel
                Vector2 c = centres[i];
                if (i > 0) along += Vector2.Distance(centres[i - 1], c);
                vertices[i * 2] = new Vector3(c.x - side.x * halfWidth, y, c.y - side.y * halfWidth);
                vertices[i * 2 + 1] = new Vector3(c.x + side.x * halfWidth, y, c.y + side.y * halfWidth);
                uvs[i * 2] = new Vector2(0f, along / river.width);
                uvs[i * 2 + 1] = new Vector2(1f, along / river.width);
            }
            for (int i = 0; i < count - 1; i++)
            {
                int v = i * 2, o = i * 6;
                // Winding chosen so the face points up (clockwise seen from above in Unity's left-handed space).
                triangles[o] = v; triangles[o + 1] = v + 2; triangles[o + 2] = v + 1;
                triangles[o + 3] = v + 1; triangles[o + 4] = v + 2; triangles[o + 5] = v + 3;
            }

            // Update the mesh asset in place so references (and the GUID) survive re-runs.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RiverMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Paramo_01_River" };
                AssetDatabase.CreateAsset(mesh, RiverMeshPath);
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var go = new GameObject("ToxicRiver");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = EnsureEmissiveMaterial(ToxicMaterialPath, ToxicColour, ToxicEmission);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            MarkStatic(go);
        }

        /// <summary>
        /// One object per route with a child per polyline node (on the ground). Drawn as scene-view lines by
        /// <see cref="ParamoRouteGizmos"/>; no runtime cost. Rival lines are what TR-01c turns into AI RacingLines.
        /// </summary>
        private static void BuildRouteMarkers(TerrainLayoutData layout, HeightField h, Transform parent)
        {
            if (layout.routes == null) return;
            foreach (RouteDef route in layout.routes)
            {
                if (route.nodes == null || route.nodes.Length < 2) continue;
                var go = new GameObject($"Route_{route.kind}_{route.id}");
                go.transform.SetParent(parent, false);
                for (int n = 0; n < route.nodes.Length; n++)
                {
                    var node = new GameObject($"P{n}");
                    node.transform.SetParent(go.transform, false);
                    node.transform.position = Ground(h, route.nodes[n]);
                }
            }
        }

        // ================================================================== player rig

        private static VehicleController EnsurePlayer(Scene scene, Transform spawn)
        {
            GameObject go = FindRoot(scene, PlayerName);
            if (go == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JackalPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[Paramo] {JackalPrefabPath} not found: scene has no player vehicle.");
                    return null;
                }
                // InstantiatePrefab (not Object.Instantiate) keeps the prefab link, so Jackal fixes flow into this scene.
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.name = PlayerName;
            }
            if (spawn != null)
                go.transform.SetPositionAndRotation(spawn.position + Vector3.up * SpawnDropHeight, spawn.rotation);
            return go.GetComponent<VehicleController>();
        }

        private static void EnsureCamera(Scene scene, VehicleController target)
        {
            Camera camera = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (!r.CompareTag("MainCamera")) continue;
                camera = r.GetComponent<Camera>();
                if (camera != null) break;
            }
            if (camera == null)
            {
                var go = new GameObject(CameraName) { tag = "MainCamera" };
                SceneManager.MoveGameObjectToScene(go, scene);
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            camera.farClipPlane = CameraFarClip;

            // Same chase camera as Sprint_01. Its fields are [SerializeField] private, so they are set through
            // SerializedObject (the Inspector's own path) instead of reflection or making them public.
            var chase = camera.GetComponent<SandboxChaseCamera>();
            if (chase == null) chase = camera.gameObject.AddComponent<SandboxChaseCamera>();
            var so = new SerializedObject(chase);
            so.FindProperty("_target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (target != null)
            {
                // Start roughly where the chase camera will settle so the first frame is not a long swoop.
                Transform t = target.transform;
                camera.transform.position = t.position - t.forward * CameraStartBack + Vector3.up * CameraStartUp;
                camera.transform.rotation = Quaternion.LookRotation(t.position + Vector3.up - camera.transform.position, Vector3.up);
            }
        }

        private static void EnsureVolume(Scene scene)
        {
            if (FindRoot(scene, VolumeName) != null) return;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null) return; // optional look; the scene plays fine without post-processing
            var go = new GameObject(VolumeName);
            SceneManager.MoveGameObjectToScene(go, scene);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ================================================================== helpers

        private static Vector3 Ground(HeightField h, Vector2 p) => new Vector3(p.x, h.Sample(p.x, p.y), p.y);

        private static GameObject Box(string name, Transform parent, string materialPath, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            SetMaterial(go, materialPath);
            return go;
        }

        private static void SetMaterial(GameObject go, string materialPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void PlacePrefab(GameObject prefab, string name, ObstaclePlacement p, HeightField h, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(Ground(h, p.position), Quaternion.Euler(0f, p.yawDeg, 0f));
            // Placement scale is the object's size in metres; prefabs are expected to be authored at 1 m units.
            go.transform.localScale = p.scale;
        }

        /// <summary>
        /// Static batching: fewer draw calls for the many greybox boxes. Toggling SetActive on static objects
        /// (blockers, mines) still works at runtime.
        /// </summary>
        private static void MarkStatic(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Creates (once) or refreshes a URP Lit emissive material under Terrain/Materials. GUID stays stable.</summary>
        private static Material EnsureEmissiveMaterial(string path, Color baseColour, Color emission)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.GetColor("_BaseColor") == baseColour && mat.GetColor("_EmissionColor") == emission)
            {
                return mat; // unchanged: avoid dirtying the asset on every run
            }
            mat.SetColor("_BaseColor", baseColour);
            mat.SetColor("_Color", baseColour); // fallback name for non-URP shaders
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            // Why: without this flag Unity treats the emission as baked-only and it would not glow in realtime.
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }

    /// <summary>
    /// Scene-view lines for the routes generated under <c>Paramo_Generated/Routes</c> (rival = red, main = cyan,
    /// shortcut = yellow). Editor-only: lives in Convoy.Editor so no runtime component or per-frame cost ships.
    /// Lines follow the terrain between nodes; corner rounding of the carved road is not drawn.
    /// </summary>
    [InitializeOnLoad]
    internal static class ParamoRouteGizmos
    {
        private const float StepMetres = 10f;  // terrain-follow resolution of the drawn line
        private const float Lift = 1.5f;       // drawn above the ground so the line is not hidden by the road
        private const float LineWidth = 4f;    // pixels

        private static readonly Color RivalColour = new Color(1f, 0.3f, 0.2f);
        private static readonly Color MainColour = new Color(0.3f, 0.9f, 1f);
        private static readonly Color ShortcutColour = new Color(1f, 0.85f, 0.2f);

        private static Vector3[] _buffer = new Vector3[512];

        static ParamoRouteGizmos()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;
            GameObject routes = GameObject.Find(ParamoTerrainGenerator.RootName + "/" + ParamoTerrainGenerator.RoutesGroupName);
            if (routes == null) return;
            Terrain terrain = Terrain.activeTerrain;

            foreach (Transform route in routes.transform)
            {
                if (route.childCount < 2) continue;
                string n = route.name;
                Handles.color = n.StartsWith("Route_Rival_") ? RivalColour
                    : n.StartsWith("Route_Shortcut_") ? ShortcutColour : MainColour;

                int count = 0;
                for (int i = 0; i < route.childCount - 1; i++)
                {
                    Vector3 a = route.GetChild(i).position, b = route.GetChild(i + 1).position;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / StepMetres));
                    for (int s = 0; s < steps; s++) Add(ref count, Follow(terrain, Vector3.Lerp(a, b, (float)s / steps)));
                }
                Add(ref count, Follow(terrain, route.GetChild(route.childCount - 1).position));
                Handles.DrawAAPolyLine(LineWidth, count, _buffer);
                Handles.Label(_buffer[0] + Vector3.up * 3f, n);
            }
        }

        private static Vector3 Follow(Terrain terrain, Vector3 p)
        {
            if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            p.y += Lift;
            return p;
        }

        private static void Add(ref int count, Vector3 p)
        {
            if (count == _buffer.Length) System.Array.Resize(ref _buffer, _buffer.Length * 2);
            _buffer[count++] = p;
        }
    }
}
