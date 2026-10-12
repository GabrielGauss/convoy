// Loads the Jackal prefab through AssetDatabase, which only exists in the Editor. The whole file is
// Editor-only so a player build of the test assembly still compiles (it just contains no tests).
#if UNITY_EDITOR
using System.Collections;
using Convoy.Core;
using Convoy.Input;
using Convoy.Vehicle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convoy.Tests.PlayMode
{
    /// <summary>
    /// D-01b handling regression lock (README rule: "Don't break the Jackal"). Drives the real Jackal prefab
    /// in a straight line on flat ground at full throttle and compares against the PT-01 measurements Gabriel
    /// rated 5/5. Any physics/handling change must keep these green; if a change is *meant* to alter feel,
    /// update the PT-01 constants only after a new playtest, never to make the test pass.
    /// </summary>
    [Category("Slow")]
    public class JackalHandlingRegressionTests
    {
        private const string JackalPrefabPath = "Assets/_Convoy/Prefabs/Jackal.prefab";

        // PT-01 measurements (docs/playtests/PT-01_2026-10-11.md).
        private const float Pt01ZeroTo100Seconds = 3.4f;
        private const float Pt01ZeroTo100ToleranceSeconds = 0.3f;     // spec D-01 AC 3a
        private const float Pt01SpeedAt6sKmh = 147f;
        private const float Pt01SpeedAt6sToleranceKmh = 10f;          // extra lock beyond the spec: catches top-speed/drag regressions

        // Damage on/off at full health must be the same car: within one 50 Hz physics step (spec AC 3a).
        private const float SameCarToleranceSeconds = 0.02f;
        private const float SameCarSpeedToleranceKmh = 0.5f;
        // Spec AC 3b: a wrecked hull is clearly slower.
        private const float ZeroHealthMaxSpeedRatio = 0.85f;

        private const float TargetKmh = 100f;
        private const float RunSeconds = 6f;
        private const float SettleSeconds = 1f;      // let the suspension settle before throttle, like a standing start
        private const float GroundSize = 2000f;      // 2 km: the run covers < 250 m, so the car never reaches an edge
        private const float SpawnHeight = 1f;        // small drop: ~4.4 m/s, below every impact threshold
        private const float MsToKmh = 3.6f;

        private enum Scenario { NoDamageComponent, FullHealth, ZeroHealth }

        private sealed class RunResult
        {
            public float ZeroTo100Seconds = float.NaN;
            public float SpeedAt6sKmh;
            public float FinalHealth01 = 1f;
        }

        // The undamaged run is the reference for every test; run it once per test session (it takes ~7 s real time).
        private static RunResult _baseline;

        [UnityTest]
        public IEnumerator NoDamageComponent_MatchesPt01()
        {
            yield return EnsureBaseline();

            Assert.That(_baseline.ZeroTo100Seconds, Is.Not.NaN, "Jackal never reached 100 km/h");
            Assert.That(_baseline.ZeroTo100Seconds,
                Is.EqualTo(Pt01ZeroTo100Seconds).Within(Pt01ZeroTo100ToleranceSeconds), "0-100 km/h drifted from PT-01");
            Assert.That(_baseline.SpeedAt6sKmh,
                Is.EqualTo(Pt01SpeedAt6sKmh).Within(Pt01SpeedAt6sToleranceKmh), "speed after 6 s drifted from PT-01");
        }

        [UnityTest]
        public IEnumerator FullHealth_DrivesIdenticallyToNoDamageComponent()
        {
            yield return EnsureBaseline();
            var full = new RunResult();
            yield return Run(Scenario.FullHealth, full);

            Assert.That(full.FinalHealth01, Is.EqualTo(1f), "a straight run on flat ground must not cause impact damage");
            Assert.That(full.ZeroTo100Seconds, Is.Not.NaN, "Jackal never reached 100 km/h");
            Assert.That(full.ZeroTo100Seconds,
                Is.EqualTo(_baseline.ZeroTo100Seconds).Within(SameCarToleranceSeconds));
            Assert.That(full.ZeroTo100Seconds,
                Is.EqualTo(Pt01ZeroTo100Seconds).Within(Pt01ZeroTo100ToleranceSeconds));
            Assert.That(full.SpeedAt6sKmh, Is.EqualTo(_baseline.SpeedAt6sKmh).Within(SameCarSpeedToleranceKmh));
        }

        [UnityTest]
        public IEnumerator ZeroHealth_IsClearlySlowerAfter6s()
        {
            yield return EnsureBaseline();
            var wrecked = new RunResult();
            yield return Run(Scenario.ZeroHealth, wrecked);

            Assert.That(wrecked.FinalHealth01, Is.EqualTo(0f));
            Assert.That(wrecked.SpeedAt6sKmh,
                Is.LessThanOrEqualTo(_baseline.SpeedAt6sKmh * ZeroHealthMaxSpeedRatio));
        }

        private static IEnumerator EnsureBaseline()
        {
            if (_baseline != null) yield break;
            var result = new RunResult();
            yield return Run(Scenario.NoDamageComponent, result);
            _baseline = result;
        }

        private static IEnumerator Run(Scenario scenario, RunResult result)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JackalPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Jackal prefab not found at " + JackalPrefabPath);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "RegressionGround";
            ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);
            ground.transform.position = new Vector3(0f, -0.5f, 0f); // top face at y = 0

            // Instantiate under an inactive parent so no Awake runs until the components are set up:
            // VehicleController looks up VehicleDamage once in Awake.
            var holder = new GameObject("RegressionHolder");
            holder.SetActive(false);
            var spawn = new Vector3(0f, SpawnHeight, -GroundSize * 0.45f);
            GameObject car = Object.Instantiate(prefab, spawn, Quaternion.identity, holder.transform);
            DamageConfig config = null;

            try
            {
                // The test drives the car itself; keyboard/gamepad must not overwrite the command.
                if (car.TryGetComponent(out PilotInputSource input)) input.enabled = false;

                // D-01c adds VehicleDamage to the prefab, so handle both "already there" and "missing".
                car.TryGetComponent(out VehicleDamage damage);
                if (scenario == Scenario.NoDamageComponent)
                {
                    if (damage != null) Object.DestroyImmediate(damage);
                    damage = null;
                }
                else
                {
                    if (damage == null) damage = car.AddComponent<VehicleDamage>();
                    // Fresh default config: the test locks code behaviour, independent of later asset tuning.
                    config = ScriptableObject.CreateInstance<DamageConfig>();
                    var so = new SerializedObject(damage);
                    so.FindProperty("_config").objectReferenceValue = config;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                holder.SetActive(true);

                Assert.That(car.TryGetComponent(out VehicleController controller), Is.True, "Jackal has no VehicleController");
                Assert.That(car.TryGetComponent(out Rigidbody body), Is.True);

                if (scenario == Scenario.ZeroHealth)
                {
                    damage.ApplyDamage(new DamageEvent { Amount = config.maxHealth, Kind = DamageKind.Hazard });
                    Assert.That(damage.State.Health01, Is.EqualTo(0f));
                }

                float dt = Time.fixedDeltaTime;
                controller.SetPilotCommand(PilotCommand.Neutral);
                int settleSteps = Mathf.RoundToInt(SettleSeconds / dt);
                for (int i = 0; i < settleSteps; i++) yield return new WaitForFixedUpdate();

                controller.SetPilotCommand(new PilotCommand { Throttle = 1f });

                // Count physics steps instead of reading clocks: identical step counts mean identical
                // simulated time even if the Editor drops frames. WaitForFixedUpdate resumes after the
                // physics step, so the body velocity already includes this step's forces.
                int runSteps = Mathf.RoundToInt(RunSeconds / dt);
                for (int step = 1; step <= runSteps; step++)
                {
                    yield return new WaitForFixedUpdate();
                    float kmh = body.linearVelocity.magnitude * MsToKmh;
                    if (float.IsNaN(result.ZeroTo100Seconds) && kmh >= TargetKmh)
                        result.ZeroTo100Seconds = step * dt;
                    if (step == runSteps) result.SpeedAt6sKmh = kmh;
                }

                result.FinalHealth01 = damage != null ? damage.State.Health01 : 1f;
                Debug.Log($"[JackalRegression] {scenario}: 0-100 km/h {result.ZeroTo100Seconds:0.000} s, " +
                          $"{RunSeconds} s -> {result.SpeedAt6sKmh:0.0} km/h, health {result.FinalHealth01:0.00}");
            }
            finally
            {
                Object.Destroy(holder);
                Object.Destroy(ground);
                if (config != null) Object.Destroy(config);
            }

            // Destroy is deferred to the end of the frame; wait so the next run starts on an empty scene.
            yield return null;
        }
    }
}
#endif
