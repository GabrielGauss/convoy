# LV-03b — Integration: Sprint_02 geometry generator (terrain, roads, fork, chasm, mud, gate)
- **Owner (agent):** level-builder (script) → orchestrator (runs)            - **Human reviewer/owner:** Narrative & Game Designer + Gabriel
- **Status:** spec
- **Spec:** docs/specs/LV-03.md · ADR-006
- **Blocked by:** LV-03a, **ND design brief approved by Gabriel** (`docs/design/LV-03_ToxicWastes_brief.md`), LV-01c (RammerGate.prefab), LV-02c (MudPatch.prefab)
- **Allowed files:** `Editor/Integration/LV03_BuildSprint02Geometry.cs`, `ToxicWastesLayoutDefaults.cs` (new). Orchestrator commits `Scenes/Sprint_02.unity`, `Scenes/Sprint_02/**`, `Config/Level/ToxicWastes_Layout.asset`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 3: validator gate aborts on errors; re-run supersedes (disables, never deletes) the old root; dry run changes nothing
  2. Numbers only from the approved brief
  - Always: script idempotent (second run logs 0 changes), never deletes, exposes `ApplyToScene(string scenePath)` where it touches a scene (D-030); builder never runs Unity — orchestrator runs it and commits the generated diff.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | spec | written | architect (ARCH-002); awaiting Gabriel's spec approval |
