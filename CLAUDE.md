# CLAUDE.md — Rules for AI agents in this repo

Read `docs/DECISIONS.md`, `docs/TECH_DESIGN.md`, and the relevant task in `docs/BACKLOG.md` before changing anything.

## Hard limits (never, without explicit human approval in chat)
- Push to any remote, merge into `main`, create tags/releases, deploy.
- Incur any paid cost (LFS data packs, cloud services, asset purchases).
- Delete or overwrite assets, scenes, or docs you did not create in the current task.
- Commit secrets, keys, or credentials. Run destructive commands (`git reset --hard`, `git push --force`, `rm -rf`).
- Make irreversible architecture decisions — propose a `DECISIONS.md` entry instead.

## How to work
1. Before coding, state: objective, task ID, affected modules, risks, acceptance criteria.
2. Work on a branch `feat/<task-id>-short-name`. Small, single-purpose commits.
3. Write C# logic and tests. **Do not hand-edit `.unity`, `.prefab`, `.asset` YAML** — describe the editor wiring for a human to do.
4. All gameplay numbers go into ScriptableObject configs. No magic numbers in behaviour code.
5. After coding: compile, run EditMode/PlayMode tests, report changed files and real results. Never claim something works without verifying it; if you could not run Unity, say so.
6. Keep docs current: update `BACKLOG.md` status and add/supersede `DECISIONS.md` entries when relevant.

## Architecture rules (D-007, enforced in review)
- Simulation never references Presentation. Presentation reads state only.
- Player and AI actions are per-seat command structs (`PilotCommand`, `GunnerCommand`, `EngineerCommand`).
- No `LocalPlayer` singletons — use `SeatId`.
- Vehicle state is a serializable struct. Gameplay runs in `FixedUpdate`.
- One asmdef per module; no cyclic references.

## Code style
- C# naming: `PascalCase` types/methods, `_camelCase` private fields, `[SerializeField] private` over public fields.
- Namespaces match modules: `Convoy.Vehicle`, `Convoy.Race`, …
- Prefer plain C# classes for logic (testable) and thin `MonoBehaviour` adapters.
- No new packages without a decision entry.

## Out of scope until a decision says otherwise
Networking, matchmaking, progression, economy, open/procedural worlds, skill trees, final art.
