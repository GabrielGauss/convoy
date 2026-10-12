// Loads the Jackal prefab through AssetDatabase and wires private fields through SerializedObject, both
// Editor-only. The whole file is Editor-only so a player build of the test assembly still compiles.
#if UNITY_EDITOR
using System.Collections;
using Convoy.Core;
using Convoy.Input;
using Convoy.Race;
using Convoy.Vehicle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Convoy.Tests.PlayMode
{
    /// <summary>
    /// UP-01b (spec AC 3–4) + D-031: the Monolith uplink with the real Jackal on code-built flat ground.
    /// Scenario: settle, full throttle for 2 s along +Z, then brake to a stop. Zones are placed relative to
    /// that path (Jackal: ~16–20 m after 2 s, ~12–16 m braking with brakeForce 14000 N on 1200 kg).
    /// Timing is measured in physics steps, not wall clock, so Editor frame drops don't matter.
    /// </summary>
    [Category("Slow")]
    public class UplinkTests
    {
        private const string JackalPrefabPath = "Assets/_Convoy/Prefabs/Jackal.prefab";
        private const float GroundSize = 400f;
        private const float SpawnHeight = 1f;
        private const float SettleSeconds = 1f;
        private const float ThrottleSeconds = 2f;
        private const float StopSpeed = 0.6f;          // Jackal reverseThreshold: below it, Throttle −1 would reverse
        private const float TimeoutSeconds = 10f;      // after the throttle phase (spec AC 4b: "within 10 s")
        private const float HoldSeconds = 4f;          // UplinkConfig default
        private const float HoldUpperBound = 4.1f;     // spec AC 4a
        private const float FarAway = 300f;            // finish gate the car can never reach

        // SprintController writes PlayerPrefs on finish (best time per scene); keep the developer's value intact.
        private string _bestKey;
        private bool _hadBest;
        private float _savedBest;

        private sealed class Rig
        {
            public GameObject Holder, Ground;
            public VehicleController Car;
            public Rigidbody Body;
            public SprintController Sprint;
            public UplinkZone Zone;
            public UplinkConfig Config;

            public int Step;                    // physics steps completed since the throttle phase started
            public int RunningStep = -1;        // first step the run was observed Running
            public int EntryStep = -1;          // first step the body was inside the zone
            public int FinishedStep = -1;       // step counter value when Finished fired
            public float ElapsedAtFinish;
            public Vector3 PositionAtFinish;
            public int CompletedCount;
            public VehicleController CompletedVehicle;
        }

        [SetUp]
        public void SetUp()
        {
            _bestKey = "convoy.sprint.best." + SceneManager.GetActiveScene().name;
            _hadBest = PlayerPrefs.HasKey(_bestKey);
            _savedBest = PlayerPrefs.GetFloat(_bestKey, 0f);
            PlayerPrefs.DeleteKey(_bestKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadBest) PlayerPrefs.SetFloat(_bestKey, _savedBest);
            else PlayerPrefs.DeleteKey(_bestKey);
            PlayerPrefs.Save();
        }

        // --- Spec AC 3 -------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator LegacyFinishGateStillFinishes()
        {
            const float gateZ = 10f;
            Rig rig = Build(withSprint: true, withUplink: false, gateZ: gateZ, zoneCentreZ: 0f, zoneHalf: Vector3.one, trackedConfig: false);
            try
            {
                Assert.That(rig.Sprint.UsesUplink, Is.False);
                Assert.That(rig.Sprint.Uplink, Is.Null);
                yield return Drive(rig);

                Assert.That(rig.FinishedStep, Is.GreaterThanOrEqualTo(0), "crossing the finish gate did not finish the run");
                Assert.That(rig.Sprint.Run.Phase, Is.EqualTo(SprintPhase.Finished));
                // Instant: the run ends as the car crosses the gate, not later.
                Assert.That(rig.PositionAtFinish.z, Is.InRange(gateZ - 3f, gateZ + 3f));
            }
            finally { Teardown(rig); }
            yield return null;
        }

        // --- Spec AC 4a + D-031 winner ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator StopInsideZone_FinishesAfterHold()
        {
            // 30 × 6 × 60 m zone from z = 10 to 70: entered during the throttle phase, the car stops inside.
            Rig rig = Build(withSprint: true, withUplink: true, gateZ: FarAway, zoneCentreZ: 40f,
                zoneHalf: new Vector3(15f, 3f, 30f), trackedConfig: false);
            try
            {
                Assert.That(rig.Sprint.UsesUplink, Is.True);
                yield return Drive(rig);

                float dt = Time.fixedDeltaTime;
                Assert.That(rig.EntryStep, Is.GreaterThanOrEqualTo(0), "car never entered the zone");
                Assert.That(rig.FinishedStep, Is.GreaterThanOrEqualTo(0), "uplink never finished the run");
                // Finished fired during physics step FinishedStep + 1; the body was first inside after step EntryStep.
                float held = (rig.FinishedStep + 1 - rig.EntryStep) * dt;
                Debug.Log($"[UplinkTests] entered at step {rig.EntryStep}, finished at {rig.FinishedStep}: hold {held:0.000} s, elapsed {rig.ElapsedAtFinish:0.000} s");
                Assert.That(held, Is.InRange(HoldSeconds - 0.001f, HoldUpperBound));

                // The clock kept running during the hold.
                float timeToZone = (rig.EntryStep - rig.RunningStep) * dt;
                Assert.That(rig.ElapsedAtFinish, Is.GreaterThanOrEqualTo(timeToZone + HoldSeconds - 2f * dt));

                Assert.That(rig.Zone.Winner, Is.SameAs(rig.Car), "D-031: the zone must record the winning vehicle");
                Assert.That(rig.CompletedCount, Is.EqualTo(1));
                Assert.That(rig.CompletedVehicle, Is.SameAs(rig.Car));
                Assert.That(rig.Sprint.Uplink.Complete, Is.True);
            }
            finally { Teardown(rig); }
            yield return null;
        }

        // --- Spec AC 4b (+ the finish gate is ignored in uplink mode) ----------------------------------

        [UnityTest]
        public IEnumerator ZoneEndsBeforeCarStops_NeverFinishes()
        {
            // Zone z = 6..10 is crossed at speed; the car stops ~30 m out. The finish gate at z = 4 is driven
            // through first and must be ignored because the uplink replaces it.
            Rig rig = Build(withSprint: true, withUplink: true, gateZ: 4f, zoneCentreZ: 8f,
                zoneHalf: new Vector3(15f, 3f, 2f), trackedConfig: false);
            try
            {
                yield return Drive(rig);

                Assert.That(rig.EntryStep, Is.GreaterThanOrEqualTo(0), "test setup: the car should pass through the zone");
                Assert.That(rig.Zone.Contains(rig.Body.position), Is.False, "test setup: the car should stop beyond the zone");
                Assert.That(rig.FinishedStep, Is.EqualTo(-1), "run finished without completing the uplink");
                Assert.That(rig.Sprint.Run.Phase, Is.EqualTo(SprintPhase.Running));
                Assert.That(rig.Sprint.Uplink.HeldSeconds, Is.EqualTo(0f), "Reset mode: leaving the zone clears the hold");
                Assert.That(rig.Zone.HasWinner, Is.False);
            }
            finally { Teardown(rig); }
            yield return null;
        }

        // --- D-031: any vehicle can win (future AI rivals), no SprintController needed --------------------

        [UnityTest]
        public IEnumerator TrackedVehicle_CompletesUplink_RaisesCompletedWithVehicle()
        {
            Rig rig = Build(withSprint: false, withUplink: true, gateZ: FarAway, zoneCentreZ: 40f,
                zoneHalf: new Vector3(15f, 3f, 30f), trackedConfig: true);
            try
            {
                UplinkHold hold = rig.Zone.Track(rig.Car);
                Assert.That(hold, Is.Not.Null);
                Assert.That(rig.Zone.Track(rig.Car), Is.SameAs(hold), "Track must be idempotent");

                yield return Drive(rig);

                Assert.That(rig.CompletedCount, Is.EqualTo(1));
                Assert.That(rig.CompletedVehicle, Is.SameAs(rig.Car));
                Assert.That(rig.Zone.Winner, Is.SameAs(rig.Car));
                Assert.That(hold.Complete, Is.True);
                // A later report (e.g. the player finishing second) does not change the winner.
                Assert.That(rig.Zone.ReportCompleted(rig.Car), Is.False);
                Assert.That(rig.CompletedCount, Is.EqualTo(1));
            }
            finally { Teardown(rig); }
            yield return null;
        }

        // --- UplinkZone geometry (no physics) -----------------------------------------------------------

        [Test]
        public void Contains_UsesZoneRotationAndHalfExtents()
        {
            var go = new GameObject("UplinkZoneGeometry");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(100f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                var zone = go.AddComponent<UplinkZone>();
                SetHalfExtents(zone, new Vector3(2f, 1f, 10f));

                // Rotated 90° about Y: the zone's local Z (10 m) now runs along world X.
                Assert.That(zone.Contains(new Vector3(109f, 0f, 0f)), Is.True);
                Assert.That(zone.Contains(new Vector3(100f, 0f, 9f)), Is.False);
                Assert.That(zone.Contains(new Vector3(100f, 0f, 2f)), Is.True, "faces are inclusive");
                Assert.That(zone.Contains(new Vector3(100f, 1.5f, 0f)), Is.False);
                Assert.That(zone.ReportCompleted(null), Is.False);
                Assert.That(zone.HasWinner, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- Helpers -------------------------------------------------------------------------------------

        private static void SetHalfExtents(UplinkZone zone, Vector3 half)
        {
            var so = new SerializedObject(zone);
            so.FindProperty("_halfExtents").vector3Value = half;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Rig Build(bool withSprint, bool withUplink, float gateZ, float zoneCentreZ, Vector3 zoneHalf, bool trackedConfig)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JackalPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Jackal prefab not found at " + JackalPrefabPath);

            var rig = new Rig();
            rig.Ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rig.Ground.name = "UplinkTestGround";
            rig.Ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);
            rig.Ground.transform.position = new Vector3(0f, -0.5f, 0f); // top face at y = 0

            // Everything is built under an inactive parent so no Awake runs before the private fields are wired:
            // SprintController decides UsesUplink and binds its gates once, in Awake.
            rig.Holder = new GameObject("UplinkTestHolder");
            rig.Holder.SetActive(false);

            GameObject car = Object.Instantiate(prefab, new Vector3(0f, SpawnHeight, 0f), Quaternion.identity, rig.Holder.transform);
            // The test drives the car itself; keyboard/gamepad must not overwrite the command.
            if (car.TryGetComponent(out PilotInputSource input)) input.enabled = false;
            if (car.TryGetComponent(out SeatInputRouter router)) router.enabled = false;
            Assert.That(car.TryGetComponent(out rig.Car), Is.True, "Jackal has no VehicleController");
            Assert.That(car.TryGetComponent(out rig.Body), Is.True);

            if (withUplink)
            {
                var zoneGo = new GameObject("Uplink");
                zoneGo.transform.SetParent(rig.Holder.transform, false);
                zoneGo.transform.position = new Vector3(0f, 0f, zoneCentreZ);
                rig.Zone = zoneGo.AddComponent<UplinkZone>();
                rig.Config = ScriptableObject.CreateInstance<UplinkConfig>(); // defaults: 4 s, Reset, no speed limit
                var zso = new SerializedObject(rig.Zone);
                zso.FindProperty("_halfExtents").vector3Value = zoneHalf;
                if (trackedConfig) zso.FindProperty("_trackedConfig").objectReferenceValue = rig.Config;
                zso.ApplyModifiedPropertiesWithoutUndo();
                rig.Zone.Completed += v => { rig.CompletedCount++; rig.CompletedVehicle = v; };
            }

            if (withSprint)
            {
                GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gate.name = "Finish";
                gate.transform.SetParent(rig.Holder.transform, false);
                gate.transform.position = new Vector3(0f, 1f, gateZ);
                gate.transform.localScale = new Vector3(40f, 6f, 1f);
                gate.GetComponent<Collider>().isTrigger = true;
                SprintGate finish = gate.AddComponent<SprintGate>();

                var sprintGo = new GameObject("Sprint");
                sprintGo.transform.SetParent(rig.Holder.transform, false);
                rig.Sprint = sprintGo.AddComponent<SprintController>();
                var so = new SerializedObject(rig.Sprint);
                so.FindProperty("_vehicle").objectReferenceValue = rig.Car;
                so.FindProperty("_finish").objectReferenceValue = finish;
                so.FindProperty("_checkpoints").arraySize = 0;  // spec AC 4: 0 checkpoints
                if (withUplink)
                {
                    so.FindProperty("_uplink").objectReferenceValue = rig.Zone;
                    so.FindProperty("_uplinkConfig").objectReferenceValue = rig.Config;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                rig.Sprint.Finished += () =>
                {
                    rig.FinishedStep = rig.Step;
                    rig.ElapsedAtFinish = rig.Sprint.Run.Elapsed;
                    rig.PositionAtFinish = rig.Body.position;
                };
            }

            rig.Holder.SetActive(true);
            return rig;
        }

        /// <summary>Settle, full throttle for 2 s, brake to a stop, then wait until the timeout or a finish.</summary>
        private static IEnumerator Drive(Rig rig)
        {
            float dt = Time.fixedDeltaTime;
            rig.Car.SetPilotCommand(PilotCommand.Neutral);
            int settleSteps = Mathf.RoundToInt(SettleSeconds / dt);
            for (int i = 0; i < settleSteps; i++) yield return new WaitForFixedUpdate();

            int throttleSteps = Mathf.RoundToInt(ThrottleSeconds / dt);
            int totalSteps = throttleSteps + Mathf.RoundToInt(TimeoutSeconds / dt);
            bool done = false;
            for (rig.Step = 0; rig.Step < totalSteps && !done; )
            {
                PilotCommand cmd;
                if (rig.Step < throttleSteps) cmd = new PilotCommand { Throttle = 1f };
                else
                {
                    float forward = Vector3.Dot(rig.Body.linearVelocity, rig.Body.transform.forward);
                    // Brake with reverse throttle while rolling; below the reverse threshold that would reverse,
                    // so hold the handbrake instead and let rolling resistance finish the stop.
                    cmd = forward > StopSpeed ? new PilotCommand { Throttle = -1f } : new PilotCommand { Handbrake = true };
                }
                rig.Car.SetPilotCommand(cmd);

                // WaitForFixedUpdate resumes after the physics step; increment after it so handlers that fire
                // inside the next step's FixedUpdate see "steps completed so far".
                yield return new WaitForFixedUpdate();
                rig.Step++;

                if (rig.RunningStep < 0 && rig.Sprint != null && rig.Sprint.Run.Phase == SprintPhase.Running) rig.RunningStep = rig.Step;
                if (rig.EntryStep < 0 && rig.Zone != null && rig.Zone.Contains(rig.Body.position)) rig.EntryStep = rig.Step;
                done = rig.FinishedStep >= 0 || rig.CompletedCount > 0;
            }
        }

        private static void Teardown(Rig rig)
        {
            if (rig.Holder != null) Object.Destroy(rig.Holder);
            if (rig.Ground != null) Object.Destroy(rig.Ground);
            if (rig.Config != null) Object.Destroy(rig.Config);
        }
    }
}
#endif
