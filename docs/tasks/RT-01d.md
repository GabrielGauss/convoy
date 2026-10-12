# RT-01d — Integration: RedCanyon SprintDefinition wired into Sprint_01
- **Owner (agent):** ui-input-engineer (script) → orchestrator (runs)            - **Human reviewer/owner:** Gabriel
- **Status:** cancelled (D-031 — open terrain, no medals)
- **Spec:** docs/specs/RT-01.md · ADR-006
- **Blocked by:** RT-01c, UP-01c (Sprint_01 owner chain)
- **Allowed files:** `Editor/Integration/RT01_SprintDefinitionSetup.cs` (new). Orchestrator commits `Sprint_01.unity`, `Config/Sprints/RedCanyon_SprintDefinition.asset`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 6 manual: medal + save file on first finish, green/red deltas, quick restart ≤ 2.0 s to control, legacy PB imported
  - Always: script idempotent (second run logs 0 changes), never deletes, exposes `ApplyToScene(string scenePath)` where it touches a scene (D-030); builder never runs Unity — orchestrator runs it and commits the generated diff.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
