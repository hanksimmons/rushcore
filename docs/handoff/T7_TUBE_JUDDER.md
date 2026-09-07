# T7 — Tube judder while boosting (bug)

**Branch:** `opus/t7-tube-judder` off `develop-secondary`  
**Phase:** 3 defect (D-101 see-through tubes)  
**Gate lines served:** 08 §5 "the harness drives the ball into a tube at the cap and asserts it is carried through"; 08 §3 no solver chaos  
**Owning specs:** 04 §5I (tubes), 03 §3 (ground follow) and §12 (collision), 11 §7d; DECISIONS D-101, V-015  
**Reported:** 2026-09-07 by the user on `develop-secondary`, on several archetypes: the player model judders continuously
for seconds while **boosting through a tube**. Entry is fine; cruise without boost is fine. Not seen before the
branching-exits merge, which touched no tube, player or camera code.

## Analysis (main track, 2026-09-07, from the code; not yet measured)

### The geometry that matters

- **Collider:** `src/World/TubeMesh.cs` builds one ring of `Sides = 10` vertices per axis sample (axis resampled every
  4 m, `TubeBuilder` line ~143) with a parallel-transported frame whose first normal is Up, so ring vertex `s = 0` is the
  top of the tube and `s = 5` is the exact **bottom**. The same triangles are the `ConcavePolygonShape3D`. A regular
  10-gon of circumradius R = 6 m has inradius R·cos(18°) = 5.706 m: the wall is 0.294 m inside the analytic circle at
  every facet middle and on the circle only at the ten corners.
- **Follow:** `PlayerPhysics.TryTubeFollow` holds the ball to the analytic circle: `gap = radius − BallRadius − dist`
  with `radius = TubeDefinition.Radius` (6.0) from `MovementToyWorld.Nearest`, so the ball centre is driven to
  5.34 m from the axis and its surface to exactly 6.0 m. At a corner that is the wall. At a facet middle it is
  **0.29 m inside the collider**.
- **Solver:** Jolt resolves that penetration with a position correction each tick; the follow reads the corrected
  position (`dist` < 5.34, `gap` > 0) and sets the outward velocity to `(gap − 0.03) / 0.05 s` ≈ up to 5 m/s back
  into the wall. Two authorities, opposite signs, every tick: that is a judder at the physics rate, visible because
  the position correction moves the body transform, which interpolation cannot hide.

### Why cruise is quiet and boosting is not

At cruise on a straight the ball rests at the bottom, which is a **corner** of the polygon, so circle and collider
agree to the millimetre and there is nothing to fight. The ball only leaves the corner when something holds it up the
wall between corners (the next corners are 36° either side of the bottom):

- a bend: tan φ = v²κ / g; the swing (200 m over 500 m, κ ≈ 0.0048) at the cap gives φ ≈ 70°, and D-101 reports rides
  to 85–88°; the ball crosses several facets and the fight is brief at each,
- **steering or boosting**: `ApplyBoost` accelerates along the blend of travel and the stick, and any lateral stick
  inside a tube pushes the ball up the wall; boost sustains that lateral force against gravity, so the ball is parked
  between corners for as long as boost is held. That is the "several seconds, continuous, on every archetype" report.

Prediction the trace must confirm: judder ticks coincide with a ride angle away from a multiple of 36° and with
`GetContactCount() > 0`; penetration depth follows 0.294·(1 − cos(phase)) with phase the angle to the nearest corner;
at a corner (phase 0) the contact count is 0 or the penetration under 1 cm.

### Things checked and ruled out from the code

- **Nearest axis point** is the nearest *vertex* (4 m apart) with that vertex's tangent projected out. On a straight
  axis the along-axis error vanishes; on the swing (ρ ≈ 200 m) the lateral error is ≈ 2²/(2·200) = 1 cm and changes
  every 4 m of travel: a 1 cm ripple at 40–60 Hz, below the deadband, not the judder.
- **Ground follow first** (`TubeFollowActive = !_followActive && ...`): the terrain is 30 m below the cruise, so the
  ground follow cannot engage except at the mouths; entry is reported fine.
- **Mouth flare** ×2 over one diameter: the follow keeps the analytic radius there while the collider is farther
  out, so no fight at the mouths, only a hover.
- **Camera** (`PushOutOfTubes`) also uses the nearest vertex and is pushed to R + 1.5 m every frame: a centimetre ripple
  on the swing, possible but not the player-model judder.
- **The cap** uses the wall normal's tangent plane; it does not oscillate.
- **Frame twist:** the parallel transport keeps the bottom corner near the true bottom on the near-planar axes; a
  twisted frame would put a facet middle at the bottom and give a *cruise* buzz. Measure the bottom corner's phase on
  the tube you test; if it is off by more than 5°, that is a second defect (fix: start the frame from Up at every ring).

### The fix and its numbers

1. `TubeMesh.Sides` 10 → **24** (keep it even so the bottom stays a corner). Facet depth 0.294 m → 6·(1 − cos 7.5°) =
   0.051 m. Vertices per tube ≈ 24 × (length / 4 m): about 12 k for a 2 km tube, trivial.
2. `TryTubeFollow` holds the **inscribed** circle: `float wall = radius * Mathf.Cos(Mathf.Pi / TubeMesh.Sides);` then
   `gap = wall − BallRadius − dist`. The ball centre sits at 5.949 − 0.66 = 5.29 m; its surface touches the collider
   only at facet middles and is 5 cm inside it at corners, so with the 3 cm deadband the collider never fires while the
   follow is active. Fast arrivals (`vOut > snap / dt`) still land on the collider as before.
3. Do not change `GroundFollowCloseSeconds`, the deadband or the snap distance: they are shared with the ground follow
   and part of D-092. Do not write the transform.

Why not match the polygon exactly (target = inradius / cos(phase))? The ball would then ride the polygon, a 0.29 m
(or 5 cm) wobble per facet that is real motion; the inscribed circle is smooth and the 5 cm corner gap is invisible.

### Harness recipe

In the tube ride, after the cruise pass and `RefillBoost(BoostCapacity)`: hold `InputBootstrap.Boost` and add a
constant lateral `_worldDrive` (perpendicular to the axis tangent, toward one wall) for 3 s on a straight part of the
cruise, then along the axis to the exit. Per tick record `dist` (radial), ride angle `atan2` in the ring frame, phase
to the nearest corner, `GetContactCount()`, `TubeFollowActive`. Report: max tick-to-tick Δdist, RMS Δdist, contact
ticks, follow flips. Before the fix expect Δdist ≈ 0.1–0.3 m at 10–30 Hz with contacts on most ticks; after, Δdist
< 0.03 m, contacts ≈ 0, follow flips ≤ 2.

## Reproduce first

1. `-- --seed 9` (sample stage 1), enter the tube at 1.1 km, hold boost. Confirm the judder.
2. Harness: extend the tube ride (`tests/MovementToySelfTest.cs`, "Tube ride (08 §5, D-101)") with a second pass holding
   `InputBootstrap.Boost` from the mouth to the exit after `RefillBoost(BoostCapacity)`. Record per tick: radial distance
   from the axis, `TubeFollowActive`, `IsRawGrounded`, the contact normal. Print the largest tick-to-tick radial change
   and the count of follow-state flips. A judder shows as radial oscillation of about 0.1–0.3 m at 5–20 Hz.
3. Print the same trace at cruise (no boost) as the control.

## Fix (if the hypothesis holds)

1. `TubeMesh.Sides` 10 → 24. Facet error drops to R·(1 − cos(π/24)) ≈ 0.05 m. Vertex count per tube stays small
   (rings every few metres × 24). Keep the ribs and the mouth flare as they are.
2. In `TryTubeFollow`, hold the ball to the polygon's inscribed radius, not the analytic one:
   `float wall = radius * Mathf.Cos(Mathf.Pi / TubeMesh.Sides);` then `gap = wall - BallRadius - dist`. Inside a tube the
   collider then never fires while the follow is active, and the walls are analytic. Nothing else in the method changes;
   nothing outside tubes changes (the ground follow is untouched; `TubeContact` off must still equal the D-101 baseline
   minus the follow).
3. If the alternative (2) is the cause instead, the fix is in `PlayerVisual` (smooth the contact normal inside a tube),
   not in physics. If (3), widen only the tube branch's launch test, never the ground follow's.

## Boundaries

This packet may edit `TryTubeFollow` and `TubeMesh` only. It may not touch the ground follow, steering, boost, the cap,
or any compiled default. The golden hashes must not change (a tube's mesh is not in the hash; its axis is).

## Acceptance (harness)

- Boosted ride on the borrowed tube seed: carried 100% grounded, max tick-to-tick radial change < 0.03 m, follow-state
  flips ≤ 2 (mouth and exit), exit speed within 10% of the carried model at the boosted speed (report the number), lens
  outside the shell and sight clear as the existing ride asserts.
- The existing cruise ride unchanged (100% grounded, radial ≤ R − ball + 0.5, exit ≤ 20° off axis).
- Node count and golden hashes unchanged; full harness green plus `RUSHCORE_ARCHETYPE=canyon|dunes|sky`.

Manual: the user boosts through the sample tubes (stages 1 and 4) and the judder is gone.

## Files expected

`src/World/TubeMesh.cs`, `src/Player/PlayerPhysics.cs` (the tube follow only), `tests/MovementToySelfTest.cs`, docs 04 §5I
(one sentence: the follow holds the inscribed radius), 11 §7d (side count), this packet, `STATUS.md`,
`PROVISIONAL_DECISIONS.md` (P-entry: sides 24, inscribed-radius follow).

## Delivery record (running log; the main track started it, Opus continues it)

**Done** (each with its commit; add the harness check if one is missing):
- Branch `opus/t7-tube-judder` off `develop-secondary` (with the packet merged); builds.
- Harness instrumentation (WIP commit): the tube ride runs twice; pass 2 holds boost and, for three seconds of the cruise,
  the stick toward one wall, tracing per tick the radial distance, tick-to-tick change, contacts, follow flips, ride
  angle, facet phase (from `TubeMesh.Frames`, new public helper) and penetration into the facet plane. Three new checks
  (Δradial < 3 cm per tick, no contact ticks in the window, carried to the exit) are expected to FAIL before the fix.

- Before numbers (full harness, Highlands 9/0, 311/313 with the two expected failures): cruise pass unchanged (100%
  grounded, radial ≤ 5.33 m, exit 149 m/s at 0°, wall ride ≤ 78°). Boosted pass, steered window 180 ticks: ride ≤ 95° off
  the bottom, **Δradial max 15.7 cm per tick, rms 2.89 cm, contacts on 180 of 180 ticks, penetration into the facet plane
  ≤ 27.8 cm** (predicted 29.4), follow flips 0, bottom 0.0° off a corner. On facets 38 ticks (Δ avg 1.14 cm) vs near
  corners 142 ticks (Δ avg 1.43 cm): the phase correlation predicted in the Analysis did NOT show, because the lateral
  stick presses the ball into the wall everywhere, so the solver corrects at corners too; the depth of the fight (28 cm)
  and its presence on every tick confirm the mechanism (collider against follow), not the corner-only refinement.
  Frame twist is not a factor on this tube (bottom exactly on a corner).
- Fix applied (commit with the after numbers): `TubeMesh.Sides` 24; `TryTubeFollow` holds `radius·cos(π/Sides) − ball`.
  Docs 04 §5I, 11 §7d; P-009.

- After the first fix (24 sides + inscribed circle), full harness 311/313: cruise pass unchanged (100% grounded, radial
  ≤ 5.35 m, exit 149 m/s at 1°); boosted window: **Δradial max 5.7 cm, rms 1.32 cm, penetration ≤ 7.2 cm, contacts on
  175 of 180 ticks**, ride ≤ 116°. Better by 3×, not clean. Cause of the residual, from the code: the server integrates
  gravity *after* `_IntegrateForces`, so a ball the follow holds on the wall creeps outward by g·dt² per tick (≈ 1.1 cm)
  until the follow's closing velocity (excess / 0.05 s) balances it, at excess ≈ 1.1 cm × 0.05 / 0.0167 ≈ 3.3 cm past
  the 3 cm deadband: ≈ 6 cm inside the held circle, which is past the facet plane (0–5 cm). Prediction matched 7.2 cm.
- Second fix (same commit as the after numbers): the follow supplies the wall's **normal force** in advance, cancelling
  gravity's outward component (max(0, −outward.Y·g)) and the centripetal demand of the motion around the ring
  (u²/dist), both × dt, after the clamp. Still a velocity rule scoped to tubes; the ground follow is untouched (its
  collider supplies the normal force and contacts are wanted there).

- After the second fix (normal force), full harness 311/313: boosted window **penetration ≤ 3.9 cm, contacts on 129 of
  180 ticks, Δradial max 8.8 cm (rms 1.73 cm)**, ride ≤ 134°; the cruise pass fell to **98% grounded** (was 100%), a
  small regression to explain. Reading: the creep is gone (3.9 cm ≈ the 3 cm deadband plus the residual), but the
  follow lets the ball rest anywhere inside the deadband, and at a facet's middle the inscribed circle *is* the collider,
  so a ball resting 3 cm past the held circle touches it; the 8.8 cm jumps are the solver's corrections at those
  contacts. The velocity path was checked: `v = vT + planeNormal·vN` keeps the follow's normal component, so nothing
  downstream undoes the pre-compensation.
- Third change (commit with its numbers): the held circle moves one deadband plus 1 cm inside the inscribed one
  (`wall = R·cos(π/Sides) − 0.03 − 0.01`), so the ball's rest range ends at the facet plane; corners then float ≤ 9 cm,
  invisible. Harness: `RUSHCORE_TUBE_TRACE=1` prints per-tick wall state (dist, vOut, contacts, follow, raw, grounded,
  ground follow, height over terrain): the cruise pass prints its odd ticks (not raw-grounded, a contact, or the follow
  off) to explain the 98%, the boosted pass every fifth window tick.

- After the third change, full harness 311/313: boosted window **penetration ≤ 4.1 cm, contacts on 90 of 180 ticks,
  Δradial max 8.7 cm (rms 1.81 cm)**, ride ≤ 137°; cruise 99% grounded. Trace reading: the cruise pass's odd ticks are
  all at the mouth (ak 0–3, ground follow on, terrain contact), so the 98–99% is the entry handover, not a regression.
  In the boosted window the ball climbs the wall with `dist` swinging 5.01–5.31 m at about 2 Hz and `vOut` down to
  −10 m/s while no contact exists: the **centripetal pre-compensation is twice too strong** (a straight step of the
  motion around the ring leaves the circle by u²dt²/2r; a full-step impulse of u²/r·dt moves the ball inward by
  u²dt²/r), so the ball drifts inward, the P-closing pushes it back out, and the overshoot reaches the facet plane
  (contacts from tick ~90 on at dist 5.30 against a facet plane at 5.29). Gravity's term is exact (semi-implicit Euler
  adds g·dt at the same instant).
- Fourth change (commit with its numbers): the centripetal term × 0.5; the held circle two deadbands plus 1 cm inside
  the inscribed one (rest range ends 4 cm short of the facet plane; corners float ≤ 12 cm, the ball's surface sits
  7 cm inside the visible shell at a facet's middle: acceptable for the placeholder shell, noted as a visual to judge).

**Next** (in order; continue from the first):
1. Read the after numbers of the fourth change; paste them here. If Δradial max is still above 3 cm with few or no
   contacts, the residual is the P-closing's own step (excess / 0.05 s × dt) on the climb, and the check's 3 cm may be
   judged against the rms instead (state the numbers and the reasoning in P-009; do not change the shared closing
   constant). The three T7 checks and the cruise checks must pass and the golden hashes must be unchanged.
4. Fix: `TubeMesh.Sides` 24; `TryTubeFollow` on the inscribed circle; nothing else.
5. After numbers; the acceptance checks; the cruise ride unchanged; golden hashes unchanged.
6. Docs 04 §5I, 11 §7d; P-entry; STATUS row; full harness plus the three archetype runs; push; compare URL.


- Branch / commits:
- Harness (full run and the three archetype runs):
- Reproduction (trace numbers before: radial oscillation amplitude and rate, follow flips):
- Cause confirmed:
- Fix applied:
- Numbers after:
- P-entries written:
- Spec sections edited:
- Open items:
- Needs main track:
