# TR-01a — Pure terrain synthesis, TerrainLayout SO, WorldVariation selection
- **Owner (agent):** combat-engineer            - **Human reviewer/owner:** Gabriel
- **Status:** spec
- **Spec:** docs/specs/TR-01.md
- **Blocked by:** LV-01a (creates `Convoy.Level.asmdef` and the `Tests/EditMode/Level/` test asmdef)
- **Allowed files:** `Scripts/Level/Terrain/*.cs` (new: `TerrainLayout`, `TerrainLayoutData` + defs, `HeightField`, `TerrainSynthesis`, `ChasmJump`, `DeterministicRng`, `ParamoLayoutDefaults`, `WorldVariationConfig`, `WorldVariation`, `WorldVariationRules`); `Tests/EditMode/Level/TerrainSynthesisTests.cs`, `WorldVariationRulesTests.cs` (new) (paths under `Assets/_Convoy/` unless stated)
- **Acceptance criteria:**
  1. Spec AC 1: determinism by seed; pinned `DeterministicRng` values
  2. Spec AC 2: crater floor/plateau height, rim radius and height
  3. Spec AC 3: every default route ≤ `maxRouteSlopeDeg` + 0.5° longitudinal, ≤ 6° cross slope
  4. Spec AC 4: chasm gap width/depth; `MinClearSpeed` known value and default in [15, 32] m/s
  5. Spec AC 5–6: river bed, edge berm ≥ 30°, flat spawn pads, splat Σ = 1, route sampler spacing
  6. Spec AC 7: variation determinism, storm rate, open-shortcut bounds, mines off the safe lane, 0 B GC `Select`
  7. No `Terrain`/`TerrainData`/scene API in these files; `Convoy.Level` still references `Convoy.Core` only
  - Always: compiles with 0 errors; all existing tests stay green; no per-frame GC alloc in Update/FixedUpdate hot paths; no hand-edited `.unity`/`.prefab`/`.asset` YAML.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
