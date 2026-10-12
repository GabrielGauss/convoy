# Spec A-01 — AI rival driving

- **Human owner/reviewer:** Gabriel or Lead Dev (backlog "GV or DEV"); racing lines for T-01: Narrative & Game Designer · **Builder agent:** ai-engineer · **Architecture:** ADR-001, ADR-004 (D-019)
- **Split:** **A-01a** pure logic + profile SO + tests · **A-01b** runtime adapters (`RacingLine`, `AiPilot`), HUD target fix, PlayMode synthetic-track test · **A-01c** integration script for `Sprint_01` (racing line from `Road_N`, 3 rivals — executed by the orchestrator)

## Objective
AI rivals drive the standard Jackal through `IPilotCommandReceiver.SetPilotCommand` — exactly the path a human's commands take — following a waypoint racing line with lookahead steering, curvature-based speed planning, lane-based obstacle avoidance and stuck recovery via V-02 resets. No NavMesh, no physics shortcuts, no rubber-banding. Target: ≥ 90 % of runs finish without getting stuck, within ±15 % of a competent human's time.

## Scope / out of scope
- In: `PolylinePath`, `SpeedPlanner`, `LaneSelector`, `StuckDetector`, `AiDriverBrain`, `AiDriverProfile`; `RacingLine`, `AiPilot`; `Sprint_01` racing line + 3 rivals; presentation target fix.
- Out: race positions/countdown/results (R-01), rival gunners (A-02, post-G-01), fork choice (follow-up after T-01), AI seat assist (K-02), visual tint for rivals, rubber-banding (never in M1), Unity Splines package.

## Public interfaces (`Convoy.AI`, `Scripts/AI/`, asmdef refs Core + Vehicle)
```csharp
namespace Convoy.AI
{
    public struct PathProjection { public int Segment; public float Distance; public float LateralOffset; public Vector3 Point; }

    /// <summary>Immutable polyline (points copied once). Distances are metres along the line.</summary>
    public sealed class PolylinePath
    {
        public PolylinePath(IReadOnlyList<Vector3> points, bool closed);   // ≥ 2 points, throws otherwise
        public int PointCount { get; }  public float Length { get; }  public bool Closed { get; }
        public PathProjection Project(Vector3 position, int segmentHint, int searchWindow); // window search, no alloc
        public PathProjection ProjectGlobal(Vector3 position);                            // full search (after teleports)
        public Vector3 PositionAt(float distance);    // clamped (open) or wrapped (closed)
        public Vector3 TangentAt(float distance);     // unit, horizontal
        public float CurvatureAt(float distance, float span); // |heading change| / span, 1/m, on the XZ plane
    }

    public static class SpeedPlanner
    {
        /// <summary>Max speed allowed now so every corner within the horizon can be taken (ADR-004 formula).</summary>
        public static float TargetSpeed(PolylinePath path, float distance, float vehicleTopSpeed, AiDriverProfile profile);
        public static bool ClearForBoost(PolylinePath path, float distance, float vehicleTopSpeed, AiDriverProfile profile);
    }

    public sealed class LaneSelector
    {
        public LaneSelector(AiDriverProfile profile, float halfWidth);
        public float Offset { get; }                                       // metres, + = right of path
        public void Step(float obstacleLeft, float obstacleCentre, float obstacleRight, float deltaTime); // distances, +∞ = clear
    }

    public enum AiDriveMode { Holding, Driving, Reversing, RequestReset }

    public sealed class StuckDetector
    {
        public StuckDetector(AiDriverProfile profile);
        public AiDriveMode Mode { get; }
        public void Step(float speed, float requestedThrottle, float lateralOffset, float progressDelta, float deltaTime);
        public void NotifyResetIssued();
    }

    public struct AiSensorFrame
    {
        public float DeltaTime;
        public Vector3 Position, Forward, Velocity;
        public float ForwardSpeed;
        public int GroundedWheels;
        public float ObstacleLeft, ObstacleCentre, ObstacleRight;   // metres to first obstacle, float.PositiveInfinity = clear
    }

    public sealed class AiDriverBrain
    {
        public AiDriverBrain(PolylinePath path, AiDriverProfile profile, VehicleConfig vehicle, float halfWidth);
        public PilotCommand Step(in AiSensorFrame frame);   // no alloc
        public float Progress { get; }                       // metres along path
        public bool ReachedEnd { get; }                      // open path: Progress ≥ Length − arriveDistance
        public AiDriveMode Mode { get; }
        public int ResetRequests { get; }
        public bool Held { get; set; }                       // true → Neutral command (start hold)
    }

    [CreateAssetMenu(fileName = "AiDriverProfile", menuName = "Convoy/AI Driver Profile")]
    public sealed class AiDriverProfile : ScriptableObject
    {
        [Header("Steering")] [Min(0f)] public float lookaheadBase = 6f; [Min(0f)] public float lookaheadPerSpeed = 0.5f; [Min(0f)] public float steerGain = 1f;
        [Header("Speed")] [Min(0.1f)] public float maxLateralAccel = 14f; [Range(0.5f, 1.2f)] public float cornerSpeedScale = 0.92f;
                          [Min(0.1f)] public float brakingDecel = 12f; [Min(1f)] public float planningHorizon = 120f; [Min(0.5f)] public float planningStep = 5f;
                          [Min(0.5f)] public float curvatureSpan = 8f; [Min(0f)] public float throttleGain = 0.5f; [Min(0f)] public float coastBand = 1f;
                          public bool useBoost = true; [Min(0f)] public float boostClearDistance = 80f;
        [Header("Lanes")] [Min(0f)] public float laneWidth = 2.5f; [Min(0.1f)] public float laneChangeRate = 3f; [Min(1f)] public float obstacleCheckDistance = 25f;
                          [Min(0.1f)] public float obstacleRadius = 1f; [Min(0f)] public float obstacleCastHeight = 0.8f; [Range(0f, 1f)] public float walkableNormalUp = 0.5f;
        [Header("Recovery")] [Min(0f)] public float stuckSpeed = 1f; [Min(0f)] public float stuckTime = 2.5f; [Min(0f)] public float reverseTime = 1.5f;
                             [Min(0)] public int maxReverseAttempts = 2; [Min(1f)] public float offPathResetDistance = 25f; [Min(0f)] public float wrongWayTime = 4f;
                             [Min(0f)] public float recoveryBackoff = 10f; [Min(1)] public int projectionWindow = 8; [Min(0f)] public float arriveDistance = 5f;
        [Header("Start (temporary until R-01)")] [Min(0f)] public float releaseSpeed = 1.5f;
    }
}
```
Vehicle limits (`maxSteerAngle`, `topSpeed`, `highSpeedSteerFactor`) are read from `VehicleConfig`; current max steer via `VehicleMath.SteerAngle(1f, …)`.

### A-01b adapters
```csharp
[DisallowMultipleComponent]
public sealed class RacingLine : MonoBehaviour           // ordered child Transforms = waypoints
{
    [SerializeField] private bool _closed;
    [Min(0.5f)] [SerializeField] private float _halfWidth = 4f;
    public PolylinePath Path { get; }                     // built once in Awake from child positions
    public float HalfWidth { get; }
    // OnDrawGizmos: polyline + halfWidth band (editor only)
}

[RequireComponent(typeof(VehicleController))] [DisallowMultipleComponent] [DefaultExecutionOrder(-50)]
public sealed class AiPilot : MonoBehaviour, IRecoveryPointProvider
{
    [SerializeField] private RacingLine _line;
    [SerializeField] private AiDriverProfile _profile;
    [SerializeField] private LayerMask _obstacleMask = ~0;
    [Tooltip("Temporary until R-01: hold until this vehicle starts moving.")] [SerializeField] private VehicleController _waitForVehicle;
    public AiDriverBrain Brain { get; }
    public void Release();                               // R-01 calls this after the countdown
    public bool TryGetRecoveryPose(out Pose pose);       // path pose at Progress − recoveryBackoff, facing tangent
    // Awake: brain, vehicle.SetRecoveryPointProvider(this). FixedUpdate: 3× Physics.SphereCastNonAlloc (preallocated buffer,
    // skip own rigidbody and hits with normal·up > walkableNormalUp) → AiSensorFrame → brain.Step → vehicle.SetPilotCommand.
}
```
Presentation fix (A-01b): `SpeedometerHud` and `VehicleDebugOverlay` auto-target the first `VehicleController` **without** an `AiPilot` (`TryGetComponent<AiPilot>`), so the HUD never shows a rival. `Convoy.Presentation.asmdef` adds `Convoy.AI`.

## Data assets (A-01c script)
`Assets/_Convoy/Config/AI/AI_Steady.asset` (cornerSpeedScale 0.85), `AI_Balanced.asset` (0.92), `AI_Aggressive.asset` (0.98); other fields default.

## A-01c integration script — `Convoy/Integration/A-01 Sprint AI Setup`
1. Create the three profiles if missing.
2. In `Sprint_01`, if `RacingLine_Main` is missing: collect objects named `Road_<n>`, sort by `n` numerically, take every 4th world position (y + 0.5 m) as children `WP_000…`; append one point 20 m past the finish gate along its forward. `_halfWidth = 4`.
3. If `Rival_1..3` missing: instantiate `Jackal.prefab` at the player's start pose offset (−4 m right, −8 m back), (+4 m, −8 m), (0, −16 m); add `AiPilot` (line, profiles Steady/Balanced/Aggressive, mask = Default, `_waitForVehicle` = player Jackal).
4. Save, log. Idempotent; never deletes.

## Dependencies
Blocked by: — (A-01a); A-01a (A-01b); A-01b **and F-03c** (A-01c — the prefab must no longer read the keyboard, otherwise all rivals drive with WASD). Blocks: R-01 (rivals), G-01 (targets), A-02. ADRs: ADR-001, ADR-004.

## Allowed files
- **A-01a:** `Assets/_Convoy/Scripts/AI/**` (new asmdef `Convoy.AI` + pure classes + `AiDriverProfile.cs`), `Assets/_Convoy/Tests/EditMode/AI/**` (new test asmdef + tests).
- **A-01b:** `Assets/_Convoy/Scripts/AI/RacingLine.cs`, `AiPilot.cs`; `Assets/_Convoy/Scripts/Presentation/SpeedometerHud.cs`, `VehicleDebugOverlay.cs` (target resolution only), `Convoy.Presentation.asmdef` (add `Convoy.AI`); `Assets/_Convoy/Tests/PlayMode/AiSyntheticTrackTests.cs` (+ add `Convoy.AI` to the PlayMode asmdef; if D-01b hasn't created it yet, create it per ADR-001).
- **A-01c:** `Assets/_Convoy/Editor/Integration/A01_SprintAiSetup.cs`, `Convoy.Editor.asmdef` (add `Convoy.AI`), `Assets/_Convoy/Tests/PlayMode/AiSprintCompletionTests.cs`. Orchestrator commits `Sprint_01.unity` and new assets/metas.

## Acceptance criteria
1. EditMode tests green (list below); 0 compile warnings added.
2. A-01b PlayMode: on a code-built flat ground with a generated closed "stadium" path (two 150 m straights, two 40 m-radius turns), one AI Jackal completes 3 laps in < 90 s sim time with 0 resets and never leaves the 4 m half-width band by more than 2 m.
3. A-01c PlayMode `AiSprintCompletionTests` (`[Category("Slow")]`, `Time.timeScale = 3`, fixed timestep unchanged): 10 runs of one rival from the start of `Sprint_01` → **≥ 9 reach the path end** within 180 s sim time; mean resets ≤ 2 per run.
4. Median AI (Balanced) time within ±15 % of the reference human time. Reference = Gabriel's best `Sprint_01` time recorded during F-03c/A-01c manual check (until recorded, provisional bound: ≤ 2:00, autopilot did 1:46 with 3 resets).
5. Manual: with 3 rivals + Gabriel, rivals visibly avoid each other and the wreck props in ≥ 1 of 2 runs without stopping; HUD always shows the player's car.
6. Perf: all `AiPilot.FixedUpdate` for 3 rivals < 0.1 ms total, 0 B GC (Profiler).

## Test plan
- **EditMode:** `PolylinePathTests` (length, PositionAt clamp/wrap, projection on a segment & window vs global agreement, curvature of straight = 0 and of a 40 m circle ≈ 1/40 ± 10 %); `SpeedPlannerTests` (straight → top speed; corner at 50 m ahead requires braking: result ≤ sqrt(v_c² + 2·a·50)); `LaneSelectorTests` (blocked centre → picks clearer side, rate-limited move, clamped to halfWidth); `StuckDetectorTests` (stuck → Reversing → RequestReset after attempts; off-path → RequestReset); `AiDriverBrainTests` (car left of path steers right; Held → Neutral; reset emitted once).
- **PlayMode:** AC 2 and 3 (orchestrator runs).
- **Manual:** AC 5.

## Risks
- `Road_N` ordering assumption — verified for indices 0–280 (positions progress along +Z); the script must log the path length (expected ≈ 1.1 km) and the orchestrator eyeballs the gizmo.
- Rivals ram the player at the start (spawned 8 m behind) → acceptable for M1; R-01 grid replaces it.
- Spherecasts hitting jump ramps → `walkableNormalUp` filter; tune if AI swerves at ramps.
- Without R-01, rivals' finishing isn't recorded by `SprintController` (single vehicle) — measured by the PlayMode test instead.
