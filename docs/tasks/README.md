# Agent task contracts

Every task an agent works on is one file here: `docs/tasks/<ID>.md`. Agents never talk to each other; the orchestrator moves work between them through these files.

## Pipeline (no gate is skipped)
`todo → spec → building → qa → perf → review → pr → done`

1. **Spec gate (architect):** objective, public interfaces, dependencies, measurable acceptance criteria, allowed files. Builders start only on an approved spec (approved by Gabriel during M1).
2. **Build:** builder works on `agent/<agent>/<task-id>` in an isolated worktree. Builders never touch Unity or Gabriel's PC.
3. **QA gate (qa-engineer):** tests written; orchestrator runs them in the single Unity Editor (MCP) on the `integration` branch. Gameplay features also get a PlayMode test or a manual checklist for Gabriel.
4. **Perf gate (perf-auditor):** hot paths only (physics, AI, combat, net). No per-frame GC alloc, no frame-time regression.
5. **Review gate (reviewer):** APPROVE / REQUEST_CHANGES with concrete reasons. Max 2 change cycles; the 3rd escalates to Gabriel.
6. **PR:** orchestrator opens it to `main` with summary, how to test in the Editor, and every gate result. **Merge = human only.**

## Hard rules
- One owner per `.unity` scene and prefab. Scenes are generated/edited by versioned editor scripts, never hand-edited YAML.
- Unity Editor = one locked resource, used only by the orchestrator.
- Commit `.meta` files; never `Library/`, `Temp/`, `Logs/`.
- One asmdef per system; dependencies only downward per the module ADR.
- Design data (vehicle, weapon, AI stats) in ScriptableObjects.
- Don't break the Jackal: physics changes need the handling regression test.
- Comment the *why* of Unity-specific patterns (team is new to Unity).
- PRs small: reviewable in < 20 min.

## Branches
- `main` — humans merge only.
- `integration` — orchestrator-only; Gabriel's PC stays on it during agent work (`git pull` once per batch). Holds reviewed work awaiting merge to `main`.
- `agent/<agent>/<task-id>` — one per task.

## Task file template
```
# <ID> — <title>
- **Owner (agent):**            - **Human reviewer/owner:**
- **Status:** todo
- **Spec:** docs/specs/<file>.md
- **Blocked by:**
- **Allowed files:**
- **Acceptance criteria:**
## Verdict log
| Date | Gate | Verdict | Notes |
|---|---|---|---|
```
