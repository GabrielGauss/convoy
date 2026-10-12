using System.Collections.Generic;
using System.IO;
using Convoy.AI;
using Convoy.Vehicle;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Convoy.Editor
{
    /// <summary>
    /// Shared editor helpers to put AI rivals into a scene (TR-01c now, A-01c's Sprint_01 script later): the three
    /// driver profiles, a <see cref="RacingLine"/> built from waypoints, and a Jackal with an <see cref="AiPilot"/>.
    /// Everything is find-or-create by name, so re-running a setup updates in place instead of duplicating.
    /// Why SerializedObject: <see cref="AiPilot"/>/<see cref="RacingLine"/> keep their wiring in
    /// <c>[SerializeField] private</c> fields; setting them through SerializedObject is the Inspector's own path and is
    /// what gets saved with the scene (Configure() at edit time would not be serialized).
    /// </summary>
    public static class AiRivalSetup
    {
        public const string ProfileFolder = "Assets/_Convoy/Config/AI";
        public const string SteadyPath = ProfileFolder + "/AI_Steady.asset";
        public const string BalancedPath = ProfileFolder + "/AI_Balanced.asset";
        public const string AggressivePath = ProfileFolder + "/AI_Aggressive.asset";
        public const string JackalPrefabPath = "Assets/_Convoy/Prefabs/Jackal.prefab";

        // A-01 spec data table: only cornerSpeedScale differs between the three personalities.
        private const float SteadyCornerScale = 0.85f, BalancedCornerScale = 0.92f, AggressiveCornerScale = 0.98f;

        // Rivals spawn this far above the ground and settle on their suspension (same as the player in Sprint_01/Paramo).
        public const float SpawnDropHeight = 1.2f;

        private const string WaypointPrefix = "WP_";

        /// <summary>Steady, Balanced, Aggressive (A-01 spec). Created when missing; existing assets are never changed.</summary>
        public static AiDriverProfile[] EnsureProfiles()
        {
            return new[]
            {
                EnsureProfile(SteadyPath, SteadyCornerScale),
                EnsureProfile(BalancedPath, BalancedCornerScale),
                EnsureProfile(AggressivePath, AggressiveCornerScale),
            };
        }

        private static AiDriverProfile EnsureProfile(string path, float cornerSpeedScale)
        {
            var profile = AssetDatabase.LoadAssetAtPath<AiDriverProfile>(path);
            if (profile != null) return profile;
            EnsureFolder(ProfileFolder);
            profile = ScriptableObject.CreateInstance<AiDriverProfile>();
            profile.cornerSpeedScale = cornerSpeedScale;
            AssetDatabase.CreateAsset(profile, path);
            Debug.Log($"[AI] created {path} (cornerSpeedScale {cornerSpeedScale}).");
            return profile;
        }

        /// <summary>
        /// Finds or creates <paramref name="name"/> under <paramref name="parent"/> with a <see cref="RacingLine"/> whose
        /// children are exactly <paramref name="waypoints"/> (old waypoint children are replaced: they are generated).
        /// Open line (routes end at the Monolith); <paramref name="halfWidth"/> bounds the AI's lane offsets.
        /// </summary>
        public static RacingLine EnsureRacingLine(Transform parent, string name, IReadOnlyList<Vector3> waypoints, float halfWidth)
        {
            Transform t = parent.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            if (t == null) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            for (int i = 0; i < waypoints.Count; i++)
            {
                var wp = new GameObject($"{WaypointPrefix}{i:000}");
                wp.transform.SetParent(go.transform, false);
                wp.transform.position = waypoints[i];
            }

            var line = go.GetComponent<RacingLine>();
            if (line == null) line = go.AddComponent<RacingLine>();
            var so = new SerializedObject(line);
            so.FindProperty("_closed").boolValue = false;
            so.FindProperty("_halfWidth").floatValue = halfWidth;
            so.ApplyModifiedPropertiesWithoutUndo();
            return line;
        }

        /// <summary>
        /// Finds or creates a Jackal named <paramref name="name"/> under <paramref name="parent"/> at
        /// <paramref name="spawn"/> (+<see cref="SpawnDropHeight"/>), with an <see cref="AiPilot"/> driving
        /// <paramref name="line"/>. <paramref name="waitFor"/> = the player's car: the rival holds until it moves
        /// (A-01 temporary start until R-01's countdown). Obstacle casts hit the Default layer (A-01 spec).
        /// Returns null (logged) when the Jackal prefab is missing.
        /// </summary>
        public static AiPilot EnsureRival(Transform parent, string name, Pose spawn, RacingLine line, AiDriverProfile profile,
            VehicleController waitFor)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t != null)
            {
                go = t.gameObject;
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JackalPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[AI] {JackalPrefabPath} not found: rival {name} not created.");
                    return null;
                }
                // InstantiatePrefab keeps the prefab link, so Jackal tuning flows into the rivals too.
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = name;
            }
            go.transform.SetPositionAndRotation(spawn.position + Vector3.up * SpawnDropHeight, spawn.rotation);

            if (go.GetComponent<VehicleController>() == null)
            {
                Debug.LogError($"[AI] {name} has no VehicleController; AiPilot not added.", go);
                return null;
            }
            var pilot = go.GetComponent<AiPilot>();
            if (pilot == null) pilot = go.AddComponent<AiPilot>();
            var so = new SerializedObject(pilot);
            so.FindProperty("_line").objectReferenceValue = line;
            so.FindProperty("_profile").objectReferenceValue = profile;
            so.FindProperty("_obstacleMask").intValue = 1 << LayerMask.NameToLayer("Default");
            so.FindProperty("_waitForVehicle").objectReferenceValue = waitFor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pilot;
        }

        /// <summary>Spawn pose facing along the first leg of the line (the authored spawn yaw may not match the road).</summary>
        public static Pose StartPose(IReadOnlyList<Vector3> waypoints)
        {
            Vector3 start = waypoints[0];
            Vector3 ahead = waypoints[Mathf.Min(1, waypoints.Count - 1)] - start;
            ahead.y = 0f;
            Quaternion facing = ahead.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(ahead.normalized, Vector3.up) : Quaternion.identity;
            return new Pose(start, facing);
        }

        /// <summary>Finds a scene root by name or creates it (in <paramref name="scene"/>).</summary>
        public static Transform EnsureRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        /// <summary>Finds or creates a child group under <paramref name="parent"/>.</summary>
        public static Transform EnsureGroup(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Creates "Assets/a/b/c" folder by folder (AssetDatabase.CreateFolder makes one level at a time).</summary>
        public static void EnsureFolder(string assetFolder)
        {
            assetFolder = assetFolder?.Replace('\\', '/');
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }
}
