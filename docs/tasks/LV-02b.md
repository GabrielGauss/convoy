# LV-02b — SurfaceTag + VehicleController/VehicleState surface edits + PlayMode tests
- **Owner (agent):** vehicle-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/LV-02.md · ADR-006
- **Blocked by:** LV-02a, D-01b, Q-01b (VehicleController.cs/VehicleState.cs chain, ADR-006 § 9)
- **Allowed files:** `Scripts/Vehicle/SurfaceTag.cs` (new); `VehicleController.cs`, `VehicleState.cs` (7 listed edits only); `Tests/PlayMode/SurfaceZoneTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 2: D-01b handling regression passes unchanged
  2. Spec AC 3 (a–d): neutral profile identical ± 0.02 s; mud 0.40–0.75× speed at 6 s; nitro ≥ 15 % faster on mud; state counters
  3. Spec AC 5: 0 B GC, ≤ +0.01 ms FixedUpdate
  4. VehicleController diff limited to the listed edits
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
