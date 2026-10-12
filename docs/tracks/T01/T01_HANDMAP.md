# T01 — Hand-drawn topology (Gabriel, 2026-10-12)

**Status:** Accepted as T01's topology (D-037). Design input for ARCH-003 / TK-00 / TK-01.
**Source images (this folder, 900 × 1600 px; coordinates below are in that pixel space, origin top-left):**

| File | Shows |
|---|---|
| `T01_map_overall.png` | Everything combined + legend |
| `T01_map_main-A.png` | Main A only (yellow) |
| `T01_map_main-B.png` | Main B only (red) |
| `T01_map_perk-sidecar.png` | Mains + sidecar (moto) connectors (blue) |
| `T01_map_perk-heavy.png` | Mains + heavy (truck) connectors (green) |
| `T01_map_perk-jackal.png` | Mains + JACKAL connectors (purple) |

## Structure
- **Two full main lines, A and B, braided from START (bottom) to FINISH (upper right, Monolith).** They split, cross and merge many times. A crew can change line mid-race.
- **Class perks are connectors between (or within) the mains**, not isolated per-segment forks:
  - **Sidecar (blue):** uses narrow spaces such as tunnels.
  - **Heavy (green):** breaks rocks / blockades.
  - **JACKAL (purple):** big full-speed jump.
- Not used from the earlier "FRACTURA" proposal: its layout. It was never committed; only its rule set (track design matrix) may be adopted later as a separate decision.

## Design rules for this map (Lead AI review, approved by Gabriel)

1. **A and B get distinct identities.**
   - **B (red) = primary:** wide, smooth, high speed, sweeping curves (big top-left loop).
   - **A (yellow) = secondary:** narrow, rough, technical (tight hairpins low and mid).
   - Time delta A vs B ≤ 2 s per ~60 s section. By eye, B's top loop is much longer than A's and the mid section is the reverse — **measure it** once digitized.
2. **Every A/B crossing is classified** (≈ 8–10 on the map: ~3 near the start, ~2 in the central S, 2–3 at the top):
   - **Merge:** shared road, crew contact (PvP).
   - **Grade-separated crossing:** bridge / tunnel — this is where the D-035 layers live.
   - Pick **3 signature over/under crossings**; turn the rest into clean merges/splits. No ambiguous flat intersections.
3. **Sidecar/heavy overlap top-left:** both connect B's loop (~100,575) to A (~220,640). Give it to one class, or split it: sidecar tunnel underneath, heavy breakable wall on a separate line.
4. **Central triple crossing (~330,770) = signature moment.** JACKAL diagonal, heavy diagonal and sidecar vertical meet at one point. Stack it:
   - sidecar → tunnel on the low layer,
   - heavy → breakable wall at ground level,
   - JACKAL → jump over the top.
   All three classes pass over/under each other at one readable spot.
5. **Last third is unbalanced.** Above ~y 500 only the JACKAL top jump exists. Add one sidecar and one heavy connector in that third, or move the JACKAL one earlier. No perks in the first ~250 px after the start is intended (pack spreads out).
6. **Long sidecar shortcut (635 → 830) saves too much.** It skips most of B's S-section. Shorten it or add cost (slow tunnel, rough floor, no nitro). Target: ≤ 2–3 s gain.
7. **Gates are physical, never locks:**
   - **Sidecar:** passage width (natural gate).
   - **Heavy:** wall stays broken once rammed, leaving debris that slows followers (reuses LV-01 Rammer gate, class-based break per ARCH-003).
   - **JACKAL:** speed threshold (`ChasmJump.MinClearSpeed`). Other classes may attempt; failing drops them to the lower layer → O-004 toggle.
8. **Open:** purple dot at ~(410,660) — ramp or drawing leftover? → ask Gabriel.

## Next step (owned by ARCH-003)
Digitize the map into a **node graph** — `T01_topology.md` + `T01_topology.json` in this folder:
- **Nodes:** splits, merges, crossings (each with level type: merge / over / under), perk entries/exits, START, FINISH.
- **Edges:** typed `A`, `B`, `sidecar`, `heavy`, `jackal`; polyline in map pixels + estimated length.
- **Scale:** whole route ≈ 10 km (D-035), fit within ~3 × 3 km around origin.
- **Checks:** A/B deltas, shortcut gains, mandatory jumps beatable at beginner speed, ≥ 1 min learnable section, segment split into four ~2.5 km segments (TK-01 = segment 1 from START).
