# PR-01c — Integration: SprintCatalog asset in both sprints + editor save tools
- **Owner (agent):** ui-input-engineer (script) → orchestrator (runs)            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/PR-01.md · ADR-006
- **Blocked by:** PR-01b, LV-03c, RT-01d
- **Allowed files:** `Editor/Integration/PR01_ProgressionSetup.cs`, `Editor/SaveTools.cs` (new). Orchestrator commits `Config/Sprints/SprintCatalog.asset`, `Sprint_01.unity`, `Sprint_02.unity`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 3 manual: Silver unlocks Toxic Wastes, overlay loads it and back, unlock survives relaunch, backup-and-reset renames (never deletes)
  - Always: script idempotent (second run logs 0 changes), never deletes, exposes `ApplyToScene(string scenePath)` where it touches a scene (D-030); builder never runs Unity — orchestrator runs it and commits the generated diff.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
