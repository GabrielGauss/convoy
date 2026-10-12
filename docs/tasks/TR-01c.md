# TR-01c — World variation runtime, rival route export, route recovery
- **Owner (agent):** combat-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** done (merged in PR #12); AC 4 Gabriel check pending. Further open-terrain work paused by D-035 — `Paramo_01` is a test map/backdrop
- **Spec:** docs/specs/TR-01.md
- **Blocked by:** TR-01b (`Paramo_01.unity` owner before it), A-01b (`RacingLine`, `AiPilot`), A-01c (rival setup to reuse)
- **Allowed files:** `Scripts/Level/Terrain/WorldVariationRunner.cs`, `Scripts/Presentation/ParamoAtmosphere.cs`, `Scripts/Race/RouteRecoveryPoints.cs`, `Scripts/Race/RouteRecoveryMath.cs` (new); `Scripts/Presentation/Convoy.Presentation.asmdef` (add `Convoy.Level`); `Editor/Integration/TR01_ParamoRuntimeSetup.cs` (+ `AiRivalSetup.cs` if extracted) (new); `Tests/EditMode/AI/ParamoRivalRouteTests.cs` + `Convoy.AI.Tests.EditMode.asmdef` (add `Convoy.Level`); `Tests/EditMode/RouteRecoveryMathTests.cs` (new). Orchestrator commits `Paramo_01.unity`, `Config/Level/Paramo_WorldVariation.asset`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 9: rival polylines are valid `PolylinePath`s (length ± 1 %, curvature ≤ 1/25 m⁻¹), waypoints 10–15 m apart; `RouteRecoveryMath` nearest point + unit tangent, 0 B GC
  2. `WorldVariationRunner` applies before the first `FixedUpdate`; `fixedSeed ≠ 0` reproduces the same world; closed shortcuts show their rockfall, main routes never blocked
  3. `ParamoAtmosphere` only writes `RenderSettings` fog (presentation reads state, D-007); Level/Race never reference Presentation
  4. Spec AC 10 (manual, Gabriel): rivals reach the crater, V-02 reset returns to a route, storm runs visibly differ, ≥ 60 fps at the three viewpoints
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-12 | build | done | 14d1261 |
| 2026-10-12 | review | APPROVE | AC 1–3 met: rival export tests (length ±1 %, curvature, 10–15 m) + recovery math 0 B GC; runner order -200/atmosphere -150 with EnsureApplied; Level→Core only, Race→Core/Vehicle, only Presentation writes fog (D-007). Orchestrator notes addressed (chasm clamp, shortcuts rejoin/shorter, cave never closable + SanitiseVariation). Documented deviations accepted (no `_layoutSeed`, lift from VehicleConfig, extra files needed for the layout fixes). Non-blocking follow-ups: move `RouteRecoveryPoints._backoffMetres` into an SO config; recovery points include closed shortcuts (reset may land before a rockfall); add warm-up/positive control to `RouteRecoveryMathTests.Queries_DoNotAllocate`. AC 4 still needs Gabriel: storm runs differ, chasm/river reset, ≥ 60 fps at 3 viewpoints. |
| 2026-10-12 | — | paused-after-merge | D-035: MVP track is a multi-level spline track; no further open-terrain runtime work until ARCH-003 says otherwise |

## Orchestrator notes before build (from TR-01b review, 2026-10-12)
- Clamp the chasm cut to the shortcut end (today it slots ~700 m through ridges).
- Cave sits on the main river route: closing it must leave a detour (or never close the cave; vary another shortcut).
- Retune layout: chasm shortcut must be genuinely shorter than the canyon route; chasm/rammer shortcuts must rejoin their routes (now 39 m / 74 m off).

## Build notes (level-engineer, 2026-10-12)
- **Run order in the Editor (orchestrator):** `Convoy/Terrain/Reset Páramo_01 Layout To Defaults` (the generator never overwrites the existing layout asset, so the layout fixes only arrive this way) → `Convoy/Terrain/Generate Páramo_01` → `Convoy/Integration/TR-01c Páramo Runtime Setup` (= `TR01_ParamoRuntimeSetup.ApplyToScene`). Re-run the setup after every generator run (it rebuilds `Paramo_Generated`). Commit `Paramo_01.unity`, `Paramo_01_Layout.asset`, `Paramo_WorldVariation.asset` (setup drops `cave` from `shortcutIds`), new `Config/AI/AI_*.asset`, metas.
- **Layout fixes:** chasm shortcut branches 42 % along canyon (520,-520)→(740,-400) and rejoins at the canyon's (420,40) node (545 vs 733 m); canyon loops via (740,-400),(700,-160); chasm 36 m across. Rammer shortcut (-300,-750)→(-628,-498) rejoins the desert (414 vs 560 m); desert bends via (-660,-690); gate 180 m along it. Chasm cut clamped to its shortcut's far end. Cave: no shortcutId, no blocker; `WorldVariationConfig.shortcutIds` default = chasm, rammer. Junction placement was tuned until AC 3 (drivability) passes — junctions sit on level canyon stretches.
- **Extra files beyond the allowed list:** `Scripts/Level/Terrain/RivalRouteExport.cs` (pure export shared by setup + tests), `Tests/EditMode/Level/WorldVariationRunnerTests.cs`, `Editor/Convoy.Editor.asmdef` (+`Convoy.AI`, `Convoy.Race`), `WorldVariationConfig.cs` default ids.
- **Interface deviations:** `WorldVariationRunner` has no `_layoutSeed` (mine positions are read from the slot objects, always consistent with the scene); adds `EnsureApplied()`/`Apply(seed)`/`Configure`. Rivals stop at the plateau edge (first waypoint within `crater.plateauRadius`) instead of driving into the Monolith. Recovery "+1 m up" comes from `VehicleConfig.recoveryLift` (1 m), not added twice.
- **Verified:** pure logic compiled and run with .NET 8 against Unity stubs: all Level/AI/recovery EditMode tests incl. Slow (67/67). Editor scripts compiled against stubs only.
- **Unverified (needs Unity):** real compile, editor wiring, scene save/idempotency, AiPilot rivals on terrain reaching the crater, runner/fog at play time, perf (AC 10 manual).
