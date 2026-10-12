# TR-01c — World variation runtime, rival route export, route recovery
- **Owner (agent):** combat-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
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

## Orchestrator notes before build (from TR-01b review, 2026-10-12)
- Clamp the chasm cut to the shortcut end (today it slots ~700 m through ridges).
- Cave sits on the main river route: closing it must leave a detour (or never close the cave; vary another shortcut).
- Retune layout: chasm shortcut must be genuinely shorter than the canyon route; chasm/rammer shortcuts must rejoin their routes (now 39 m / 74 m off).
