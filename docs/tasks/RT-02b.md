# RT-02b — GhostRecorder (Race) + GhostRenderer (Presentation) + PlayMode test
- **Owner (agent):** vehicle-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/RT-02.md · ADR-006
- **Blocked by:** RT-02a, RT-01b (Save/Definition/LastOutcome), D-01b (PlayMode asmdef)
- **Allowed files:** `Scripts/Race/GhostRecorder.cs`, `Scripts/Presentation/GhostRenderer.cs` (new); `Tests/PlayMode/GhostRecordTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 5: file written on PB only, 25 Hz ± 2 samples, reload + Sample within 0.05 m
  2. 0 B GC in GhostRecorder.FixedUpdate / GhostRenderer.Update; ghost has no colliders
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
