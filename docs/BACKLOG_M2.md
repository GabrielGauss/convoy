# CONVOY — Backlog: M2 "Convoy Run vs AI" (DRAFT)

> **Status: draft for team review (2026-10-12).** Not started. M2 begins only when the M1 exit questions get positive answers (PT-02..04). Re-estimate everything at M1 exit.
> Sources: D-032 (format), D-033 (art), D-020 / ADR-005 (netcode spike), O-002.

**M2 goal:** a 2–3 person crew in one vehicle crosses `Paramo_01` carrying cargo against 3 AI rival crews that steal and can be stolen from, with one optional boss and one real natural event, and delivers at the Monolith uplink. In parallel: the LAN netcode spike decides the online path.

**M2 exit questions:** Does choosing a route matter (crews debate, choices spread across routes)? Does robbery create stories (cargo changes hands and can be won back)? Is the boss worth the detour? Does losing feel fair (players can name the decision that cost them)? Does the LAN spike keep the Pilot's feel?

Owners: **GV** = Gabriel · **DEV** = Lead Dev · **ND** = Narrative & Game Designer · **AI** = Claude drafts, human owner reviews. Estimates are rough ideal hours.

**Critical path:** CG-01 → CG-02 → RB-01 → AI-02 → RD-01 → PT-05.

---

## Cargo & resources

### CG-01 Cargo & shared capacity · DEV (+AI) · ~8 h · P0
- `CargoConfig` SO. Cargo, ammo and armour plates share one vehicle capacity; carried mass feeds the existing handling multipliers (D-018). Cargo delivered at the uplink is the score.
- **AC:** a full cargo hold makes 0–100 km/h measurably slower (number set in the SO and logged); capacity rules are plain C# with EditMode tests; no gameplay numbers in code.
- **Deps:** UP-01, D-01.

### CG-02 Cargo drop, pickup & wreck · AI → GV review · ~6 h · P0
- Heavy hits drop part of the cargo as crates; a wreck (W-01) drops it all and respawns the crew instead of eliminating it. Any crew can pick crates up; crates despawn after a timer.
- **AC:** dropped cargo is always recoverable for ≥ N s (SO); a wreck never ends the run; telemetry logs drops and pickups.
- **Deps:** CG-01, W-01.

### SC-01 Scrap & mechanic stop · GV (+AI) · ~8 h · P0
- Scrap pickups on the resource route; the Engineer spends scrap to repair (M-02) or refill ammo; one mechanic stop on the map.
- **AC:** every scrap use is one visible decision (repair vs ammo); playtesters can name one scrap decision per run.
- **Deps:** CG-01, M-02.

## Conflict

### RB-01 Robbery with counterplay · GV (+AI) · ~12 h · P0
- Gunner harpoon/hook: stay within range and channel for N s to pull cargo from a rival. The victim sees and hears it; damage or distance interrupts it; each attempt is capped; cooldown after.
- **AC:** no single steal takes more than X% of a hold (SO); every steal can be interrupted; in playtest, at least one interrupted steal per session.
- **Deps:** CG-01, G-01.

### AI-02 Rival crews race, rob and defend · DEV or GV (+AI) · ~12 h · P0
- AI rivals pick between racing, robbing and defending, using the same seat commands as players (no cheating physics, D-019). They carry cargo and are valid targets.
- **AC:** across 10 runs, AI attempts steals and gets robbed; AI still delivers in ≥ 80% of runs without getting stuck.
- **Deps:** A-01, RB-01, CG-02.

## World

### RD-01 Route identity pass on `Paramo_01` · ND (+GV) · ~10 h · P0
- Assign each route a role (fast & exposed · resources & slow · high risk & reward); tune so no route dominates; telegraph forks early with signage and Engineer intel (N-01).
- **AC:** over PT-05..07, no route is chosen in more than 60% of runs, and each route wins at least once.
- **Deps:** TR-01, SC-01, N-01.

### EV-01 Dust storm as a real event · AI → GV review · ~6 h · P1
- Upgrade the existing `WorldVariation` dust storm: telegraphed arrival, lower visibility and grip, hides convoys, uncovers scrap.
- **AC:** crews change plan because of the storm in ≥ 1 observed instance per session.
- **Deps:** TR-01.

### BS-01 One optional boss · ND (design) + GV (+AI) · ~16 h · P1
- One boss (e.g. armoured excavator) blocks a route and guards a reward. Fighting it costs time; ignoring it is allowed; breaking a part opens a path.
- **AC:** the boss creates a dilemma (some crews fight, some skip) in playtest; it never blocks every route.
- **Deps:** ND's paper boss design (M1 parallel work), G-01, AI-02.

## Vehicles & art

### H-02 MULE greybox · GV (+AI) · ~8 h · P1
- Second vehicle: heavy, more capacity, slower. `VehicleConfig` + placeholder model. Without it the cargo trade-off has only one side.
- **AC:** with the same cargo, MULE and JACKAL win different routes in playtest.
- **Deps:** CG-01.

### ART-01 Look spike · GV (+AI) · ~30 h · P1 — *may start at the end of M1 once PT-01 passes*
- JACKAL with toon ramp, halftone/hatch shadows, inverted-hull outline, paper grain; impact flash per viewport with an off switch (D-033).
- **AC:** the vehicle reads by silhouette first; patterns do not swim at 150 km/h; no nausea in a playtest; works in split-screen; no per-frame GC in the effect path.
- **Deps:** PT-01 pass, C-01.

### ART-02 Concept art: 2–3 vehicles · ND + GV · cost TBD · P1
- Brief: JACKAL and MULE faces, gangs and sponsors. Human artist. **Paid → explicit approval first.**
- **AC:** signed-off sheets that ART-01 and modelling can follow; licence on file.

## Network & data

### NET-01 LAN netcode spike · DEV · 2 weeks timebox · P0
- As ADR-005 / D-020, with the map moved from `Sprint_01` to `Paramo_01`. Ends in a decision entry (NGO or fallback).
- **AC:** ADR-005 exit criteria met and measured; no paid services (O-003).
- **Deps:** M1 done.

### Q-02 Telemetry for Convoy Run · DEV (+AI) · ~4 h · P0
- Add per run: route chosen, cargo carried/delivered/lost/stolen, steals attempted/interrupted, scrap earned/spent, boss engaged y/n, storm on/off.
- **AC:** ND builds a playtest report in < 30 min; survey question 6 (fair loss) is in `docs/playtests/TEMPLATE.md`.
- **Deps:** Q-01.

### PT-05..07 Convoy Run playtests · All · 3 sessions · P0
- 2 and 3 players vs 3 AI crews on `Paramo_01`. Reports in `docs/playtests/`.
- **Exit:** answer the M2 exit questions; write the M3 decision entry.

---

## Load
~140 h of build work + a 2-week Lead Dev spike → roughly **5–6 weeks** at ~45–50 h/week, if M1 lands as planned.

## Explicitly out of M2
Online PvP and matchmaking · the 4-edge 4 km arena (D-021) · payload or vehicle capture · persistent economy or progression · more than 2 vehicles · final art beyond ART-01 · fauna · fog of war.
