# UP-01c — Integration: uplink zone + config in Sprint_01
- **Owner (agent):** ai-engineer (script) → orchestrator (runs)            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/UP-01.md · ADR-006
- **Blocked by:** UP-01b, F-03c, A-01c, C-01b (Sprint_01 owner chain, ADR-006 § 9)
- **Allowed files:** `Editor/Integration/UP01_UplinkSetup.cs` (new). Orchestrator commits `Sprint_01.unity`, `Config/Race/Monolith_UplinkConfig.asset`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 5 manual: pillars no longer end the run; 4 s hold finishes; leaving/reset empties the ring; CSV still `finished=1`
  2. `Finish` gate untouched
  - Always: script idempotent (second run logs 0 changes), never deletes, exposes `ApplyToScene(string scenePath)` where it touches a scene (D-030); builder never runs Unity — orchestrator runs it and commits the generated diff.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
