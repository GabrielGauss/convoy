# CONVOY — Game Design Document (living)

**CONVOY is the code name** (D-034). The public title is decided before release.

Owner: Narrative & Game Designer · Creative director: Gabriel. Source vision: project brief "01 — Game Design & Vision".

## One-liner
A third-person, top-speed combat sprint: crews of 1–3 drive, shoot and upgrade a junkyard war-machine down a 10 km track that climbs, drops and crosses over itself, where each vehicle class has its own lines to the Monolith (D-035).

## Pillars
1. The crew wins, not the individual.
2. Every advantage has a trade-off (armor = weight, power = heat, ammo = space).
3. Terrain is an active opponent.
4. Combat creates racing opportunities rather than replacing racing.
5. Every crew station must make meaningful decisions.

## Format (D-035)
- **The sprint:** one hand-built ~10 km track (4–5 min), start to the **Monolith**; finish with the uplink (UP-01). Several branches, shortcuts and impossible jumps.
- **Multi-level:** three layers that cross over and under each other — high (bridges, ridges, ruin rooftops), mid (main road), low (canyon floor, tunnels, river). Falling off a layer: open (O-004).
- **Weapons, not Mario Kart:** no random item boxes, no rubber-banding. Mounted weapons with ammo and heat; chosen before the race and upgraded during it.
- **Equip & upgrade:** pre-race loadout from a fixed pool; scrap collected on the track buys upgrades at workshops between segments. No persistent garage in the MVP.
- **Modes:** crews vs AI rivals first (one rival per class); crews vs crews online after the netcode spike.
- **Post-MVP (D-032):** cargo delivery, robbery with counterplay, optional bosses, the 4-edge open arena (D-021).

## Crew stations (D-002)
| Station | Owns | Key decisions |
|---|---|---|
| Pilot | Driving, traction, drift, recovery | Line, risk, when to brake for the Gunner |
| Gunner | Turret, ammo, overheat, defenses | Target choice, ammo economy, suppress vs. finish |
| Engineer (Mechanic + Tactician) | Power split, heat, repairs, map/route intel, pings | Boost vs. weapons vs. cooling, repair now vs. later, route call |

Seats depend on the vehicle class (D-035): sidecar = Pilot + Gunner; JACKAL and heavy = all three. With fewer players than seats, the non-pilot switches stations (D-002).

## Vehicles (D-035) — the core of the track design
Every segment offers at least one line that favours each class; no class wins everywhere (target: competent runs within ±5%).

| Class | Crew | Strength | Weakness | Its own lines |
|---|---|---|---|---|
| **Sidecar motorbike** | 1–2 | Acceleration, tight turns, smallest target | Fragile, light ammo, mud hurts most, loses any ram | Pipes, girders, cliff ledges, gaps in fences and wrecks |
| **R-01 JACKAL** | 2–3 | Balanced top speed, drift, nitro | Medium armour, no ramming | Impossible jumps (speed-gated ramps), drift lines |
| **H-02 MULE** (heavy) | 3 | Armour, firepower, rams walls and rivals, partly ignores mud | Slow acceleration, worst jumper, too heavy for fragile bridges | Rammer walls, plough-through debris, ground-level brute lines |

- The sidecar has three wheels, so it reuses the raycast controller; its visual lean is presentation only.
- To test: the heavy collapses a fragile bridge and closes that shortcut for whoever is behind.

## Track build (D-035, D-036)
One track, done well, built in ~2.5 km segments: segment 1 with three layers (JACKAL only) → sidecar + heavy lines → weapons, loadout, upgrades → extend to 10 km. Each step gates on a playtest. Roads and bridges are spline meshes; Unity Terrain is backdrop only. `Paramo_01` remains a test map and backdrop.

## Visual identity (D-033)
**Ink-and-print caricature in a junkyard world.** Comic-book ink lines and halftone on aged paper, applied to absurd, proud war-machines built from 1930s–1970s scrap. M1 stays greybox; the look is proven in the ART-01 look spike after PT-01 passes.

**Reference:** Lorenzo Etherington's *Stranski* — for the treatment only. No copied designs, emblems, characters or names.

### Principles
1. **The vehicle IS the lore.** Each car is a gang, a culture and a joke told with a straight face. Its silhouette, decals and face say who drives it before any text does.
2. **Serious universe, absurd contrast.** Nobody in the world thinks it is funny; the humour comes from pride, contrast and over-engineering.
3. **Involuntary faces.** Grille, intakes and headlights line up into an expression (a growling jaw, grinning skull-eyes). This doubles as gameplay readability at 50 m+.
4. **Exaggerated mass.** Bonnet ~40% oversized vs. the cab, small cab set back, oversized wheels in bent fenders or fully exposed suspension.
5. **Exposed mechanics.** Engines, pipes, rivets and clamps on show; every part looks bolted on by someone.
6. **Gang identity.** Hand-painted numbers, crowned skulls, worn racing stripes, sponsor plates — kept in a decal layer of each vehicle's UV atlas.
7. **Silhouette first.** Vehicles read by their massive shape before their details (menus use a 3/4 view).

### Era & world
A world that recycles everything: vehicles from the 1930s to the 1970s mixed freely, analog tech, industrial machinery, sponsor culture, improvised engineering, hostile wasteland.

### Rendering (URP target)
| Layer | Technique | Watch-out |
|---|---|---|
| Base | Flat colour + decals | Keep textures simple; colour carries identity |
| Light | 2-tone toon ramp | Readable in both day and dust-storm fog |
| Shadow | Halftone / cross-hatch density driven by light | Patterns must not swim at speed; test in motion |
| Outline | Inverted hull, ink colour `#39302F`, slight width noise | Noise must not flicker frame to frame |
| Post | Paper grain | Subtle; never hides hazards |
| Impact | 2–4 frame impact flash + 3D comic onomatopoeia on crits | Per viewport (split-screen), can be turned off (photosensitivity) |

**Starting palette:** vintage pink `#C77E9A` · industrial blue `#7788AD` · signal orange `#E88B48` (accents, hazards) · worn metal `#D8C9AA` · ink `#39302F`.

### Asset pipeline
Human concept art for 2–3 vehicles → model → shader. Commissioned art needs explicit approval (cost). AI-generated final assets need a licence/provenance check (D-011). Blender-via-MCP models are placeholders (D-013).

### Open
- First concept-art brief: sidecar, JACKAL and MULE — faces, gangs and sponsors (ND + Gabriel).
- Factions and their visual codes (ties into Narrative).

## Risks (ranked)
1. **Crew workload** — non-pilot seats idle or overwhelmed. Test: station telemetry + "when were you bored?". Pivot signal: any seat idle > 40%.
2. **Handling** — arcade drift vs. instability on rough terrain; trade-offs must be felt. Test: PT-01 gate.
3. **Camera/nausea** — passengers in a drifting vehicle. Test: ask every playtest; roll-stabilized Gunner cam.
4. **Multiplayer** — shared physics vehicle + moving-platform aiming under latency; several crews + AI per session raise server cost. Mitigation: D-007 rules now, spike after M1.
5. **Scope** — 10 km of layered track + 3 vehicles + weapons + upgrades is large for ~45–50 h/week. M1 proves one loop only.

## Narrative
TBD (ND). Placeholder: world bible, factions/sponsors (each faction = a vehicle culture, D-033), why crews race to the Monolith, what each vehicle culture is (sidecar, JACKAL, heavy gangs).
