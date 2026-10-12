# LV-03c — Integration: Sprint_02 race wiring, Monolith/uplink, definition, Build Settings
- **Owner (agent):** level-builder (script) → orchestrator (runs)            - **Human reviewer/owner:** Narrative & Game Designer + Gabriel
- **Status:** cancelled (D-031 — open terrain, no medals)
- **Spec:** docs/specs/LV-03.md · ADR-006
- **Blocked by:** LV-03b, F-03c, C-01b, UP-01c, RT-01d (ApplyToScene entry points)
- **Allowed files:** `Editor/Integration/LV03_WireSprint02.cs` (new). Orchestrator commits `Sprint_02.unity`, `Config/Sprints/ToxicWastes_SprintDefinition.asset`, `ProjectSettings/EditorBuildSettings.asset`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 4: second run 0 changes; Sprint_01.unity no diff; Sprint_02 in Build Settings
  2. Spec AC 5–6 manual: full run, shortcuts work, checkpoints unskippable, ≤ 8 ms frame; ND acceptance recorded
  - Always: script idempotent (second run logs 0 changes), never deletes, exposes `ApplyToScene(string scenePath)` where it touches a scene (D-030); builder never runs Unity — orchestrator runs it and commits the generated diff.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
