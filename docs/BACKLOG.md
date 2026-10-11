# CONVOY — Backlog: M1 "First Playable Greybox Race"

**M1 goal:** 2–3 people on one PC complete a full 3–5 min race in the Jackal against 3 AI rivals, using Pilot + Gunner + Engineer stations, with telemetry captured across ≥ 3 playtest sessions and a successful local Windows build.

**M1 exit questions (from 04 — First Milestone):** Is driving fun? Is the vehicle recoverable after mistakes? Is the camera readable? Can the race be completed reliably? Do non-pilot seats have meaningful decisions? → The next milestone depends on these answers, not on elapsed time.

Owners: **GV** = Gabriel · **DEV** = Lead Dev · **ND** = Narrative & Game Designer · **AI** = Claude drafts, human owner reviews.
Estimates are ideal hours for a Unity beginner; DEV estimates assume senior C# skills.

**Critical path:** F-00 → F-01 → V-01 → C-01 / T-01 → R-01 → K-01 → G-01 / M-01 → PT-02 crew playtest.

---

## Week 0–1 — Foundation

### F-00 Unity onboarding · All · ~8 h each · P0
- Everyone completes the onboarding path in `docs/onboarding/UNITY_ONBOARDING.md`.
- **AC:** each person can open the project, enter Play mode, create a prefab, edit a ScriptableObject, and make a branch + PR.

### F-01 Unity project in repo · DEV (+AI) · ~4 h · P0
- Create Unity 6.3 LTS URP project at repo root; Force Text, Visible Meta Files; Input System package enabled; folder layout per `TECH_DESIGN.md`; assembly definitions per system.
- **AC:** a fresh clone opens without errors on two different machines; `git status` is clean after opening; exact editor version recorded in README.
- **Deps:** F-00 (DEV only), repo on GitHub.

### F-02 Docs live · AI · ~2 h · P0 — *done in this commit*
- README, CLAUDE.md, GDD, TECH_DESIGN, DECISIONS, BACKLOG, playtest template.
- **AC:** team has read and signed off or amended every *Proposed* decision.

### F-03 Input + seat assignment · DEV · ~8 h · P0
- Input Actions asset with `Pilot` / `Gunner` / `Engineer` / `UI` maps; `PlayerInputManager` joins one device per player; `SeatId` assignment at runtime; input → command structs.
- **AC:** 1 keyboard+mouse and 2 gamepads map to three seats; disconnecting a pad does not crash; a debug label shows device→seat mapping.
- **Deps:** F-01.

---

## Week 1–2 — Feel (highest risk, start immediately)

### V-01 Jackal arcade controller · GV (+AI) · ~16 h · P0 — *code verified in Unity (21/21 EditMode tests, drives in Sandbox, 2026-10-10); merged (#3); PT-01 feel test pending*
- Rigidbody + 4 raycast wheels: spring/damper suspension, throttle, brake, reverse, steering with speed falloff, handbrake drift, air control, downforce. All tunables in `VehicleConfig` ScriptableObject.
- **AC:**
  - A first-time player finishes the sandbox track with ≤ 1 flip.
  - No physics explosions at 2× nominal top speed over rough terrain.
  - Changing a `VehicleConfig` value in Play mode takes effect immediately.
  - EditMode tests cover the pure math (suspension force, grip curve).
- **Deps:** F-01. Keyboard input is fine before F-03 lands.

### V-02 Recovery · GV (+AI) · ~4 h · P0 — *auto-right + manual reset + cooldown done (38/38 EditMode, verified in Play mode 2026-10-11); resets to last safe pose until T-01 provides checkpoints via `IRecoveryPointProvider`*
- Auto-right when upside down > 2 s; manual reset to last valid checkpoint, facing track direction.
- **AC:** back racing within 3 s; reset cannot skip checkpoints; cooldown prevents spam.
- **Deps:** V-01, T-01 (checkpoints).

### V-03 Debug overlay & live tuning · AI → GV review · ~4 h · P0 — *done: F1 overlay, self-bootstrapping, compiled out of release; measured 0.05–0.09 ms in Play mode (2026-10-11)*
- Overlay: speed, RPM-equivalent, slip angle, grounded wheel count, suspension compression, FPS. Toggle with F1.
- **AC:** overlay costs < 0.5 ms; off in release builds.
- **Deps:** V-01.

### V-04 Nitro (pilot boost) · GV (+AI) · ~4 h · P0 — *added by Gabriel 2026-10-11 (D-013); done*
- Hold Shift / RB: finite tank (~3 s), refills after 1 s delay (~10 s to full), +12 m/s top speed. Must release after running dry.
- **Measured:** 0→3 s full throttle 63 km/h without vs 107 km/h with nitro (Sandbox, Play mode).
- **Later (M-01):** boost generates heat and draws Engineer power; tank becomes the power budget.

### S-01 Sprint prototype: canyon run to the Monolith · AI → GV review · ~8 h · P0 — *added 2026-10-11 (D-014)*
- Scene `Sprint_01`: ~1.1 km canyon road, jumps, boulder field, rough section, narrow pass, nitro straight; 5 ordered checkpoints; finish between the Monolith pillars (Blender model).
- `Convoy.Race`: `SprintRun` (clock starts on first movement, ordered checkpoints, finish requires all, best time), `SprintController` (recovery to last checkpoint), `SprintHud` (timer, result screen, Enter/Start to restart).
- **AC:** start → finish completable in one run; reset returns to last checkpoint; timer stops at the Monolith; restart works.

### C-01 Pilot chase camera · GV · ~8 h · P0
- Cinemachine 3 follow camera; distance and FOV scale with speed; damping; terrain collision avoidance; look-back button.
- **AC:** no clipping through terrain on the test track; vehicle always readable on screen; no motion sickness reported in PT-01.
- **Deps:** V-01.

### T-01 Greybox track · ND · ~12 h · P0 — *note: Sandbox now has a placeholder loop (`Track_Loop`: barriers, nitro straight, jump, start gantry) for handling tests only; T-01 is still the real race track with routes + checkpoints*
- ProBuilder greybox: 3–5 min loop; start line; ≥ 6 checkpoints; one **fork** (short/risky vs. long/safe); one jump; one rough-terrain section; static obstacles.
- **AC:** two fork routes differ measurably in time *and* damage taken; AI spline paths authored for both routes.
- **Deps:** V-01 (needs a drivable car to tune scale).

### PT-01 Handling playtest · All · 2 h · P0 — **GATE** — *partial 2026-10-11: Gabriel 5/5 and wants to keep driving; Lead Dev + ND pending (`docs/playtests/PT-01_2026-10-11.md`)*
- Free driving in the sandbox, no objectives.
- **Pass:** all three want to keep driving. **Fail:** iterate V-01/C-01 before anything else.

---

## Week 3–4 — Race loop

### R-01 Race flow · DEV · ~8 h · P0
- States: `PreRace → Countdown → Racing → Finished → Results`; checkpoint validation in order; finish detection; results screen (position, time, damage taken); restart in < 5 s.
- **AC:** 10 consecutive races complete without soft-locks; race state is a plain C# class with EditMode tests.
- **Deps:** T-01.

### A-01 AI rivals · GV or DEV (+AI) · ~12 h · P0
- 3 rivals following spline paths via the same vehicle controller (AI produces `PilotCommand`, no cheating physics); obstacle avoidance; collidable and damageable; no rubber-banding in M1.
- **AC:** AI finishes the race in ≥ 90% of runs without getting stuck; lap time within ±15% of a competent human.
- **Deps:** R-01, V-01.

### D-01 Damage model · GV · ~6 h · P0
- Vehicle HP; impact damage from collisions above a speed threshold; low HP degrades top speed and grip (configurable curves).
- **AC:** heavy crash visibly changes handling; values in `DamageConfig`.
- **Deps:** V-01.

---

## Week 4–6 — Crew

### K-01 Station system · DEV · ~8 h · P0
- Pilot fixed; non-pilot players switch Gunner ↔ Engineer with ~1 s transition; works with 2 and 3 players; empty stations idle (AI assist is post-M1, see O-001).
- **AC:** switching never drops input or desyncs cameras; telemetry records time per station.
- **Deps:** F-03.

### G-01 Gunner turret · GV (+AI) · ~12 h · P0
- Turret with independent aim, roll-stabilized orbit camera, split-screen viewport; one weapon (e.g. autocannon), limited ammo, overheat.
- **AC:** hit a rival at 30 m while the car drifts; a hit visibly slows the rival (combat creates racing opportunity); camera stays level during drifts.
- **Deps:** K-01, A-01, C-01.

### M-01 Engineer power & heat · GV · ~10 h · P0
- Power distribution across Engine / Weapons / Cooling; boost generates heat; overheat → limp mode; heat shown on HUD.
- **AC:** after a race, playtesters can describe at least one power decision they made and why.
- **Deps:** K-01, D-01.

### M-02 Field repair · GV · ~4 h · P0
- Timed repair action at the Engineer station; repairing blocks other Engineer actions.
- **AC:** repairing is a real trade-off (observed in playtest).
- **Deps:** M-01, D-01.

### N-01 Tactician-lite · ND (design) + DEV · ~8 h · P1
- Engineer station shows full minimap, upcoming fork/hazards, rival positions; ping system places markers in the Pilot's view.
- **AC:** pilot takes the fork the Engineer called in ≥ 1 observed instance per session.
- **Deps:** K-01, T-01.

---

## Week 6–8 — Test, build, decide

### U-01 Minimal per-seat HUD · AI → GV review · ~6 h · P1 — *first slice done: analogue speedometer + digital km/h + nitro bar (IMGUI prototype, no assets); per-seat layout still to do*
- Speed, temperature, damage, ammo, position, checkpoint progress; per viewport.

### W-01 Wreck state (attrition test) · GV · ~4 h · P1
- At 0 HP the vehicle is wrecked; configurable: eliminated / respawn at checkpoint with time penalty.
- **AC:** toggle works; playtest records which option feels better (feeds O-002).
- **Deps:** D-01, R-01.

### Q-01 Telemetry & playtest kit · DEV (+AI) + ND · ~6 h · P0
- CSV per race: segment times, route taken, flips/resets, station occupancy & activity %, shots/hits, damage taken, overheat events. 5-question survey in `docs/playtests/TEMPLATE.md`.
- **AC:** one CSV per race in a known folder; ND can produce a playtest report in < 30 min.
- **Must exist before PT-02.**

### B-01 Local Windows build · DEV · ~3 h · P0
- Editor menu or script producing a versioned zip (`convoy-pt-NNN.zip`). No CI, no cloud.
- **AC:** build runs on a machine without Unity installed.

### PT-02..04 Crew playtests · All · 3 sessions · P0
- Full races with 2 and 3 players. Reports in `docs/playtests/`.
- **Exit:** answer the M1 exit questions; write the M2 decision entry.

---

## Explicitly out of M1
H-02 MULE · any networking · loadouts/customization · bosses (designed on paper by ND, not built) · final art/audio · menus beyond restart · terrain deformation · weather · progression/economy.

## Parallel design work (ND, no code)
- Paper crew test (week 1): one person drives an existing racing game, one calls routes from a printed map, one manages a "heat" timer. Note communication load.
- Race Royale proposal for O-002 (crews per match, elimination, "the prize").
- First boss encounter design on paper (what crew roles it stresses).
