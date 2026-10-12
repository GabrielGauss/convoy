// Loads the Jackal prefab through AssetDatabase, which only exists in the Editor. The whole file is
// Editor-only so a player build of the test assembly still compiles (it just contains no tests).
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Convoy.AI;
using Convoy.Input;
using Convoy.Vehicle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Convoy.Tests.PlayMode
{
    /// <summary>
    /// A-01b: the real Jackal prefab driven by <see cref="AiPilot"/> on a code-built flat "stadium" (two 150 m
    /// straights joined by two 40 m-radius turns) — no scene, no assets besides the prefab.
    /// Spec A-01 AC 2 (3 laps &lt; 90 s, 0 resets, within the band) and AC 6 (3 rivals &lt; 0.1 ms, 0 B GC),
    /// plus an open-polyline run (TR-01 rival routes are open lines that end at the crater).
    /// Profiles are fresh defaults (= AI_Balanced): the test locks code behaviour, not later asset tuning.
    /// </summary>
    [Category("Slow")]
    public class AiSyntheticTrackTests
    {
        private const string JackalPrefabPath = "Assets/_Convoy/Prefabs/Jackal.prefab";

        // Stadium geometry (spec AC 2).
        private const float StraightLength = 150f;
        private const float TurnRadius = 40f;
        private const float StraightSpacing = 5f;   // waypoint spacing on straights, metres
        private const float TurnStepDeg = 5f;       // ≈ 3.5 m spacing on the 40 m arcs
        private const float HalfWidth = 4f;

        // Spec AC 2.
        private const int Laps = 3;
        private const float LapTimeLimitSeconds = 90f;
        private const float MaxBandExcess = 2f;      // never further than halfWidth + 2 m from the line
        private const float GiveUpSeconds = 150f;    // sim-time cap so a broken AI fails instead of hanging

        // Spec AC 6.
        private const int Rivals = 3;
        private const float RivalBudgetMs = 0.1f;    // all rivals together, per physics step
        private const float PerfWarmupSeconds = 2f;
        private const float PerfMeasureSeconds = 5f;

        // Open-line run.
        private const float OpenLineGiveUpSeconds = 60f;
        // Past the end the brain brakes only down to the vehicle's reverseThreshold (0.6 m/s) so it never reverses;
        // rolling resistance does the rest. 1 m/s = clearly stopped without waiting for that slow roll-out.
        private const float StoppedSpeed = 1f;

        private const float GroundSize = 2000f;
        private const float SpawnHeight = 1f;        // small drop, below every impact-damage threshold
        private const float SettleSeconds = 1f;      // let the suspension settle before the AI is released
        private const float TestTimeScale = 4f;      // more physics steps per rendered frame; fixed timestep unchanged

        private readonly List<Object> _spawned = new List<Object>();
        private float _savedTimeScale;

        // Static sink so the positive-control allocation can't be optimised away.
        private static byte[] _sink;

        [SetUp]
        public void SetUp()
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = TestTimeScale;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _savedTimeScale;
            foreach (Object o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            yield return null; // Destroy is deferred to the end of the frame
        }

        // --- AC 2 ------------------------------------------------------------

        [UnityTest]
        public IEnumerator Stadium_ThreeLaps_Under90s_NoResets_StaysInBand()
        {
            SpawnGround();
            RacingLine line = SpawnLine(StadiumPoints(), closed: true);
            PolylinePath path = line.Path;
            AiDriverProfile profile = NewProfile();
            AiPilot pilot = SpawnRival(line, profile, startDistance: 0f, out VehicleController car);

            yield return Settle(pilot);

            float dt = Time.fixedDeltaTime;
            int maxSteps = Mathf.RoundToInt(GiveUpSeconds / dt);
            float target = Laps * path.Length;
            float travelled = 0f;
            float lastProgress = pilot.Brain.Progress;
            float worstLateral = 0f;
            int segmentHint = 0;
            int steps = 0;

            while (steps < maxSteps && travelled < target)
            {
                yield return new WaitForFixedUpdate();
                steps++;

                // Wrap-aware progress: crossing the start line jumps Progress by ±Length.
                float delta = pilot.Brain.Progress - lastProgress;
                if (delta > path.Length * 0.5f) delta -= path.Length;
                else if (delta < -path.Length * 0.5f) delta += path.Length;
                travelled += delta;
                lastProgress = pilot.Brain.Progress;

                PathProjection p = path.Project(car.State.Position, segmentHint, 8);
                segmentHint = p.Segment;
                worstLateral = Mathf.Max(worstLateral, Mathf.Abs(p.LateralOffset));
            }

            float seconds = steps * dt;
            Debug.Log($"[AiSyntheticTrack] stadium {path.Length:0} m/lap: {travelled / path.Length:0.00} laps in {seconds:0.0} s, " +
                      $"resets {pilot.Brain.ResetRequests}, worst |lateral| {worstLateral:0.00} m");

            Assert.That(travelled, Is.GreaterThanOrEqualTo(target), $"did not finish {Laps} laps within {GiveUpSeconds} s");
            Assert.That(seconds, Is.LessThan(LapTimeLimitSeconds), $"{Laps} laps took too long");
            Assert.That(pilot.Brain.ResetRequests, Is.EqualTo(0), "AI asked for a reset on an empty track");
            Assert.That(worstLateral, Is.LessThanOrEqualTo(HalfWidth + MaxBandExcess), "left the route band");
        }

        // --- Open polylines (TR-01 rival routes) -------------------------------

        [UnityTest]
        public IEnumerator OpenLine_DrivesToTheEnd_AndStops()
        {
            SpawnGround();
            RacingLine line = SpawnLine(OpenSCurvePoints(), closed: false);
            AiPilot pilot = SpawnRival(line, NewProfile(), startDistance: 0f, out VehicleController car);

            yield return Settle(pilot);

            float dt = Time.fixedDeltaTime;
            int maxSteps = Mathf.RoundToInt(OpenLineGiveUpSeconds / dt);
            int steps = 0;
            while (steps < maxSteps && !(pilot.Brain.ReachedEnd && Mathf.Abs(car.State.ForwardSpeed) < StoppedSpeed))
            {
                yield return new WaitForFixedUpdate();
                steps++;
            }

            Debug.Log($"[AiSyntheticTrack] open line {line.Path.Length:0} m: progress {pilot.Brain.Progress:0.0} m " +
                      $"in {steps * dt:0.0} s, resets {pilot.Brain.ResetRequests}");

            Assert.That(pilot.Brain.ReachedEnd, Is.True, "never reached the end of the open line");
            Assert.That(Mathf.Abs(car.State.ForwardSpeed), Is.LessThan(StoppedSpeed), "did not stop at the end");
            Assert.That(pilot.Brain.ResetRequests, Is.EqualTo(0));
        }

        // --- AC 6 ------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThreeRivals_TickUnderBudget_AndAllocateNothing()
        {
            SpawnGround();
            RacingLine line = SpawnLine(StadiumPoints(), closed: true);
            AiDriverProfile profile = NewProfile();
            var pilots = new AiPilot[Rivals];
            for (int i = 0; i < Rivals; i++)
                pilots[i] = SpawnRival(line, profile, startDistance: i * 25f, out _); // nose to tail: casts see each other

            yield return Settle(pilots);

            float dt = Time.fixedDeltaTime;
            int warmup = Mathf.RoundToInt(PerfWarmupSeconds / dt);
            for (int i = 0; i < warmup; i++) yield return new WaitForFixedUpdate();

            // Positive control: prove this runtime's counter reports allocations, so the 0 below means something.
            long probeStart = System.GC.GetAllocatedBytesForCurrentThread();
            _sink = new byte[1024];
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - probeStart, Is.GreaterThanOrEqualTo(1024L),
                "GC.GetAllocatedBytesForCurrentThread does not report allocations in this runtime");

            // Measure exactly the AiPilot work: after each physics step, run one extra Tick per rival with the
            // counters around it (the extra command is simply overwritten by next step's own Tick).
            int measure = Mathf.RoundToInt(PerfMeasureSeconds / dt);
            long allocated = 0;
            double totalMs = 0;
            double worstMs = 0;
            var watch = new Stopwatch();
            for (int s = 0; s < measure; s++)
            {
                yield return new WaitForFixedUpdate();

                long before = System.GC.GetAllocatedBytesForCurrentThread();
                watch.Restart();
                for (int i = 0; i < Rivals; i++) pilots[i].Tick();
                watch.Stop();
                allocated += System.GC.GetAllocatedBytesForCurrentThread() - before;

                double ms = watch.Elapsed.TotalMilliseconds;
                totalMs += ms;
                if (ms > worstMs) worstMs = ms;
            }

            double meanMs = totalMs / measure;
            Debug.Log($"[AiSyntheticTrack] {Rivals} rivals: mean {meanMs:0.0000} ms/step, worst {worstMs:0.0000} ms, GC {allocated} B " +
                      "(Editor timing; confirm in the Profiler row Convoy.AiPilot.FixedUpdate)");

            Assert.That(allocated, Is.EqualTo(0L), "AiPilot.Tick must not allocate");
            Assert.That(meanMs, Is.LessThan(RivalBudgetMs), $"{Rivals} rivals over budget");
        }

        // --- setup helpers -------------------------------------------------------

        private void SpawnGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "AiTestGround";
            ground.transform.localScale = new Vector3(GroundSize, 1f, GroundSize);
            ground.transform.position = new Vector3(0f, -0.5f, 0f); // top face at y = 0
            _spawned.Add(ground);
        }

        private RacingLine SpawnLine(List<Vector3> points, bool closed)
        {
            var go = new GameObject("AiTestRacingLine");
            _spawned.Add(go);
            RacingLine line = go.AddComponent<RacingLine>();
            line.Configure(points, closed, HalfWidth);
            Assert.That(line.Path, Is.Not.Null);
            return line;
        }

        private AiDriverProfile NewProfile()
        {
            var profile = ScriptableObject.CreateInstance<AiDriverProfile>();
            _spawned.Add(profile);
            return profile;
        }

        private AiPilot SpawnRival(RacingLine line, AiDriverProfile profile, float startDistance, out VehicleController controller)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JackalPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Jackal prefab not found at " + JackalPrefabPath);

            PolylinePath path = line.Path;
            Vector3 position = path.PositionAt(startDistance) + Vector3.up * SpawnHeight;
            Quaternion rotation = Quaternion.LookRotation(path.TangentAt(startDistance), Vector3.up);

            // Instantiate under an inactive parent so no Awake runs before the AI is wired (AiPilot.Configure
            // must precede Awake, and the keyboard source must be off before it ever reads input).
            var holder = new GameObject("AiTestHolder");
            holder.SetActive(false);
            _spawned.Add(holder);
            GameObject car = Object.Instantiate(prefab, position, rotation, holder.transform);

            // Until F-03c the prefab still carries the keyboard source; a rival must not read the keyboard.
            if (car.TryGetComponent(out PilotInputSource input)) input.enabled = false;

            AiPilot pilot = car.AddComponent<AiPilot>();
            pilot.Configure(line, profile);
            holder.SetActive(true);

            Assert.That(car.TryGetComponent(out controller), Is.True, "Jackal has no VehicleController");
            Assert.That(pilot.Brain, Is.Not.Null, "AiPilot did not build its brain");
            return pilot;
        }

        // Hold the AI while the suspension settles after the spawn drop (standing start, like the regression test),
        // then release every rival on the same step.
        private static IEnumerator Settle(params AiPilot[] pilots)
        {
            foreach (AiPilot pilot in pilots) pilot.Brain.Held = true;
            int steps = Mathf.RoundToInt(SettleSeconds / Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
            foreach (AiPilot pilot in pilots) pilot.Release();
        }

        /// <summary>
        /// Counter-clockwise from above (left-hand turns): up the x = +40 straight heading +Z, turn around (0, 75),
        /// down the x = −40 straight heading −Z, turn around (0, −75). Waypoints are not repeated at the seam. Lap ≈ 2 × 150 + 2π × 40 ≈ 551 m.
        /// </summary>
        private static List<Vector3> StadiumPoints()
        {
            var points = new List<Vector3>();
            float half = StraightLength * 0.5f;
            int straightSteps = Mathf.RoundToInt(StraightLength / StraightSpacing);
            int turnSteps = Mathf.RoundToInt(180f / TurnStepDeg);

            for (int i = 0; i < straightSteps; i++)                       // east straight, +Z
                points.Add(new Vector3(TurnRadius, 0f, -half + i * StraightSpacing));
            for (int i = 0; i < turnSteps; i++)                           // north turn, centre (0, +half)
            {
                float a = i * TurnStepDeg * Mathf.Deg2Rad;
                points.Add(new Vector3(TurnRadius * Mathf.Cos(a), 0f, half + TurnRadius * Mathf.Sin(a)));
            }
            for (int i = 0; i < straightSteps; i++)                       // west straight, −Z
                points.Add(new Vector3(-TurnRadius, 0f, half - i * StraightSpacing));
            for (int i = 0; i < turnSteps; i++)                           // south turn, centre (0, −half)
            {
                float a = i * TurnStepDeg * Mathf.Deg2Rad;
                points.Add(new Vector3(-TurnRadius * Mathf.Cos(a), 0f, -half - TurnRadius * Mathf.Sin(a)));
            }
            return points;
        }

        /// <summary>
        /// Open, irregular line like an exported TR-01 rival route: 60 m straight, a gentle right, a gentle left,
        /// uneven waypoint spacing (4–15 m), ending 60 m after the last bend. Not a loop, does not start at the origin.
        /// </summary>
        private static List<Vector3> OpenSCurvePoints()
        {
            var points = new List<Vector3>();
            Vector3 p = new Vector3(200f, 0f, -300f);
            float heading = 0f; // degrees from +Z, clockwise
            points.Add(p);

            void Advance(float metres, float turnDeg, float spacing)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(metres / spacing));
                for (int i = 0; i < n; i++)
                {
                    heading += turnDeg / n;
                    float rad = heading * Mathf.Deg2Rad;
                    p += new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * (metres / n);
                    points.Add(p);
                }
            }

            Advance(60f, 0f, 15f);    // straight
            Advance(90f, 60f, 4f);    // right bend, R ≈ 86 m
            Advance(40f, 0f, 12f);
            Advance(90f, -60f, 6f);   // left bend
            Advance(60f, 0f, 15f);    // run-out to the end
            return points;
        }
    }
}
#endif
