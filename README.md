# CONVOY

Cooperative vehicular combat racing — a third-person **Race Royale** for crews of 2–3 players on PC. Retro-industrial retrofuturism, 1970s–80s.

**Current milestone:** M1 — First Playable Greybox Race (offline, local multiplayer). See [`docs/BACKLOG.md`](docs/BACKLOG.md).

## Requirements
- **Unity 6.3 LTS** — exact version: `6000.3.____` *(fill in after F-01; everyone must use this exact version)*
- Git + **Git LFS** (`git lfs install` once per machine)
- Windows PC; Xbox/PlayStation gamepad recommended for local multi-seat testing

## Get playing (target: < 10 min)
```bash
git lfs install
git clone <repo-url> convoy
cd convoy
```
1. Unity Hub → **Add** → select the `convoy` folder (after F-01 creates the project).
2. Open `Assets/_Convoy/Scenes/Sandbox.unity` → Play.
3. One-time Git setup for Unity scene merges — see [`docs/onboarding/UNITY_ONBOARDING.md`](docs/onboarding/UNITY_ONBOARDING.md#git-setup).

## Docs
| File | What |
|---|---|
| [`docs/GDD.md`](docs/GDD.md) | Game design (living) |
| [`docs/TECH_DESIGN.md`](docs/TECH_DESIGN.md) | Architecture, modules, rules |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | Every decision, with status |
| [`docs/BACKLOG.md`](docs/BACKLOG.md) | M1 tasks, acceptance criteria, dependencies |
| [`docs/playtests/`](docs/playtests/) | Playtest template and reports |
| [`CLAUDE.md`](CLAUDE.md) | Rules for AI-assisted work in this repo |

## Workflow (D-009)
- `main` is always playable. Work on `feat/<task-id>-name` branches, ≤ 3 days, PR with 1 review, squash merge.
- **Scene lock:** announce before editing any `.unity` scene; one owner at a time.
- Playtest builds are tagged `pt-NNN`.

## Team
Gabriel (code + design, creative director) · Lead Dev (architecture, review) · Narrative & Game Designer · Claude (Lead AI, supervised).
