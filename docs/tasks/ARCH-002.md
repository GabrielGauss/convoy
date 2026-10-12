# ARCH-002 — Páramo Run alignment: ADR-006 + specs for the retry loop
- **Owner (agent):** architect (opus)            - **Human reviewer/owner:** Gabriel
- **Status:** building
- **Blocked by:** —
- **Allowed files:** `docs/adr/ADR-006-paramo-run.md`, `docs/specs/{RT-01,RT-02,UP-01,LV-01,LV-02,LV-03,PR-01}.md`, `docs/tasks/{same ids}.md`, small consistency edits to `docs/adr/ADR-00*.md` and `docs/specs/*.md` (note each edit in the report)
- **Acceptance criteria:**
  1. ADR-006: how D-021/D-022 map onto modules (Race owns run/splits/medals/uplink; Vehicle owns surface modifiers; Combat or Level owns breakables; Presentation owns ghost rendering + HUD), save data (local JSON/PlayerPrefs, no services), and which existing specs change.
  2. One spec per task above, same format as ARCH-001 specs, each a PR reviewable in < 20 min (split if needed), with blockedBy against batch-2 tasks where files overlap.
  3. Flag every scene/prefab step as orchestrator integration via editor script.
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
| 2026-10-11 | launch | — | D-021/D-022 accepted; architect launched |
| 2026-10-11 | spec | done | architect: ADR-006 (`docs/adr/ADR-006-paramo-run.md`, proposes D-023..D-030; DECISIONS.md untouched), specs UP-01/RT-01/RT-02/LV-01/LV-02/LV-03/PR-01 (`docs/specs/<ID>.md`), 7 umbrella task files → spec + 22 split tasks (UP-01a–c, RT-01a–d, RT-02a–c, LV-01a–c, LV-02a–c, LV-03a–c, PR-01a–c). Consistency edits: ADR-001 (`Convoy.Level`, `GhostRecorder` +40, `ApplyToScene` rule), ADR-003/ADR-005 (one note each), D-01/Q-01 (blocks LV-02b; uplink semantics), F-03/C-01/A-01 (`ApplyToScene` line). No code, scenes, prefabs or assets touched. |

## Open questions for humans (ARCH-002)
1. **Accept D-023..D-030** (ADR-006 § Proposed decisions) into `DECISIONS.md` — especially D-023 new `Convoy.Level` module (Lead Dev) and D-024 JSON save (Gabriel). UP-01a/RT-01a/RT-02a/LV-02a can start before; LV-01a needs D-023.
2. **Medal times (RT-01d):** provisional Red Canyon Monolith 1:32 / Gold 1:40 / Silver 1:52 / Bronze 2:15. Gabriel: record your best `Sprint_01` time *with* the uplink (after UP-01c) and set the author time; also closes ARCH-001 Q5.
3. **Uplink feel (D-025, ND + Gabriel):** 4 s hold, progress resets on exit, no speed limit, clock runs during the hold. Change any of these? (all data in `UplinkConfig`)
4. **Rammer gate rule (LV-01):** break on the speed component along the wall normal (rewards lining up) vs total speed. Recommendation: normal component.
5. **LV-03 design brief:** ND to write `docs/design/LV-03_ToxicWastes_brief.md` with the 11 inputs listed in the LV-03 spec; Gabriel approves. LV-03b/c are blocked on it.
6. **Sprint select only from the result screen (PR-01):** acceptable for M1, or do you want a hub/title scene (new task)?
7. **Restart by scene reload (D-029):** target ≤ 2.0 s to control; if it measures slower in RT-01d, approve an in-place reset task?
8. **Chasm fall (LV-03):** falling into the pit requires a manual R reset (no kill volume in M1). OK, or add a `RecoveryVolume` follow-up?
9. **Superseded generated roots (LV-03b):** re-running the generator disables + renames the old `[Generated]` root instead of deleting it (CLAUDE.md delete rule). A human deletes them periodically — OK?

## Suggested Páramo Run order (file conflicts serialised, ADR-006 § 9)
Wave A (parallel, after spec approval): UP-01a, RT-01a, RT-02a, LV-01a (needs D-023), LV-02a → then LV-03a, PR-01a.
Wave B (code, after batch-2 wave 2): UP-01b → RT-01b → RT-01c → PR-01b; RT-02b (after RT-01b); LV-01b; LV-02b (after D-01b and Q-01b).
Wave C (orchestrator, one at a time): after batch-2 wave 3 (F-03c → D-01c → A-01c → C-01b): UP-01c → RT-01d → RT-02c → LV-01c → LV-02c → (brief) LV-03b → LV-03c → PR-01c.
