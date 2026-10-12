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
