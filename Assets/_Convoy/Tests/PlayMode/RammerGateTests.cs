// Loads the Jackal prefab and wires private serialized fields through AssetDatabase/SerializedObject, which only
// exist in the Editor. The whole file is Editor-only so a player build of the test assembly still compiles.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Convoy.Core;
using Convoy.Input;
using Convoy.Level;
using Convoy.Vehicle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
// Unity's Is extends NUnit's Is with Is.Not.AllocatingGCMemory(); all the usual NUnit constraints still work.
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Convoy.Tests.PlayMode
{
    /// <summary>
    /// LV-01b, spec LV-01 AC 3 (a–d) and AC 5. Builds a gate from code (no prefab: LV-01c creates that) in front of
    /// the real Jackal, gives the car an initial velocity and watches what physics does.
    /// </summary>
    [Category("Slow")]
    public class RammerGateTests
    {
        private const string JackalPrefabPath = "Assets/_Convoy/Prefabs/Jackal.prefab";

        // Gate geometry from the spec's LV-01c generator: 8 × 5 × 1.5 m, centred 2.5 m up, 4 × 3 = 12 chunks.
        private static readonly Vector3 WallSize = new Vector3(8f, 5f, 1.5f);
        private const float WallCentreY = 2.5f;
        private const int Columns = 4;
        private const int Rows = 3;
        private const int LayoutSeed = 1;
        private const float WallZ = 0f;                       // gate root; forward = +Z
        private const float FrontFaceZ = WallZ - 0.75f;       // half thickness
        private const float BackFaceZ = WallZ + 0.75f;

        private const float StartDistance = 10f;              // from the front face, along the approach
        private const float GroundSize = 400f;
        private const float SpawnHeight = 0.5f;
        private const float SettleSeconds = 1f;               // let the suspension settle before launching the car

        private const float BreakSpeedMps = 25f;              // 90 km/h, above the 80 km/h default
        private const float SlowSpeedMps = 15f;               // 54 km/h, below it
        private const float GlancingAngleDeg = 60f;           // 25 m/s at 60° -> closing 12.5 m/s
        private const float Retention = 0.85f;                // spec AC 3a, also the config default
        private const float RetentionTolerance = 0.05f;       // ± 5 % (one physics step of rolling drag fits easily)
        private const float BreakWithinSeconds = 1f;
        private const float PassWithinSeconds = 2f;
        private const float PassBeyondMeters = 5f;
        private const float WatchSeconds = 2f;                // how long the "does not break" cases are observed
        // Test-only: freeze debris after 1.5 s instead of the 6 s default so the suite stays fast; the timer logic is
        // identical for any value.
        private const float TestSettleSeconds = 1.5f;

        private readonly List<Object> _spawned = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
            // Destroy is deferred to the end of the frame; wait so the next test starts on an empty scene.
            yield return null;
        }

        // AC 3a + 3d.
        [UnityTest]
        public IEnumerator HeadOn25Mps_Breaks_KeepsSpeed_PassesThrough_ThenDebrisFreezes()
        {
            RammerGate gate = BuildGate(out Rigidbody[] chunks);
            Rigidbody car = null;
            yield return SpawnCar(0f, c => car = c);

            bool brokeEvent = false;
            float postBreakSpeed = float.NaN;
            // The event fires right after the retention write, so the car's velocity here is the retained one.
            gate.Broken += g => { brokeEvent = true; postBreakSpeed = Horizontal(car.linearVelocity).magnitude; };

            Launch(car, 0f, BreakSpeedMps);
            float dt = Time.fixedDeltaTime;
            float lastSpeed = BreakSpeedMps;
            float preBreakSpeed = float.NaN;
            int breakStep = -1;
            int passStep = -1;
            int steps = Mathf.RoundToInt(PassWithinSeconds / dt);
            for (int step = 1; step <= steps; step++)
            {
                yield return new WaitForFixedUpdate();
                // WaitForFixedUpdate resumes after the physics step AND its trigger callbacks, so the first step where
                // IsBroken is true already contains the retention write; the previous step's speed is the pre-break one.
                if (breakStep < 0 && gate.IsBroken) { breakStep = step; preBreakSpeed = lastSpeed; }
                if (passStep < 0 && car.position.z > BackFaceZ + PassBeyondMeters) passStep = step;
                lastSpeed = Horizontal(car.linearVelocity).magnitude;
            }

            Assert.That(breakStep, Is.GreaterThan(0), "gate did not break at 25 m/s head-on");
            Assert.That(brokeEvent, Is.True, "Broken event not raised");
            Assert.That(breakStep * dt, Is.LessThanOrEqualTo(BreakWithinSeconds));
            Assert.That(passStep, Is.GreaterThan(0), "car did not pass 5 m beyond the wall within 2 s");
            Assert.That(postBreakSpeed / preBreakSpeed,
                Is.EqualTo(Retention).Within(RetentionTolerance), $"pre {preBreakSpeed:0.00} m/s, post {postBreakSpeed:0.00} m/s");

            // AC 3d: after the settle time every chunk is frozen and the gate stops ticking.
            float wait = TestSettleSeconds + 2f * dt;
            int waitSteps = Mathf.CeilToInt(wait / dt);
            for (int i = 0; i < waitSteps; i++) yield return new WaitForFixedUpdate();

            for (int i = 0; i < chunks.Length; i++)
            {
                Assert.That(chunks[i].isKinematic, Is.True, $"chunk {i} not kinematic after settle");
                Assert.That(chunks[i].GetComponent<Collider>().enabled, Is.False, $"chunk {i} collider still on");
            }
            Assert.That(gate.enabled, Is.False, "gate should disable itself after the debris settles (AC 5)");
        }

        // AC 3b.
        [UnityTest]
        public IEnumerator HeadOn15Mps_DoesNotBreak_CarStopsAtWall()
        {
            RammerGate gate = BuildGate(out _);
            Rigidbody car = null;
            yield return SpawnCar(0f, c => car = c);

            Launch(car, 0f, SlowSpeedMps);
            int steps = Mathf.RoundToInt(WatchSeconds / Time.fixedDeltaTime);
            for (int step = 0; step < steps; step++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(gate.IsBroken, Is.False, "gate broke at 15 m/s");
                Assert.That(car.position.z, Is.LessThan(BackFaceZ), "car passed through an intact wall");
            }
        }

        // AC 3c.
        [UnityTest]
        public IEnumerator Glancing25MpsAt60Degrees_DoesNotBreak()
        {
            RammerGate gate = BuildGate(out _);
            Rigidbody car = null;
            yield return SpawnCar(GlancingAngleDeg, c => car = c);

            Launch(car, GlancingAngleDeg, BreakSpeedMps);
            int steps = Mathf.RoundToInt(WatchSeconds / Time.fixedDeltaTime);
            for (int step = 0; step < steps; step++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(gate.IsBroken, Is.False, "gate broke on a 60 degree glancing hit (closing 12.5 m/s)");
            }
        }

        // AC 5: the per-step trigger path must not allocate. Measured on the real callback (found by reflection,
        // because Unity calls it by name and it is private) with a slow heavy body inside the sensor.
        [UnityTest]
        public IEnumerator OnTriggerStay_BelowThreshold_AllocatesNothing()
        {
            RammerGate gate = BuildGate(out _);
            yield return null; // Awake ran in BuildGate; one frame keeps the setup identical to the other tests

            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(probe);
            probe.transform.position = new Vector3(0f, WallCentreY, FrontFaceZ - 1f);
            var body = probe.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1200f;
            body.linearVelocity = new Vector3(0f, 0f, SlowSpeedMps);
            Collider probeCollider = probe.GetComponent<Collider>();

            MethodInfo method = typeof(RammerGate).GetMethod("OnTriggerStay", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "RammerGate.OnTriggerStay not found");
            var stay = (Action<Collider>)Delegate.CreateDelegate(typeof(Action<Collider>), gate, method);
            stay(probeCollider); // warm-up: first call may JIT

            Assert.That(() => stay(probeCollider), Is.Not.AllocatingGCMemory());
            Assert.That(gate.IsBroken, Is.False);
        }

        // ---------- helpers ----------

        private RammerGate BuildGate(out Rigidbody[] chunks)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "RammerTestGround";
            ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);
            ground.transform.position = new Vector3(0f, -0.5f, 0f); // top face at y = 0
            _spawned.Add(ground);

            var config = ScriptableObject.CreateInstance<RammerGateConfig>(); // spec defaults: 80 km/h, 0.85, 800 kg
            config.debrisSettleSeconds = TestSettleSeconds;
            _spawned.Add(config);

            // Build under an inactive holder so RammerGate.Awake runs only after its private fields are wired.
            var holder = new GameObject("RammerTestHolder");
            holder.SetActive(false);
            _spawned.Add(holder);

            var root = new GameObject("RammerGate_Test");
            root.transform.SetParent(holder.transform, false);
            root.transform.position = new Vector3(0f, 0f, WallZ);

            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, WallCentreY, 0f);
            solid.size = WallSize;
            var sensor = root.AddComponent<BoxCollider>(); // sized by RammerGate.Awake from the config

            var specs = new List<ChunkSpec>();
            int count = WallFractureLayout.Generate(WallSize, Columns, Rows, config.jitter, LayoutSeed, specs);
            chunks = new Rigidbody[count];
            for (int i = 0; i < count; i++)
            {
                GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube); // unit cube + BoxCollider
                chunk.name = "Chunk_" + i.ToString("00");
                chunk.transform.SetParent(root.transform, false);
                chunk.transform.localPosition = specs[i].LocalCenter + new Vector3(0f, WallCentreY, 0f);
                chunk.transform.localEulerAngles = specs[i].LocalEuler;
                chunk.transform.localScale = specs[i].Size;
                var rb = chunk.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                chunks[i] = rb;
            }

            var gate = root.AddComponent<RammerGate>();
            var so = new SerializedObject(gate);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_solid").objectReferenceValue = solid;
            so.FindProperty("_sensor").objectReferenceValue = sensor;
            SerializedProperty chunkProp = so.FindProperty("_chunks");
            chunkProp.arraySize = count;
            for (int i = 0; i < count; i++) chunkProp.GetArrayElementAtIndex(i).objectReferenceValue = chunks[i];
            so.FindProperty("_seed").intValue = LayoutSeed;
            so.ApplyModifiedPropertiesWithoutUndo();

            holder.SetActive(true);
            return gate;
        }

        /// <summary>Places the Jackal StartDistance before the front face, heading at yawDeg from +Z, and lets it settle.</summary>
        private IEnumerator SpawnCar(float yawDeg, Action<Rigidbody> onReady)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JackalPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Jackal prefab not found at " + JackalPrefabPath);

            Quaternion yaw = Quaternion.Euler(0f, yawDeg, 0f);
            Vector3 aim = new Vector3(0f, 0f, FrontFaceZ);
            Vector3 spawn = aim - yaw * Vector3.forward * StartDistance;
            spawn.y = SpawnHeight;

            GameObject car = Object.Instantiate(prefab, spawn, yaw);
            _spawned.Add(car);
            // The test drives the car itself; keyboard/gamepad must not overwrite the command.
            if (car.TryGetComponent(out PilotInputSource input)) input.enabled = false;
            Assert.That(car.TryGetComponent(out VehicleController controller), Is.True, "Jackal has no VehicleController");
            Assert.That(car.TryGetComponent(out Rigidbody body), Is.True);
            controller.SetPilotCommand(PilotCommand.Neutral); // coast: no throttle/brake muddying the speed numbers

            int settleSteps = Mathf.RoundToInt(SettleSeconds / Time.fixedDeltaTime);
            for (int i = 0; i < settleSteps; i++) yield return new WaitForFixedUpdate();
            onReady(body);
        }

        private static void Launch(Rigidbody car, float yawDeg, float speedMps)
        {
            car.linearVelocity = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward * speedMps;
            car.angularVelocity = Vector3.zero;
        }

        private static Vector3 Horizontal(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
#endif
