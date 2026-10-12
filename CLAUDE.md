# CLAUDE.md — Rules for AI agents in this repo

Read `docs/DECISIONS.md`, `docs/TECH_DESIGN.md`, and the relevant task in `docs/BACKLOG.md` before changing anything.

## Current direction (2026-10-12) — read before any task
- **MVP = one ~10 km multi-level sprint track + 3 vehicle classes** (sidecar motorbike 1–2 seats, JACKAL 2–3, heavy 3), each with its own lines; mounted weapons, pre-race loadout, in-run upgrades; no persistent garage (D-035). Plan: `docs/BACKLOG_MVP.md`.
- **Track tech:** spline-mesh roads + ProBuilder; Unity Terrain only as backdrop (D-036, proposed). `Paramo_01` is a test map/backdrop; TR-01c merged (PR #12), no further open-terrain work.
- **Deferred to post-MVP:** cargo, robbery, bosses (D-032); the open-arena world shape of D-031.
- **Art:** ink-and-print caricature, junkyard 1930–1970 (D-033); still greybox until ART-01.
- **Open:** falling between layers (O-004) — build it as a config toggle, never hard-code one behaviour.
- **Track T01 layout:** Gabriel's hand-drawn map, two braided main lines + class connectors (D-037): `docs/tracks/T01/T01_HANDMAP.md`.
- Re-planning of open tasks for this direction: `docs/tasks/ARCH-003.md`.

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
