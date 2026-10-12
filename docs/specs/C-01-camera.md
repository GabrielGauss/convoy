# Spec C-01 — Pilot chase camera (Cinemachine 3)

- **Human owner/reviewer:** Gabriel (D-010: camera, creative director) · **Builder agent:** vehicle-engineer · **Architecture:** ADR-001 (Presentation read-only), D-003, ADR-005 rule 10
- **Split:** **C-01a** package + pure maths + config + rig component (code) · **C-01b** integration script building the rig in Sandbox and Sprint_01 (executed by the orchestrator)
- **Gate:** C-01a cannot start until **D-015** (below) is accepted by Gabriel/Lead Dev — it adds a package.

## Objective
Replace the placeholder `SandboxChaseCamera` with a Cinemachine 3 pilot camera that keeps today's proven framing (follows travel direction while drifting, distance and FOV grow with speed, pulls in on terrain) while adding a look-back view, terrain de-occlusion, split-screen-ready output channels for G-01, and a **comfort-first default** (no roll, no shake, modest FOV change). Distance, height and FOV are tunable per config asset. The PT-01 nausea question is still unanswered, so comfort is verified explicitly in this task.

## Why Cinemachine (not just extending `SandboxChaseCamera`)
G-01 needs a second, roll-stabilised orbit camera, split-screen viewports and blends between station views (D-003). Cinemachine 3 provides blending, `CinemachineDeoccluder` (terrain collision), output channels for split-screen and impulse (if ever wanted) — rebuilding those by hand would cost more than learning it. Cost: one package, ~1 h learning for the team.

## Scope / out of scope
- In: package install, `ChaseCameraConfig`, `ChaseCameraMath`, `PilotCameraRig`, look-back camera, Sprint_01 + Sandbox wiring, comfort defaults.
- Out: Gunner orbit camera and split-screen viewports (G-01), Engineer map view (N-01), camera shake/impulse (off; possible later behind a toggle), settings menu, removing `SandboxChaseCamera` (disabled only; deletion needs approval).

## Public interfaces (`Convoy.Presentation`)
```csharp
namespace Convoy.Presentation
{
    [CreateAssetMenu(fileName = "ChaseCameraConfig", menuName = "Convoy/Chase Camera Config")]
    public sealed class ChaseCameraConfig : ScriptableObject
    {
        [Header("Framing")]
        [Min(1f)] public float baseDistance = 6.5f;          // = SandboxChaseCamera
        [Min(0f)] public float extraDistanceAtTopSpeed = 2.0f; // was 2.5 (slightly tighter = more readable)
        [Min(0f)] public float height = 2.4f;
        [Min(0f)] public float lookAhead = 4f;
        [Min(0f)] public float minDistance = 1.5f;           // de-occluder never pulls closer
        [Header("Field of view (comfort default)")]
        [Range(40f, 90f)] public float baseFov = 60f;
        [Range(0f, 20f)] public float extraFovAtTopSpeed = 8f;   // was 12; ≤ 68° total by default
        [Min(1f)] public float maxFovChangePerSecond = 20f;      // no FOV "pumping" on nitro
        [Header("Smoothing")]
        [Min(0.1f)] public float headingSharpness = 4f;      // = SandboxChaseCamera._rotationSharpness
        [Min(0.1f)] public float speedSmoothing = 2f;         // smoothing of the speed ratio driving distance/FOV
        [Min(0f)] public float minTravelSpeedForVelocityHeading = 3f;
        [Min(0f)] public float positionDamping = 0.2f;        // CinemachineFollow TrackerSettings.PositionDamping (all axes)
        [Header("Look back")]
        [Min(0f)] public float lookBackBlendSeconds = 0.15f;
        [Header("Comfort")]
        public bool allowShake = false;                        // reserved; nothing reads it in C-01
    }

    /// <summary>Pure maths, EditMode-tested.</summary>
    public static class ChaseCameraMath
    {
        public static float SpeedRatio(float forwardSpeed, float topSpeed);                 // clamp01(|v| / top), 0 if top ≤ 0
        public static float SmoothExp(float current, float target, float sharpness, float dt); // current + (target-current)(1 - e^(-k dt))
        public static float MoveTowardsRate(float current, float target, float maxPerSecond, float dt);
        public static float Distance(ChaseCameraConfig c, float smoothedRatio);              // base + extra × ratio
        public static float TargetFov(ChaseCameraConfig c, float smoothedRatio);             // base + extra × ratio
        /// <summary>Yaw (deg) the camera should face: velocity direction when moving forward faster than the threshold, else vehicle yaw.</summary>
        public static float DesiredYaw(float vehicleYaw, float velocityX, float velocityZ, float forwardSpeed, float threshold);
        public static float SmoothYaw(float currentYaw, float targetYaw, float sharpness, float dt); // shortest-path angle smoothing
    }

    /// <summary>Drives a Cinemachine pilot camera from VehicleState. Writes ONLY its own anchor transform and
    /// its own Cinemachine components — never the vehicle (D-007).</summary>
    [DisallowMultipleComponent]
    public sealed class PilotCameraRig : MonoBehaviour
    {
        [SerializeField] private VehicleController _target;
        [SerializeField] private ChaseCameraConfig _config;
        [SerializeField] private Transform _anchor;                     // follows vehicle position, yaw = smoothed travel heading, no pitch/roll
        [SerializeField] private Unity.Cinemachine.CinemachineCamera _chaseCamera;
        [SerializeField] private Unity.Cinemachine.CinemachineCamera _lookBackCamera;
        [SerializeField] private SeatInputRouter _input;                // optional; LookBack via GetViewInput(SeatId.Pilot)
        // LateUpdate (after the interpolated Rigidbody pose is applied):
        //   anchor.position = target.position; anchor.rotation = yaw-only(smoothed DesiredYaw)
        //   follow.FollowOffset = (0, height, -Distance); chase.Lens.FieldOfView = MoveTowardsRate(..., TargetFov)
        //   lookBack.Priority = LookBack held ? chase.Priority + 1 : chase.Priority - 1
    }
}
```
### Rig layout (built by C-01b)
- `Main Camera` + `CinemachineBrain` (Update Method = **Late Update**, default blend = Ease In Out 0.15 s). Unity background: the vehicle `Rigidbody` uses interpolation, so its visible pose is only final in `LateUpdate`; a brain updating earlier causes visible jitter.
- `PilotCameraAnchor` (empty, moved by `PilotCameraRig`).
- `PilotCamera`: `CinemachineCamera` (Tracking Target = anchor, Output Channel = Default/channel 1 — reserve channels 2/3 for Gunner/Engineer in G-01), `CinemachineFollow` (Binding Mode = **Lock To Target With World Up**: follows yaw only, never rolls or pitches with the car — the main anti-nausea measure), `CinemachineRotationComposer` (look-at offset `(0, 1, lookAhead)`), `CinemachineDeoccluder` (strategy Pull Camera Forward, camera radius 0.3, minimum distance from target = `minDistance`, ignore tag `Player`/vehicle layer as configured).
- `PilotCamera_LookBack`: same target, `CinemachineFollow` offset `(0, height, +baseDistance)` looking backward, lower priority.
- `SandboxChaseCamera` component disabled (not removed).

## Data assets
`Assets/_Convoy/Config/Camera/Pilot_ChaseCameraConfig.asset` with the defaults above (created by C-01b).

## Dependencies
- Blocked by: **D-015 accepted**; F-03b (`SeatViewInput.LookBack`; if F-03b is late, C-01a may ship with `_input` optional and look-back bound only after F-03c).
- C-01b blocked by C-01a and F-03c (both edit the same scenes → serialise scene ownership).
- Blocks: G-01 (Gunner camera reuses brain + channels), PT-02.

## Allowed files
- **C-01a:** `Packages/manifest.json` (add one line `"com.unity.cinemachine": "<version pinned in D-015>"`; Unity regenerates `packages-lock.json` — orchestrator commits it); `Assets/_Convoy/Scripts/Presentation/ChaseCameraConfig.cs`, `ChaseCameraMath.cs`, `PilotCameraRig.cs` (new); `Assets/_Convoy/Scripts/Presentation/Convoy.Presentation.asmdef` (add `Unity.Cinemachine`); `Assets/_Convoy/Scripts/Presentation/SandboxChaseCamera.cs` (doc comment "superseded by PilotCameraRig" only); `Assets/_Convoy/Tests/EditMode/ChaseCameraMathTests.cs`.
- **C-01b:** `Assets/_Convoy/Editor/Integration/C01_PilotCameraSetup.cs`, `Assets/_Convoy/Editor/Convoy.Editor.asmdef` (add `Unity.Cinemachine`). Orchestrator commits `Sandbox.unity`, `Sprint_01.unity`, the config asset and metas.

## C-01b integration script — `Convoy/Integration/C-01 Pilot Camera Setup`
*(ADR-006, D-030)* Expose `public static void ApplyToScene(string scenePath)`; the menu item calls it for the scenes listed below. LV-03c reuses it for `Sprint_02`.
For `Sandbox.unity` and `Sprint_01.unity`: create config asset if missing; add `CinemachineBrain` to `Main Camera` if missing; disable `SandboxChaseCamera`; create anchor, `PilotCamera`, `PilotCamera_LookBack` with the components above if missing; add `PilotCameraRig` wired to the scene's Jackal, config, cameras and `SeatInputRouter` (if present). Idempotent, never deletes, logs.

## Acceptance criteria
1. EditMode `ChaseCameraMathTests` green: SpeedRatio clamps; Distance/Fov at ratio 0/1 equal base / base+extra; `MoveTowardsRate` never exceeds rate × dt; `DesiredYaw` uses velocity only above threshold; `SmoothYaw` takes the short way across 359°→1°.
2. Default config: FOV never exceeds 68° and never changes faster than 20°/s (including nitro bursts) — asserted by a PlayMode probe or logged by the orchestrator over a full Sprint_01 autopilot/AI run.
3. Camera roll is 0° (± 0.01) at all times in a full Sprint_01 run including jumps and rollovers (orchestrator logs `transform.eulerAngles.z` max).
4. No terrain clipping: during a full Sprint_01 run (canyon, narrow pass), the camera's near plane never intersects canyon/rock colliders (`Physics.CheckSphere(cameraPos, 0.2)` returns false every frame in a logging PlayMode run); vehicle always within the viewport (`WorldToViewportPoint` of the car in [0.05, 0.95]).
5. Look-back: holding C / R3 shows the rear view within 0.2 s; releasing returns.
6. **Comfort gate (manual, closes the open PT-01 question):** Gabriel, Lead Dev and ND each drive Sprint_01 for 5 min with the default config and answer survey Q4 ("Could you always tell what was happening on screen? Any nausea?"). Pass = 0 nausea reports and all can read the road ahead. Answers recorded in the next playtest report.
7. Perf: `PilotCameraRig.LateUpdate` < 0.05 ms, 0 B GC; Cinemachine total < 0.3 ms in the Profiler.

## Test plan
EditMode (AC 1, mandatory) · PlayMode/logged run by orchestrator (AC 2–4) · manual checklist (AC 5–6): Play Sprint_01 → drive → jump → drift with handbrake (camera follows travel direction, no roll) → hold look-back → reset (camera recovers within 0.5 s, no long swoop) → nitro straight (FOV change gentle).

## Risks
- Cinemachine 3 API names differ from CM2 tutorials (`CinemachineCamera`, not `CinemachineVirtualCamera`) — builders must use CM3 names; reviewers new to Unity should expect this.
- Teleport on reset makes the camera swoop: on a large anchor jump (> 10 m in one frame) call `CinemachineCamera.OnTargetObjectWarped(anchor, delta)`.
- Deoccluder can "pop" in the narrow pass; tune radius/damping, don't add code.
- Comfort numbers are guesses until AC 6 is run.

## Proposed decision D-015 (needs acceptance before C-01a)
**D-015 — Add Cinemachine 3 for cameras** · Status: Proposed (architect) · Owner: Gabriel (camera) · Reviewer: Lead Dev
- Add `com.unity.cinemachine` **3.1.x** (free, Unity first-party), pinned to the exact version Package Manager offers as verified for Unity `6000.3.26f1`; record the pinned version in this entry when installing. Upgrades only between milestones (as D-001).
- Used for: pilot chase camera (C-01), gunner roll-stabilised orbit camera and split-screen output channels (G-01, D-003), view blends.
- Comfort defaults: camera never rolls (Lock To Target With World Up), FOV ≤ 68°, FOV rate ≤ 20°/s, no shake. Changes to these defaults need a playtest note.
- Why: building blending, de-occlusion and split-screen channels by hand would cost more than the package; `SandboxChaseCamera` stays (disabled) as a fallback until C-01 passes the comfort gate.
