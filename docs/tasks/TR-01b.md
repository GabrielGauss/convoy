# TR-01b — Editor generator: Paramo_01 terrain, splat layers, placements
- **Owner (agent):** combat-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** qa
- **Spec:** docs/specs/TR-01.md
- **Blocked by:** TR-01a, F-03c (`Convoy.Editor.asmdef`), C-01b (camera `ApplyToScene`); soft: LV-01c (`RammerGate.prefab`), LV-02c (`MudPatch.prefab`) — placeholders until then, re-run after
- **Allowed files:** `Editor/Terrain/ParamoTerrainGenerator.cs`, `Editor/Terrain/TerrainTextureFactory.cs` (new); `Editor/Convoy.Editor.asmdef` (add `Convoy.Level` only if absent). Orchestrator runs the menu and commits `Scenes/Paramo_01.unity`, `Config/Level/Paramo_01_Layout.asset`, `Terrain/**`, metas (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 8: clean run creates scene + assets with 0 errors; second run idempotent (same object set under `Paramo_Generated`, same TerrainData GUID, identical heights); nothing outside the root deleted; ≤ 15 s
  2. `ApplyToScene(string scenePath)` public (D-030); menu `Convoy/Terrain/Generate Páramo_01`; refuses a newer `generatorVersion`
  3. Terrain draw settings and placements exactly as spec § TR-01b generator; missing prefabs → named placeholder + warning
  4. Player Jackal + camera present via F-03c/C-01b `ApplyToScene`; Gabriel can drive the three main routes (spec AC 10 driving items)
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-12 | build | done | 7c07e82 |
