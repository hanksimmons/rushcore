# T8 — The ball is arrested inside a tube (rubber-banding)

**Branch:** `opus/t8-tube-arrest` off `develop-secondary` (the camera half is already fixed on `feature/camera-interpolation`)
**Phase:** 3 defect (D-101 see-through tubes), separate from T7
**Owning specs:** 04 §5I, §9 (tube geometry); 03 §3, §12; 11 §7d; DECISIONS D-101
**Reported:** 2026-09-07 by the user, boosting through a tube: "the player model teleports backwards a metre or two,
ghosts forwards again, over and over", with the camera strobing along with it.

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

   | Archetype | Ticks more than 10% short | Worst |
   |---|---:|---:|
   | Rolling Highlands | 37 of 917 | 73% |
   | Dune Sea | 32 of 854 | 79% |
   | Sky Terraces | 43 of 911 | 95% |

   About one tick in twenty-five, on every archetype that carries a tube, and a 95% shortfall is very nearly a dead
   stop for a frame.

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
