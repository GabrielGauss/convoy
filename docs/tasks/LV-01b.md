# LV-01b — RammerGate runtime component + PlayMode test
- **Owner (agent):** combat-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/LV-01.md · ADR-006
- **Blocked by:** LV-01a, D-01b (PlayMode asmdef), UP-01b (same asmdef edit, serialise)
- **Allowed files:** `Scripts/Level/RammerGate.cs` (new); `Tests/PlayMode/RammerGateTests.cs` (new); `Tests/PlayMode/Convoy.Tests.PlayMode.asmdef` (add `Convoy.Level` only) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 3 (a–d): breaks at 25 m/s head-on with 0.85 speed retention; holds at 15 m/s and at 60°; debris freezes
  2. Spec AC 5: 0 B GC in trigger callbacks
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
