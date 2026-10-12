# CONVOY — Decision Log

One entry per decision. Never rewrite history: supersede with a new entry and mark the old one `Superseded by D-xxx`.
Status: **Accepted** (team agreed) · **Proposed** (Lead AI recommendation, awaiting team sign-off) · **Open** (needs input).

| ID | Decision | Status |
|---|---|---|
| D-001 | Engine: Unity 6.3 LTS + URP | Accepted |
| D-002 | Roles are stations, not players (3 seats) | Proposed |
| D-003 | Per-seat cameras; local tests in split-screen | Proposed |
| D-004 | Arcade raycast vehicle physics | Proposed |
| D-005 | Input: Unity Input System, gamepad + KB/M from day one | Accepted |
| D-006 | Game format: "Race Royale" (PvPvE attrition race) | Superseded by D-032 |
| D-007 | Netcode deferred; multiplayer-ready rules enforced now | Proposed |
| D-008 | Prototype race length 3–5 min | Proposed |
| D-009 | Git: GitHub private repo, trunk-based, LFS | Accepted |
| D-010 | Team ownership and capacity | Proposed |
| D-011 | AI-assisted development rules | Proposed |
| D-032 | Format: "Convoy Run" — cargo to the Monolith; replaces Race Royale and the 4-edge mega-arena | Accepted (format) / Open (numbers) |
| D-033 | Art direction: ink-and-print caricature, junkyard era 1930–1970 | Accepted |
| D-034 | Working title: MARAUDERS + subtitle (TBD) | Accepted (direction) / Open (subtitle, O-004) |
| O-001 | Crew-size balance (2 vs 3 players) | Open |
| O-002 | Crews per match / elimination & respawn rules | Open |
| O-003 | Online infrastructure & hosting cost | Open (post-M1) |
| O-004 | Title subtitle + trademark check for "Marauders" | Open |

---

## D-001 — Engine: Unity 6.3 LTS + URP
**Date:** 2026-10-10 · **Status:** Accepted
- **Decision:** Unity **6.3 LTS** (latest LTS, supported until Dec 2027). Pin the exact patch version (e.g. `6000.3.xf1`) on install and record it in `README.md`. Everyone uses the identical version. Universal Render Pipeline (URP).
- **Why not 6.0 LTS:** its LTS support ends October 2026 — this month.
- **Pinned:** `6000.3.26f1` (2026-10-10). Hub recommends 6.6 (Update release, short support window) — not used.
- **Upgrades:** only between milestones, by decision entry, never mid-sprint.
- **Context:** team has zero Unity experience → see `docs/onboarding/UNITY_ONBOARDING.md`.

## D-002 — Roles are stations, not players
**Date:** 2026-10-10 · **Status:** Proposed
- **Problem:** 4 roles (Pilot, Tactician, Gunner, Mechanic), 2–3 players.
- **Decision:** 3 stations per vehicle: **Pilot** (fixed seat), **Gunner**, **Engineer** (= Mechanic + Tactician: power/heat, repairs, map/route intel).
  - 3 players: one per station.
  - 2 players: the non-pilot switches between Gunner and Engineer (~1 s transition). Choosing *where to be* is itself the decision.
- **Rule:** the Engineer owns information the Pilot does not see (full map, upcoming hazards, rival positions) and communicates it via pings. Without information asymmetry the Tactician role is empty.
- **Revisit:** after the first crew playtest (telemetry: station idle time).

## D-003 — Per-seat cameras
**Date:** 2026-10-10 · **Status:** Proposed
- Pilot: third-person chase cam. Gunner: turret-anchored orbit cam, stabilized against vehicle roll (motion-sickness risk). Engineer: chase cam + map/systems overlay.
- Local multi-player tests use split-screen on one PC. This matches online play (each player has their own view).

## D-004 — Arcade raycast vehicle physics
**Date:** 2026-10-10 · **Status:** Proposed
- Custom raycast suspension on a single Rigidbody with an authored grip/drift model; **not** `WheelCollider`.
- **Why:** more predictable and tunable, recoverable after mistakes, easier to make deterministic-ish and to network later. WheelCollider tuning is notoriously hard for arcade feel.
- **Trade-off:** less "simulation" fidelity; we write and own more code (~1 week sandbox).
- All values live in ScriptableObjects (`VehicleConfig`), never hard-coded.

## D-005 — Input: gamepad + keyboard/mouse
**Date:** 2026-10-10 · **Status:** Accepted
- Unity **Input System** package (not the legacy Input Manager), one Input Actions asset, per-seat action maps (`Pilot`, `Gunner`, `Engineer`, `UI`).
- `PlayerInputManager` assigns one device per local player. One keyboard = one player (no shared keyboard in local tests).
- Gunner aim: mouse native; gamepad gets aim assist later (post-M1).
- Input produces **per-seat command structs**, never drives physics directly (required by D-007).

## D-006 — Game format: "Race Royale"
**Date:** 2026-10-10 · **Status:** Superseded by D-032 (2026-10-12)
- **Decision:** CONVOY is a PvPvE attrition race. Multiple player crews race through a hostile route populated by heavy AI: rival convoys, ambushes, environmental hazards, and **boss encounters**. Reaching the finish/prize is hard; a weak crew may never get there. Modes: crews vs crews (online) and crews vs AI (offline/co-op).
- **Implications:**
  1. **AI is a core system, not filler.** AI Systems moves from "basic opponents" to a primary pillar after M1 (encounter design, bosses, spawn director).
  2. **Attrition needs rules:** wreck state, elimination vs. respawn, catch-up vs. no catch-up. → O-002.
  3. **Larger online sessions** (several crews + many AI entities) strengthen the case for server-authoritative dedicated servers → hosting cost → O-003.
- **M1 impact:** M1 stays a single race vs. 3 AI rivals, but adds a configurable *wreck* state (P1) to test how attrition feels.

## D-007 — Netcode deferred; multiplayer-ready rules now
**Date:** 2026-10-10 · **Status:** Proposed
- No netcode package is chosen. Netcode for GameObjects remains a candidate only. A timeboxed networking spike happens **after M1**, ending in a decision entry.
- Enforced from day one (code review checklist):
  1. Simulation separated from presentation (camera, VFX, audio, HUD read state; never write it).
  2. All player actions are per-seat input commands (`PilotCommand`, `GunnerCommand`, `EngineerCommand`).
  3. No `LocalPlayer` singletons; seats are addressed by `SeatId`.
  4. Vehicle state is a serializable struct.
  5. Gameplay simulation runs in `FixedUpdate` at a fixed timestep.

## D-008 — Prototype race length 3–5 min
**Date:** 2026-10-10 · **Status:** Proposed
- Shipping target stays 10–15 min (subject to playtest). Prototypes use 3–5 min races so a playtest session fits 4–6 races.

## D-009 — Git workflow
**Date:** 2026-10-10 · **Status:** Accepted
- Private GitHub repo. Trunk-based: `main` is always playable.
- Branches: `feat/<task-id>-short-name`, `fix/...`, `docs/...`; live ≤ 3 days; merge by PR with 1 human review; squash merge.
- Git LFS for binaries (`.gitattributes`). Unity: Force Text serialization, Visible Meta Files, UnityYAMLMerge as merge driver.
- **Scene lock rule:** one owner per `.unity` scene at a time; announce in the team chat. Shared work goes into prefabs.
- Tag every playtest build: `pt-001`, `pt-002`, …
- **Cost watch:** GitHub's free LFS quota is limited. Greybox assets are tiny; check usage before importing any large asset packs. Buying LFS data packs requires explicit approval.

## D-010 — Team ownership and capacity
**Date:** 2026-10-10 · **Status:** Proposed
| Who | Hours/wk | Owns |
|---|---|---|
| Gabriel (code + design, creative director) | 15–20 | Vehicle feel (V-*), camera (C-*), crew stations (K-*, G-*, M-*), final creative calls |
| Lead Dev (senior developer, Gabriel's brother) | 10 | Architecture, input/seat system (F-03), race logic (R-01), code review gate, build script, post-M1 netcode spike |
| Narrative & Game Designer | 20 | `GDD.md` owner, track greybox (T-01), AI encounter design, playtest protocol & reports (Q-01), narrative bible |
| Lead AI (Claude) | — | Planning, docs, decision drafting, C# drafts + tests on branches, review support. Never merges/pushes without approval. |
- **Total:** ~45–50 h/week. **M1 estimate:** 6–8 weeks including Unity onboarding. Re-estimate after week 2.
- Lead Dev's 10 h are the scarcest resource → spend them on architecture and review, not on content.

## D-011 — AI-assisted development rules
**Date:** 2026-10-10 · **Status:** Proposed
See `CLAUDE.md`. Summary: AI drafts on branches; humans review, own, and merge. AI writes C# logic and tests, not `.unity`/`.prefab` YAML. No AI-generated final assets without license/provenance check.

## D-012 — Unity editor MCP: MCP for Unity (CoplayDev)
**Date:** 2026-10-11 · **Status:** Accepted (Gabriel)
- Add `com.coplaydev.unity-mcp` (MIT, free, local-only) pinned to **v10.3.0** so Claude can read the Console, inspect/edit scenes and scripts, and run tests without copy-paste.
- Editor-only tooling: not included in player builds. Upgrade only by changing the pinned tag in a PR.
- Chosen over the official Unity MCP (needs Unity AI subscription) and IvanMurzak/Unity-MCP (defaults to a cloud endpoint).
- D-011 still applies: Claude edits scenes through the editor only when asked; humans review and merge.

## D-013 — Pilot nitro, prototype HUD and first vehicle art
**Date:** 2026-10-11 · **Status:** Accepted (Gabriel, creative director)
- Nitro on the Pilot seat now (Shift/RB, finite tank). It is the seed of the GDD boost mechanic: M-01 will make boost cost Engineer power and generate heat. All tunables in `VehicleConfig`.
- Speedometer + nitro bar as the first U-01 slice, drawn with IMGUI from procedural textures (no assets, self-bootstrapping). To be rebuilt per-seat in UI Toolkit; keep the look.
- First Jackal model built in Blender via MCP (`SourceArt/Jackal.blend` → `Assets/_Convoy/Art/Vehicles/Jackal.fbx`), visual only: physics collider and wheel anchors unchanged. Placeholder quality, not final art (D-011 still applies to final assets).
- Sandbox gets a placeholder loop (`Track_Loop`) for handling tests; does not replace T-01.

## D-014 — Prototype race format: point-to-point sprint to the Monolith
**Date:** 2026-10-11 · **Status:** Proposed (Gabriel, creative director) — confirm with Narrative & Game Designer (GDD owner)
- After PT-01 (partial), Gabriel wants the first playable race to be a **sprint**: start → obstacles, ramps, rough ground, canyon → finish at a futuristic wasteland **Monolith** gate. The run completes when the vehicle passes between its pillars.
- Prototype scene `Sprint_01` (S-01). Ordered checkpoints feed V-02 recovery (`IRecoveryPointProvider`), so resets go to the last checkpoint and cannot skip any.
- **Impact to review:** D-006 "Race Royale" and T-01 assume a loop/route race. Open question for the GDD owner: is the Monolith sprint the core format, a mode, or a vertical-slice level? T-01's fork (short/risky vs long/safe) fits a sprint well.
- Sprint logic is a minimal slice of R-01 (clock, checkpoints, finish, result screen); R-01 still owns countdown, AI rivals, positions and the results loop.

## D-015..D-020 — Architecture batch (ARCH-001)
**Date:** 2026-10-11 · **Status:** Accepted (Gabriel delegated approval to Lead AI; D-020 direction to be confirmed by Lead Dev before the M2 spike)
- **D-015** Add Cinemachine 3 for C-01 (spec `docs/specs/C-01-camera.md`).
- **D-016** Module map & integration-script rule (`docs/adr/ADR-001-modules.md`): new `Convoy.Crew`, `Convoy.Combat`, `Convoy.AI`, `Convoy.Telemetry`, editor-only `Convoy.Editor`; `Convoy.Net` reserved for M2; scene/prefab changes only via re-runnable editor scripts run by the orchestrator.
- **D-017** Seat commands, device mapping, station contract (`ADR-002`).
- **D-018** Damage M1: one hull pool + handling multipliers, identical handling at full health (`ADR-003`).
- **D-019** AI driving: waypoint polylines + pure-pursuit through `IPilotCommandReceiver`, no NavMesh, no rubber-banding (`ADR-004`).
- **D-020** M2 netcode spike direction: NGO 2.x, host-authoritative, pilot-owned vehicle physics (`ADR-005`).

## D-021 — North star: PÁRAMO-NEXUS
**Date:** 2026-10-11 · **Status:** Accepted as long-term vision (Gabriel, creative director); amended by D-032 (the 4 km / 4-team arena is no longer the target) — Narrative & Game Designer to fold it into `GDD.md`
- Source: `docs/design/PARAMO-NEXUS_vision_es.md`. Four asymmetric biome approaches (Mountains N, Toxic Wastes S, Red Canyon E, Dune Valley W) converging on the **Monolith** at the centre; class-gated terrain (Rammer gates, Chasm + bridge terminal, spider cave); Payload uplink finale; X-crossings between convoys.
- **Not adopted for M1/M2 (scale and cost):** 4 km × 4 km terrain, 4 teams × 10 vehicles + 100k-HP payload, 15–20 min PvP matches, fog of war, fauna AI. These need netcode at scale and a far larger team. Revisit after M2 networking spike and PT data.
- **Paid assets named in the doc (RayFire, MicroSplat) are NOT approved** (CLAUDE.md cost rule). Use free Unity Terrain, simple fractured meshes, URP fog.

## D-022 — M1 = "Páramo Run": short biome sprints to the Monolith, built for retry and retention
**Date:** 2026-10-11 · **Status:** Accepted (Lead AI, delegated by Gabriel) — supersedes the open part of D-014
- Core loop: 1.5–3 min sprint per biome → reach the Monolith → **uplink**: hold inside the Monolith zone for a few seconds to finish (scaled-down Payload uplink). Instant restart.
- **Retry drivers (M1):** medal times (bronze/silver/gold + "Monolith" author time), live split times vs personal best at every checkpoint (green/red), ghost of your best run, discoverable shortcuts.
- **Class-gate mechanics adapted to one vehicle:** *Rammer gate* = breakable rock wall that only breaks above a speed threshold (≈80 km/h, reachable with nitro) and opens a shortcut; *Chasm* = jump ramp over a gap with a slower safe detour; *toxic mud* = surface zones cutting traction/speed 40%.
- **Retention drivers (M1→M2):** medals unlock the next biome and later vehicle classes (Rammer, Scout) — progression without economy/MTX.
- Biome order: **Red Canyon** (`Sprint_01`, exists) → **Toxic Wastes** (mud, shortest line) → Mountains / Dune Valley later.
- AI rivals (A-01) and crew stations (K-01/G-01/M-01) stay in M1 scope but serve this loop; T-01 becomes "second biome sprint with a fork" instead of a loop.

## D-023..D-030 — Páramo Run architecture (ADR-006)
**Date:** 2026-10-11 · **Status:** Accepted (Lead AI, delegated by Gabriel) — details and rationale in `docs/adr/ADR-006-paramo-run.md`
- **D-023** `Convoy.Level` module (Core only) for interactive level pieces (Rammer gate first).
- **D-024** Local save: versioned JSON `convoy_save.json` + binary ghosts in `persistentDataPath`; no online services.
- **D-025** Uplink finish: hold 4 s in the Monolith zone, leaving resets; scenes without a zone keep the instant finish.
- **D-026** Ghost: best run only, 25 Hz, ≤ 300 s, ≤ 512 KB, 0 B per-frame GC, no colliders.
- **D-027** Surface modifiers via ground-collider tags per wheel; defaults exactly 1; nitro unaffected by mud.
- **D-028** `SprintDefinition` per biome with medal times; unlocks derived from saved medals (Toxic Wastes needs Silver in Red Canyon).
- **D-029** Restart = scene reload (≤ 2 s target) + quick restart (hold Back 0.4 s).
- **D-030** Integration scripts expose `ApplyToScene(string scenePath)`.
- Placeholder medal times until Gabriel sets a reference run; ND to confirm uplink feel and write the LV-03 brief.

## D-031 — Open living terrain, not tracks; no medals
**Date:** 2026-10-11 · **Status:** Accepted (Gabriel, creative director) — supersedes D-022 (sprint/medal loop), D-024 ghosts, D-026, D-028; amends D-021 scale
- Gabriel: "el mundo está vivo, no es un track o una pista, es un terreno completo… sino es un juego de carreras con tiros." Reference maps: `PÁRAMO` technical map + PÁRAMO-NEXUS concept (2026-10-11).
- **M1 world = one open terrain slice**: free Unity Terrain, **2 km × 2 km** (¼ of the 4 km vision: playable by one small team, crossing ≈ 1.5–2.5 min), Monolith in a crater at the centre, player convoy spawns at one edge, **≥ 3 genuine routes** (canyon, toxic river crossing, open desert/minefield) plus class obstacles (Rammer rock, Chasm jump, mud). No checkpoints, no invisible walls; soft world edge only.
- **Retry/retention comes from the world, not from medals:** (1) rival AI convoys from other edges racing to the Monolith on their own routes (A-01 follows authored route polylines — the dashed lines in the map); (2) per-run world variation — dust storm on/off, which shortcut is open, mine layout; (3) route discovery; (4) win = first to hold the uplink (UP-01).
- **Cancelled:** RT-01 (splits + medals), RT-02 (ghost), PR-01 (medal unlocks), LV-03 (Sprint_02 biome sprint → replaced by **TR-01 Páramo terrain slice**). Kept: UP-01, LV-01, LV-02, A-01, crew tasks, D-029 restart. `Sprint_01` stays as a driving test course.
- **Still out of M1:** 4 teams × 10 vehicles, payload HP, fauna (spiders/beasts — M2 candidate), fog of war, paid assets.

## D-032 — Format: "Convoy Run" (replaces Race Royale and the 4-edge mega-arena)
**Date:** 2026-10-12 · **Status:** Accepted (Gabriel, creative director) — numbers Open; Narrative & Game Designer folds it into `GDD.md` · supersedes D-006, amends D-021, keeps D-031
- **Why:** Gabriel (2026-10-12): a massive arena is too much work. Source: external "Convoy Sprint" proposal, reviewed by Lead AI. The 4 km × 4 km, 4 teams × 10 vehicles PÁRAMO-NEXUS arena (D-021) stops being the target; it is a possible post-launch expansion only.
- **Map shape stays D-031:** one open terrain slice (`Paramo_01`, 2 km × 2 km). Its shape already fits: crews start at the edges, several genuine routes, everything reconverges at the **Monolith** (the Nexus).
- **Objective:** carry **cargo** to the Monolith and deliver it with the uplink (UP-01). *Payload* = the delivery goal; *cargo* = the resources carried. Losing cargo hurts, it never ends your run.
- **Cargo competes for space** with ammo and armour in the same vehicle (pillar 2). This gives JACKAL vs MULE a strategic meaning, not only a handling one.
- **Route identities** (proposal for ND to tune on `Paramo_01`): fast & exposed · resources (scrap) & slow · high risk & high reward. Rule: **no route is best at everything**. Forks are telegraphed early and the Engineer owns that intel (D-002, N-01).
- **World events:** bosses are optional and act as dilemmas (double reward, nobody is forced to fight). One natural event done well (dust storm, already in `WorldVariation`) before adding more.
- **Robbery with counterplay:** channelled, visible to the victim, interruptible, capped per attempt, resources only. No capture of vehicles or payload. The Gunner does it (harpoon/hook); AI rivals both steal and get stolen from.
- **Attrition (feeds O-002):** a wreck drops cargo as recoverable crates (W-01), it does not eliminate the crew.
- **Not adopted from the proposal:** 4 players per team, a Scavenger role, multi-vehicle convoys. The crew stays 2–3 players in one vehicle with 3 stations (D-002); its tasks go to the Gunner (steal) and Engineer (scrap intel).
- **Length:** prototypes keep D-008 (3–5 min); shipping target 8–15 min, set by playtest.
- **M1 impact:** none beyond D-031. Cargo, scrap, robbery, boss and event are M2, built and tested against AI first (`docs/BACKLOG_M2.md`, draft).

## D-033 — Art direction: ink-and-print caricature, junkyard era 1930–1970
**Date:** 2026-10-12 · **Status:** Accepted (Gabriel, creative director) — replaces the GDD era line (1970s–80s); details in `GDD.md` § Visual identity
- **Reference:** Lorenzo Etherington's *Stranski* illustrations, for **treatment only**. We do not copy his vehicle designs, emblem, characters or names.
- **Principles:** the vehicle IS the lore (each car = a gang and a culture) · the universe takes itself seriously, the absurd comes from contrast and tribal pride · involuntary faces (grille, intakes and headlights read as an expression) · exaggerated mass (huge bonnet, small cab set back, oversized wheels) · exposed mechanics (rivets, clamps, pipes) · gang decals (numbers, crowned skulls, worn stripes).
- **Era:** a junkyard world that recycles everything, 1930s–1970s vehicles mixed freely. Analog tech, sponsor culture and improvised engineering stay.
- **Rendering target (URP):** 2-tone toon ramp, halftone/hatch shadows, inverted-hull ink outlines, paper grain. Starting palette: vintage pink `#C77E9A`, industrial blue `#7788AD`, signal orange `#E88B48`, worn metal `#D8C9AA`, ink `#39302F`.
- **Hard constraints:** impact frames and flashes are per viewport and can be turned off (photosensitivity; split-screen) · screen-space patterns must stay stable in motion (tested at speed, not in stills) · no `Camera.main` (per-seat cameras, D-003) · presentation only reads simulation state (D-007).
- **Pipeline:** human concept art for 2–3 vehicles first, then model, then shader. Commissioned art is a paid cost and needs explicit approval. AI-generated final assets need a licence/provenance check (D-011). Blender-via-MCP models stay placeholders (D-013).
- **Not adopted from the proposal:** its C# VFX controller (`NetworkBehaviour`/`ClientRpc` breaks D-007; it mutates a shared material asset, uses `Camera.main`, `Instantiate`/`Destroy` per hit, string IDs and frame-rate-dependent timing) and a 10-vehicle roster generated by script.
- **Schedule:** ART-01 look spike after PT-01 passes. Does not block M1 (greybox).

## D-034 — Working title: MARAUDERS + subtitle (TBD)
**Date:** 2026-10-12 · **Status:** Accepted (direction, Gabriel) · subtitle Open (O-004)
- Gabriel (2026-10-12): rename the game to MARAUDERS plus a subtitle still to be invented.
- **Risk:** *Marauders* is already a published PC game (Team17 / Small Impact Games, Steam Early Access 2022). The subtitle must carry the identity, and a trademark/store search is required before any public use. A free first pass is fine; paid legal advice needs explicit approval.
- **Scope:** working title in docs and pitch material only. Repo name, `Convoy.*` namespaces, asmdefs and scene names do **not** change (renaming costs time and adds no gameplay value). Revisit before any release.
- **Subtitle candidates (Lead AI recommendation first):** MONOLITH · NEXUS RUN · PÁRAMO.

---

## Open questions
- **O-001 Crew-size balance:** a full 3-player crew covers all stations at once. Options: fixed crew size per match, or AI crew assist filling empty stations (fits "AI Systems: basic crew assistance"). Lead AI recommendation: **AI assist fills empty stations at reduced competence**. Decide after first crew playtest.
- **O-002 Match structure:** *partly answered by D-032 — the prize is delivered cargo; a wreck drops cargo instead of eliminating.* Still open: crews per online match? Elimination permanent or respawn-with-penalty? What is "the prize" mechanically (finish order, loot, extraction)? Owner: Narrative & Game Designer, proposal before end of M1.
- **O-003 Online infrastructure:** dedicated server hosting has recurring cost. No paid service without explicit approval. Evaluate in post-M1 netcode spike.
- **O-004 Title:** pick the subtitle for MARAUDERS (D-034) and run a trademark/store search before any public use. Owner: Gabriel.
