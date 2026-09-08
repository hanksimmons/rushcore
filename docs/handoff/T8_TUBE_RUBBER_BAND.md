# T8 — The ball is arrested inside a tube (rubber-banding)

**Branch:** `opus/t8-tube-arrest` off `develop-secondary` (the camera half is already fixed on `feature/camera-interpolation`)
**Phase:** 3 defect (D-101 see-through tubes), separate from T7
**Owning specs:** 04 §5I, §9 (tube geometry); 03 §3, §12; 11 §7d; DECISIONS D-101
**Reported:** 2026-09-07 by the user, boosting through a tube: "the player model teleports backwards a metre or two,
ghosts forwards again, over and over", with the camera strobing along with it. **Resolved in play** on the combined
build (`feature/tube-fixes`): the camera fix removed what the user was seeing, and T7 removed all but a trace of the
arrest underneath it. A later report of the bottom of the tube still rubber-banding was on the T7 branch alone, which
does not carry the camera fix.

## What was found, and what is already fixed

Two independent defects were behind that one symptom.

1. **The camera snapped along the tube (fixed, `feature/camera-interpolation`).** `CameraRig.PushOutOfTubes` rebuilt the
   lens position from the nearest axis sample after deleting its along-tube offset, so on every frame the push fired the
   camera's station along the tube jumped to the 4 m sample grid — up to ±2 m, on and off, frame after frame. Its own
   doc comment says the lens should be "pushed along the radial"; the code relocated it instead. The push now preserves
   the along-axis component and tests the *radial* distance rather than the distance to the sample. Measured worst
   abrupt change in the lens's step: **4.94 m → 2.30 m**.

2. **The shell arrests the ball (open, this packet).** Independently of the camera, the ball's travel over a physics
   tick falls short of what its own velocity says, with the tube follow active and one or two contacts reported on
   every such tick. At the speed cap the ball should cover 2.48 m a tick; on the bad ones it covers 1.3–1.6 m and then
   resumes. That is the rubber-band the user sees, and the camera faithfully follows it. Across the archetypes:

   | Archetype | Camera fix alone (10-sided shell) | Both fixes (T7's 24-sided shell) |
   |---|---:|---:|
   | Rolling Highlands | 37 of 917, worst 73% | **1 of 915**, worst 94% |
   | Dune Sea | 32 of 854, worst 79% | **0 of 842** |
   | Sky Terraces | 43 of 911, worst 95% | **3 of 892**, worst 79% |

   **T7 all but removes it.** Measured again with T7 merged alongside the camera fix, the arrest falls from about one
   tick in twenty-five to one in nine hundred, and Dune Sea has none at all. None of the survivors is near the bottom of
   the tube: their mean ride is 99–171°, i.e. up the wall and onto the ceiling, where the ball is leaving the surface
   anyway. So what is left of this defect is rare, confined to the ceiling, and no longer the thing a player feels.
   The packet stays open because a 79–94% shortfall is still very nearly a dead stop for one frame when it does happen.

## Evidence that it is longitudinal, not radial

- Holding the ball **six times further inside** the shell (`TubeAxisUncertainty` 0.05 → 0.30 m) changes the result not
  at all: 37 of 917 ticks, worst 73%, identical to the digit. A radial margin cannot clear an obstruction that is
  *ahead* of the ball rather than beside it.
- T7 already showed the radial fight is gone: penetration into a face is 0.0 cm on Highlands and Dune Sea and 0.9 cm on
  Sky, against 28 cm before that fix. So this is not the cross-section.
- The stalls carry contacts while the follow is active, so the collider is doing it.

**Leading hypothesis.** The shell is a swept polygon: rings of 24 vertices at every 4 m axis sample, joined by flat
quads. Around a bend the ring ahead is tilted toward the inside of the turn, so its leading edge stands proud of the
surface the ball is rolling along, square across the ball's path. A ball at 149 m/s meets one every 27 ms. That is the
longitudinal twin of the cross-section faceting T7 fixed, and it is exactly the effect D-092 describes for terrain
("at speed every convex facet edge launches a real sphere"), except that here the edge blocks rather than launches
because the ball is inside a concave surface.

## What to do

1. Confirm the hypothesis before fixing. The stalls print under `RUSHCORE_TUBE_TRACE=1` as `[STALL]` lines with the
   axis index; at those indices, measure the angle between consecutive axis segments and the height of the ring-join
   crease on the inside of the bend, and check the stalls land on the creases.
2. If confirmed, the candidate fixes, cheapest first: resample the tube axis more finely where it bends (the sweep is
   built from `WorldScale.RouteSampleSpacing`, 4 m, regardless of curvature); or round the join by averaging
   consecutive ring frames; or give the follow a forward look so it lifts the ball over a crease the way the ground
   follow's curvature test handles terrain.
3. Do not widen the radial margin further: it is proven not to help, and it already costs the ball ≈ 12 cm of float
   off the glass.

## Acceptance

- Under `RUSHCORE_TUBE_TRACE=1` the `tube ride travel (T8)` line reports **zero** ticks more than 10% short, on the
  full harness and all three archetype runs.
- The existing tube-ride and T7 checks are unchanged and still pass; golden hashes unchanged.
- Manual: the user boosts through sample stages 1 and 4 and the lurching is gone.

## Delivery record

- Branch / commits:
- Harness:
- Cause confirmed:
- Fix applied:
- Numbers after:
- Open items:
- Needs main track:


## Mouth entry (the Session A playtest note, 2026-09-07)

**Reported:** "when entering the tubes we often get a disruptive collision that slows the player down, just rolling
from the ground into the tube along the bottom."

**Found.** Two separate steps stood across a ground mouth, and the ball met both at the cap.

1. **The mouth flare was in the collider.** `TubeMesh.Build` flares every ring to `2R` over the first and last `2R`
   of axis so an entrance reads as an entrance (06 §3). The collision triangles were built from those same flared
   rings, so at the mouth ring the shell's floor sat `2R` below the axis — and `TubeBuilder` puts the axis exactly
   `R` above the corridor, so the collided floor was `R` (six metres) *below* the terrain, a cone buried under the
   ground with backface collision on, whose walls cross the surface exactly where a ball rolls in. The follow
   models a constant-radius cylinder and never hears about the flare, so the two surfaces disagreed by up to `R`
   at the one place the ball arrives. **Fix:** collide the unflared rings; draw the flare.
2. **The collided facet sat 5 cm inside the analytic circle.** The rings were inscribed, so the flat facet a ball
   rests on lay at `R·cos(pi/24)` — 5 cm above the corridor at a mouth whose floor should be flush. **Fix:** build
   the collided rings circumscribed about the analytic circle, so the facet's nearest point to the axis is exactly
   `R`. The follow then holds `R` rather than `R·cos(pi/24)`, and its T7 property is unchanged: the ball is still
   held inside the collider's inscribed circle, which is now the analytic one.

**Measured** (Highlands seed 9, the tube sample, rolling in at the cap):

| | Speed at the mouth | At the end of the 12 m flare | Worst one-tick loss | Ticks in contact |
|---|---|---|---|---|
| Before | 148.5 m/s | 125.0 m/s (16% gone) | 15.1 m/s | 6 of 11 |
| After | 148.5 m/s | 145.1 m/s (2% gone) | 4.5 m/s | 10 of 12 |

T7's numbers are unharmed: penetration under the follow still 0.0 cm over 180 on-wall ticks, one grazing contact,
zero follow flips. The harness's penetration reference was corrected with the collider (the facet plane is now at
`R`, not `R·cos(pi/24)`); that is a correction to the measurement, not a loosened tolerance.

**Needs main track — the remaining 12 cm.** What is left of the entry cost is a lip the generator leaves:
`TubeBuilder.Make` sets the mouth axis from the *route vertex* height (`y = pv.Position.Y + R + cruise * e`) while
the ground under the mouth is `field.Sample(p.X, p.Z)` at the tube's lateral offset, and the two differ — measured
12 cm on this seed. The validator tolerates up to a metre of it (`if (y - ground < R - 1f) ok = false`). Taking the
mouth height from the ground under the mouth point, or tightening that validator at the mouths, would close it, but
either changes tube geometry and therefore every stage hash, so it is the main track's to make. The harness reports
the entry cost and does not assert a threshold on it: asserting one above the remaining lip would bless it.
