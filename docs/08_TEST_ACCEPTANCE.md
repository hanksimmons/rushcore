# 08 — Test & Acceptance Specification

**Status:** Draft 0.3 — final audited  
**Authority:** Canonical definition of “working” for major systems

## 1. Testing philosophy

Automate deterministic/correctness checks.

Playtest feel.

Do not pretend subjective movement quality is a unit-test problem, and do not leave deterministic invariants to manual testing.

Each slice must pass:

1. build/launch,
2. relevant automated/self-test checks,
3. manual acceptance gate.

**Screenshots as evidence (2026-09-19, the user's rule).** Numbers in a log are not the whole verdict on behaviour that
is seen. Launched windowed with `RUSHCORE_SHOTS=1` (no `--headless`; `docs/10`), the harness saves the rendered frame at
the moments worth seeing and short bursts of consecutive frames for motion, under `shots/selftest/<archetype>/` (numbered
in run order, git-ignored): the horizon from the start pad, a mid-stage burst on the drive, the lens under a lid, the
spiral's descent, a canyon wall ride entering (a burst), on the wall and back on the corridor, at both aims, the tunnel
portal ahead, a burst through the portal, the middle of the bore, the exit ahead and after it, the branch exit's pad, the
fine/coarse seam ahead at the 2 km mark and a burst of the window filling after the tunnel drive's teleport (D-115).
Whoever runs a slice looks at them, still and frame to frame, before calling the behaviour correct; a headless run skips
them and says so. Shots are queued and taken in the physics step before the script advances, so a case's numbers are the
same with shots on or off (the tunnel drive reads identically both ways). New moments are added with `Shot(name)` and
`Motion(name, frames, every)` in the case that owns them. Known windowed-only differences: the camera's sideways framing
check reads 23.8° against a 28° band (a viewport-size effect, open), and wall-clock budgets (scatter time) can miss under
rendering; the headless run is the verdict on numbers, the windowed run the verdict on looks.

## 2. Build gate

On target macOS arm64 development machine:

- Godot .NET project compiles,
- normal startup has no unexplained persistent errors,
- main project launches,
- debug composition logs are clean enough to diagnose failures.

Exact local commands are implementation/bootstrap work.

**Two harness runs (2026-09-20):** the everyday run is `RUSHCORE_SELFTEST_QUICK=1` (everything but the four-archetype seed
batch, which was half the wall time at 3×: the regression seeds, the golden hashes and the built stage stand in for it);
the full run, on the default, canyon and sky archetypes, is the gate before a push. Commands in `docs/10`.

## 3. Movement Toy — Gate M0

Toy contains only:

- calibration/test terrain,
- player,
- camera,
- procedural presentation/VFX,
- tuning/debug UI.

### Objective behavior

- No NaN/Infinity velocity.
- Player continuous collision detection is enabled.
- Player cannot sleep during normal player control.
- Standard gravity remains active without requiring a custom-force-integrator reimplementation.
- Ground/contact reporting is configured and produces usable contact data during normal terrain contact.
- Analytic ground follow (D-092): over the strip's 800 m / 80 m hill station the ball keeps raw contact on the whole approach at 4, 8 and 16 m cells with no facet hop; a charge held from 100 m before the apex survives to it and the release jumps there at every cell size; with the follow off, 16 m facets hop the ball and cancel the charge (the toggle is the baseline); on the generated stage every kilometre without a launch crest keeps ≥ 97% raw contact and every launch crest is left exactly when v² > g·r (never glued, never faked).
- Wall ride (D-108) on the lab quarter pipe: a ball turned 18° into the 30 m fillet near the cap registers no impact, keeps at least 80% of its entry speed on the wall, rides the face as ground, returns to the plain grounded with few or no airborne ticks; three ticks of stick toward the wall climb higher; with `Wall Ride` off the wall is never ground.
- Player accelerates from rest.
- Hard locomotion speed cap is consistently enforced using the grounded-tangent / airborne-horizontal definition without clipping vertical jump/slam behavior or rotating velocity at state transitions.
- Downhill terrain materially accelerates player until cap.
- Flat/uphill player propulsion works.
- Pressing Space while grounded begins jump charge.
- A fresh Space press after becoming airborne does not begin a new jump charge.
- Releasing Space performs the jump.
- Near-zero hold produces configured minimum vertical takeoff speed.
- Full hold produces configured maximum vertical takeoff speed.
- Charge value clamps at max duration but does not auto-jump.
- If ground is lost while already charging, release inside configured grace performs the charged jump; after grace expiry the charge cancels.
- Charging applies no artificial slowdown.
- Steering authority is zero during grounded jump charge.
- Jump takeoff preserves configured useful horizontal/tangent momentum.
- No double jump.
- Air steering remains bounded.
- New Space press while airborne triggers slam.
- Slam preserves configured lateral momentum.
- Every slam landing raises the power impact (slam flag on the landing event).
- A Space press inside the burst window either side of a slam touchdown fires the landing burst: speed multiplied by the tuned factor along the unchanged heading, limited by the effective cap, ball still grounded, and the press never starts a charge or a jump.
- A press outside the window, or after a plain (non-slam) landing, is an ordinary charge and never a burst.
- The burst never slows the ball.
- Flow headroom (D-088): Flow is zero after a recovery and the cap is the base cap; a charged jump, a slam landing and a burst each grant Flow and the chain stacks; with Flow the ball travels above the base cap and never above the effective cap; no time decay inside the chain window; braking drains Flow and the cap falls with it; with headroom 0 the cap is the frozen base cap; a clean drive on the rig and on a generated corridor registers no impact; recovery zeroes Flow.
- Thin-wall CCD holds at the Flow ceiling (base cap × (1 + headroom)) as well as at the base cap.
- Carve (D-089): the button does nothing below the minimum speed; held above it while grounded the facing swings toward the input at the yaw rate while the velocity turns far less; the camera tracks behind the facing; release re-aims the velocity onto the facing with no speed lost; a real carve grants Flow.
- Boost works on ground and air.
- Boost cannot bypass hard locomotion cap.
- A run starts at the tuned boost fraction; no passive boost regeneration by default; the drain, the passive rate and the pickup refill follow tuning (D-106).
- Fall recovery restores a valid pose and resets physics interpolation so no old→new-position visual streak occurs.

### Manual playtest

- Ten minutes of movement without progression remains fun enough to continue development.
- Low-speed control feels immediate.
- High-speed lines require anticipation but still feel controllable.
- Hard cap feels like a designed maximum, not a broken physics clamp.
- Quick-tap jump has a useful role.
- Charged jump has a useful role.
- Steering lock makes long charge a real commitment rather than a strictly superior jump.
- Jump preserves the sensation of speed.
- Air control corrects rather than rewrites trajectory.
- Slam feels exceptionally snappy.
- Timing the burst press at touchdown is learnable without a QTE.
- The landing burst reads as a seamless surge, noticeably faster than the landing speed.
- Slam is useful for reconnecting with favorable downslopes.
- Boost increases route possibilities.
- Camera lets player read terrain at maximum normal playable speed.
- Representative thin/edge collision tests at playable max speed do not show routine player tunneling.

**Hard gate:** do not build roguelike progression if M0 fails.

**Result:** M0 passed 2026-09-05 (human playtest; objective checks automated in `tests/MovementToySelfTest.cs`).

## 4. Scale calibration — Gate M1

Before freezing procedural dimensions:

Create a calibration environment with known distances and representative terrain sizes: the 6.4 km scale strip (`World › Calibration Strip (M1)`, D-079) with stations at stated sizes for corridor width, hill wavelength/height, gaps, ramps and turn radius.

Evaluate:

- ball diameter,
- speed bands/max speed,
- hill height/wavelength,
- gap lengths,
- bank radius,
- charge-jump height / hang time / range at representative speeds,
- landing-burst surge as an entry-speed source for optional lines,
- camera distance,
- sightline: seconds of readable terrain ahead at the cap with the chase camera.

Outcome:

- freeze a coherent world-scale family for procedural generation,
- freeze the terrain budget at that scale: cell size (with its contact measurement), stage footprint, samples/triangles/build time, draw distance versus sightline, and whether Phase 2 needs finer tiling than the default (D-080),
- document the measured/tuned values.

**Result:** M1 passed provisionally 2026-09-05 (D-082, `docs/11`): the family is derived from the frozen
envelope and the strip/budget measurements; the feel verdicts and the frame-time reading are taken on the
first generated stage instead of the strip.

**M1 addendum (D-088 → D-094, delivered 2026-09-06):** the Flow ceiling re-derives the safety side of the
family at the D-091 baseline (`docs/11 §6`). The route speed model gained an airborne-and-landing phase
(launch where v²κ ≥ g cos θ over a 3-cell window, the ground follow's own rule; ballistic flight under air
control and drag; touchdown keeps the tangent component) and a ceiling mode (cap × (1 + headroom), steering
saturated at the base cap). Measured by the harness every run: the ceiling bend radius (201 m) and the
saturated ladder; a cliff flight against closed-form ballistics within 10%; turn radius at the ceiling on the
turn pad (204 m measured against 201 m predicted (28.1° over 100 m at 254 m/s)); the hill stations at 254.7 m/s, real flights against the model over the same centreline
(four flights in both, real [2048→2757 m, 53 m/s down → 198 m/s] [2777→3132, 41 → 166] [3151→3523, 53 → 134] [3735→4052, 67 → 151] against model [2029→2811, 56 → 212] [2815→3102, 47 → 181] [3148→3601, 52 → 141] [3763→4128, 72 → 153]: the ball keeps 7–9% less speed through each landing than the tangent rule and leaves the next facet later and lower, so the model's chained flights run up to 27% long (the safe direction for the validators), landing vertical speeds within 15%); the base-kit flights at each launch crest of the generated stage against the ball (ball 2456→2752 m, model 2457→2760 m (3%), landing 50 m/s down → 138 m/s).
Cell contact is no longer a ceiling question (D-092: 100% at every cell size). Sightline at the ceiling is the
user's Gate F0 verdict with Flow on. Generation now reads two speeds (04 §12): every report carries seconds
below the base cap, the ceiling profile with its flights and hard landings, no bend inside a flight or its
100 m landing run at either speed, and a chainable line on the primary (100 seeds: 100 valid, 0 fallbacks; seconds below the base cap 11.0 s average; 2.4 ceiling flights per stage, 3.7 s airborne; widest opportunity gap 1516 m against the 1528 m window).

## 5. Procedural Generation — Gate G0

For each initial archetype, generate a meaningful deterministic seed sample (target at least 100 automated requests).

Required:

- route speed model matches the real controller within 5% on the strip runway (0→cap) and the lab grade fan (descent speeds), and it is deterministic (D-081),
- start/exit valid,
- deterministic same-request summary/hash,
- primary route validation passes after bounded regeneration/fallback,
- mandatory jumps fit base capability envelope without required boost,
- checkpoints have clearance,
- primary corridor is not blocked,
- archetype rules do not modify hidden player physics,
- two speeds (04 §12, D-094): the ceiling profile is computed for every stage; no bend sits inside a flight or its landing run at the base cap or at the ceiling; a chainable line exists on the primary; every report carries seconds below the base cap,
- vertical grammar (D-096), for each archetype that uses it: the headroom, wall-clearance and drain validators hold across the batch; a lid holds the ball from above and from below; from the top floor of a Sky Terraces stage a dropped ball lands and the follower drives from the landing back onto the primary; the camera stays inside the declared clearance through a wall tunnel,
- the wall profile (D-109), on every walled archetype: beside the middle of the longest straight the ground rises from the corridor as the 30 m fillet and the 72° face on both sides within 1.5 m of `WallProfile`, and reaches the side terrain past the profile's top; the canyon drive borrows a canyon seed with a clear straight (no feature, lid or ledge beside it, and a wall that is not a neighbouring bend's inside), boosts from the centreline with a 20° aim at the wall and asserts no impact on entry, a ride of at least ten ticks as ground with most of them on the analytic profile, at least 75% of the entry speed kept on the wall (the climb limit sheds a little at 20°), and a grounded return onto the corridor; the same window ridden head-on (90°) near the cap asserts the climb scrub (D-110): no impact and no tick losing the impact threshold, at least ten wall ticks and a 20 m climb priced once as a scrub, a peak below the lip, and a return to the corridor; the wall shell (D-111): a walled stage builds its shells, the heightfield beside the ride window is sunk by the shell sink from the fillet to the face and coincides with the stamp on the corridor floor before the wall, and the 20° ride's distance from the analytic wall moves under 10 cm tick to tick,
- tunnels (D-113, `docs/13 §2.8`), on Canyon Run: the batch places covered portal tunnels on at least 30% of seeds and every
  tunnel passes its validators (04 §12); the canyon drive borrows a seed with a covered tunnel and drives the ball from the
  primary through the tunnel line to the rejoin, asserting the stage builds its roofs, no wall-shell vertex stands over the
  tunnel's floor along the covered run (the hole), the ball is grounded at least 95% of the time inside, neither portal is an
  impact, the lens stays under the arch and inside the walls on every tick inside, the exit speed is within 10% of the
  model's, and a ball set down on the surface over the covered run rests on the cap as a floor,
- dives (D-116, `docs/13 §2.8`), on the open archetypes: the batch places covered dives on at least 12% of Highlands seeds, 20%
  of Sky Terraces and 5% of Dune Sea (the measured shares less margin; sites, not the chance, are the limit) and every one
  passes the tunnel validators, which now also refuse a flight at the base cap anywhere on a tunnel line; the tunnel drive
  above runs on whichever archetype the harness drives (borrowing a seed of that archetype with a covered tunnel) and adds the
  **cap drive**: the ball driven along the surface over the covered run, from a fifth in to a fifth before its end, stays
  grounded at least 90% of the way and never below the cap's top,
- the far horizon (D-114, `docs/13 §4.4`): the ring builds on every archetype's batch seed with no point inside the
  footprint, its inner ring meeting the stage's own ground within 5 m at 16 probes and at most 60 k triangles; the built
  stage draws it,
- the resident mesh (D-115, `docs/13 §3.4`): the world builds to its first frame within 2 s outside the collider (the
  collider, a mesh shape until the user decides it, is bounded at 8 s; D-118) with the fine window round the start pad complete and nothing still building; on every
  kilometre of the drive every tile within 1.0 km of the ball has its fine mesh, no fine tile beyond 1.6 km is resident,
  the resident triangles stay under 1.2 M, and the coarse mesh's height under the ball agrees with the collider within
  its cell's own chord (the span of the cell's corners plus half a metre),
- Summit Descent (D-107, `docs/12 §8`), once delivered: across the batch every primary vertex's corner limit holds the base cap; the rails and headwalls are continuous (sampled every 10 m, both sides, chute gaps and exit forks excepted); the base-cap profile has no launch on the switchback section; the descent is 540–660 m with every tier 90–170 m; every hairpin passes the sightline rule; every chute's free path is grounded, its half-charge landing falls inside its landing zone and its full-charge flight lands drivable; the face outside the rails drains to the valley. The harness drives a base-cap ball into a rail at 25° and straight into a headwall and asserts it stays on the bench both times, drives the switchbacks to the valley fully grounded, and drives a chute free and half-charged with the slam landing inside the zone,
- exits (02 §4, D-105): every stage's exits are distinct, inside the footprint and on level pads; most seeds of each archetype offer a second exit (floors: Highlands 75%, Canyon Run 85%, Sky Terraces 65%, Dune Sea 40%); the harness drives the ball from the primary up a terminal line's ramp to its pad and asserts the stage ends by that exit with the plateau holding the ball grounded.
- the 3× course (D-118, `docs/13 §3`): the batch's routes sit within 0.9–1.3 of the 18 km target and their base-kit times bracket
  140 s (100–210 s); a stage definition generates in 350 ms on average; the batch checks determinism on every tenth seed; the
  full drive covers the selector's archetype's whole route within the model's time (the drive bound is 300 s); a ramp lip's flight
  is held to 30% of the model's (a crest's to 20%): at a lip the follow releases at the ease and the last facets add up to 9 m/s of
  vertical, open for the controller.

Manual sample:

- play/inspect diverse seeds,
- archetype identity recognizable from geometry,
- stages are broad directional landscapes,
- optional lines are meaningful,
- terrain is not noise soup,
- high-speed lines exist,
- a vertical stage reads top to bottom from the cloud band and a fall reads as a setback, not a death,
- a fork reads as a choice from the primary at speed, and each exit pad reads as an ending (D-105).

**Result (Rolling Highlands):** objective checks passed 2026-09-06 (D-087). The harness generates 100
requests plus the regression list every run (all valid, deterministic hashes, no fallback), drives the
whole primary route of the built stage with its follower and holds the route speed model's base-kit
time within 10% (measured 4.3%: ball 49.9 s, model 47.8 s), verifies no solid prop stands inside a
corridor, every anchor arms and restores, the SceneTree node count is flat across three regenerations,
and neither generation nor a stage build writes to tuning or the rigid body. Mandatory jumps do not
exist yet (the Phase 3 gap modules bring them and their envelope check). The gate is re-run per
archetype as Phase 3 adds Canyon Run and Dune Sea; the manual sample is the user's. At the D-091
baseline with the ground follow (D-092) and the interpolated corridor profile (D-093) the same drive
reads 0.5% (ball 47.2 s, model 47.4 s) with raw contact 100 / 100 / 67 / 100 / 100 / 75% per kilometre;
the two low readings are the launch crests, each left exactly when v² > g·r.

**Modules (D-097, 2026-09-06):** the batch asserts every challenge module passes its validator and that
gaps, ramps and banked turns all occur (100 seeds: 54 / 16 / 134, 0 fallbacks, 96 seeds with a ridge
line); the mandatory-jump envelope check is live (half charge at the model's arrival speed). The drive
case treats every feature as a launcher and compares the ball's flight off a ramp lip or a gap's far rim
with the model's (seed 1: ramp flight within 9%, whole route within 0.7%); `--seed N` drives stage N/0
so a module seed can be chosen. Regression seeds now include a ramp seed, a gap seed and the
regeneration-bound edge (three failed attempts, the fourth valid).

**Result (Canyon Run, D-098, 2026-09-07):** the batch runs per archetype (100 canyon seeds: 100 valid,
0 fallbacks, 52 gaps / 30 ramps / 97 banked turns all passing, 90 seeds with a ledge line, deterministic
hashes); the wall-clearance validator holds on every seed; `RUSHCORE_ARCHETYPE=canyon` drives a canyon
stage in the drive case and two canyon seeds sit in the regression list. The manual sample is the user's.

**Result (Dune Sea, D-099, 2026-09-07):** 100 dune seeds: 100 valid, 0 fallbacks, deterministic hashes, 259
crests of which 102 trains of two or more on 83 seeds, 24 gaps / 32 ramps / 61 banked turns all passing, every
train's crests meeting the relief wave within 2 m (the new dune validator), anchors ≥ 8; only 2 seeds needed a
second attempt. `RUSHCORE_ARCHETYPE=dunes` drives a dune stage: the ball's time within 0.3% of the model over
5.9 km, each crest launched exactly when v² > g·r (1.48–1.53), the three flights within 3 / 5 / 9% of the
model's, landing 55–62 m/s down. Only 40 seeds carry a ridge line, because ridge lines avoid feature straights
and a dune sea is mostly trains: the batch's optional-line floor is 30% for this archetype and a dune lane is
the open gap. Three dune seeds sit in the regression list. The manual sample is the user's.

**Gap closure (D-100, 2026-09-07):** every optional line is integrated at both speeds and judged on its own
geometry (no flight through a turn, landings hold the corners in their run); the check found that every ridge
launched the base kit at the top of its 200 m climb (knee radius 170 m against the cap's 560 m) and that a
transition through a banked bend rode the berm, so the ridge was redesigned (cosine ramps outside the
transitions, transitions on straights, r 70 S-curves) and a failing line is dropped rather than the stage.
Launch crests carry the two prices (the charged jump lands on the straight; the ceiling's slam landing does)
and dune trains are integrated as trains. Measured after the closure: 100 / 0 on all three archetypes, no
ramp reports a missing free flight any more (the launch window is the lip ease plus the curvature span), 0
lines dropped, ridge lines on 90% of Highlands and 70% of Canyon seeds (the batch floors), 11% of dune seeds
(reported, not judged). One archetype selector (`World › Archetype`) replaces the per-archetype toggles.

**Result (tubes, D-101, 2026-09-07):** superseded; the see-through tubes and their harness ride were removed on 2026-09-19 (D-112).

**Result (wall tunnels and the spiral pit, D-102, 2026-09-07):** the canyon batch places lids and pits (193 lids all
keeping their 15 m clearance, 53 of 100 stages ending in a pit; 100 valid, 0 fallbacks), the headroom validator
holds on every archetype; the canyon harness borrows a seed
with both (2/0) and asserts a charged jump under a lid never puts the ball above the roof, the roof carries the
ball as a floor, the lens stays under the roof through the tunnel, the spiral drives to the exit pad on the pit
floor at 134–149 m/s fully grounded, and a ball dropped off a turn's inner edge lands on the turn below and drives
on to the exit.

**Result (tunnels, T1, D-113, 2026-09-19):** 100 canyon seeds: 80 tunnel lines, all 80 covered, on 66 seeds (covered runs
487–752 m, avg 573 m), every one passing its validators; the tunnel drive on seed 1 (fork at 535 m, covered 254→965 m of a
1218 m line): 297 ticks inside, grounded 100%, min speed inside 126.7 m/s (a brush of the wall in the S), no portal impact,
exit 148.5 m/s against the model's 148.5, the lens under the arch on all 297 ticks and never in the rock, the cap holds the
ball as a floor; the wall-ride and lid checks unchanged. Canyon hashes re-recorded ("tunnel" and "tunnels + pit"); no other
archetype's moved.

**Result (dives, T2, D-116 with D-117, 2026-09-20):** 100 seeds per archetype: dives on 20 Highlands seeds (chance 0.5), 29
Sky Terraces (0.4), 8 Dune Sea (1), covered 450–1100 m, every one passing its validators, two sky dives per hundred stages
dropped for a flight at the cap; the canyon's portals 68 on 59 seeds (chance 0.7; six dropped by the corrected launch
stencil, which also drops 14 / 15 / 5 / 36 lines per hundred Highlands / Canyon / Dune / Sky stages that the blind stencil
passed). The dive drive on Highlands seed 1 (fork at 2058 m, covered 513→1398 m of an 1862 m line): 362 ticks inside,
grounded 100%, min speed inside 148.5 m/s, no portal impact, exit 148.5 m/s against the model's 148.5, the lens under
the arch on every tick; the cap drive over 433 m of the covered run grounded 100%, never below the cap's top. Before
D-117 the same drive was 84% grounded inside: the ball flew off the trench's start, where a floor that only cut rode the
lower ground beside the primary and then dropped into the cut. Default 405/405; canyon 423/423 (its portal drive unchanged: 289 ticks inside grounded 100%, exit at the model's speed); sky 406/406 (the dive drive on sky seed 1: 294 ticks inside, grounded 100%, exit 148.5 m/s, the cap drive 100% over 528 m); the windowed default run 404/405 with only the known camera-band check, the dive's open cut, portal, bore and cap seen in its shots.

**Result (the 3× course, W2, D-118, 2026-09-20):** 100 seeds per archetype: 100 valid, 0 fallbacks on every archetype; routes
19.8–21.1 km (Dune Sea 18.1–18.2), base-kit 139–147 s (Dune Sea 126–130), generation 127–320 ms average (Sky Terraces the most:
its dropped lines rebuild the field); lines 5 per stage (Dune Sea 1.7), portals on 95% of canyon seeds with six lids each, dives on
68 / 62 / 75% of Highlands / Dune Sea / Sky seeds, a second exit on 79 / 93 / 57 / 68% (Highlands / Canyon / Dune / Sky). The
Highlands drive seed builds to its first frame in 4.9–6.8 s on the first build of a process (generate 0.36, heights 0.47,
collider 3.9–5.7, mesh 0.14, structures 0.07, dressing 0.08) and 3.3–3.9 s after (the collider 2.2–2.5 s): outside the collider the
build is one second. The full drive: 20 079 of 20 083 m in 139.1 s against the model's 141.0 s (1.3%), grounded 90%, 16–20 fine
tiles per kilometre, peak 977 k resident triangles, the coarse mesh within 0.01 m of the collider. Default 406/406 in 236 s
wall; canyon 424/424 in 261 s (the 20 km drive within 1.1% of the model, the wall ride's window 100 m clear of bends both ways,
the head-on hit bounded by the scrub's own first tick, the exit line's level run grounded 100% with its descent's 22 airborne ticks
of 99 reported apart); sky 407/407 in 233 s (a 2.5 km terrace driven 2372 m grounded 100%). Every golden hash re-recorded; "sky
floor 3" is seed 6.

**Result (the resident mesh, W1, D-115, 2026-09-19):** the Highlands drive seed builds to its first frame in 1.86 s (the
canyon sample 2.1 s; 4.6 s before W1), 1.15 s of it the collider (a Jolt mesh shape, because the map is not square; the
user's call, `docs/13 §3.3`); the window round the start pad is 8 tiles, 264 k triangles, complete with nothing pending;
on the six kilometre marks of the drive 16–20 fine tiles are resident, none uncovered within 1.0 km and none beyond
1.6 km, peak 769 k resident triangles, the coarse mesh under the ball within 0.09 m of the collider (cell spans 0.5–1.6 m);
the node count stays flat across regenerations (the window owns no nodes of its own). Default 393/393; canyon 419/419 (first frame 2.23 s on its drive seed, 15–20 fine tiles per kilometre, the same 769 k peak, coarse within 0.01 m of the collider).
The M1 cell-size check now compares the fine window's triangles (106 k at 4 m, 26 k at 8 m), the coarse mesh being resident
at 16 m whatever the cell size.

**Result (Sky Terraces, first cut, D-103, 2026-09-07):** the sky batch places terraces on floor 2 and floor 3
(100 seeds: 100 valid, 0 fallbacks, 96 floor-2 and 3 floor-3 terraces on 82 seeds, 13 dropped by the drain or
corner checks) with the drain validator holding on every kept terrace; the sky drive (`RUSHCORE_ARCHETYPE=sky`)
follows the primary within 0.4% of the model, drives a floor-2 terrace 1.3 km grounded 88%, and,
borrowing seed 30/0 for a floor 3, drops the ball off the top floor's inner edge (123 m) onto floor 2 (64 m) and
drives it back to within 30 m of the primary in 4.3 s; 302/302. The cloud band and the manual read of the stage
from top to bottom are the user's.

## 6. Combat — Gate C0

Objective:

- Pylon crush above expected impact threshold succeeds.
- Same contact below threshold causes failed hostile impact.
- Relative closing direction matters.
- Bulwark requires stronger context.
- Successful crush retains configured momentum.
- Failed impact causes bounded velocity loss/deflection/damage.
- Damage invulnerability prevents contact spam.
- defeat/reward/Flow resolution fires once.

Manual:

- player quickly learns which targets are safe to ram,
- enemy chains improve Flow rather than stop movement,
- failures punish momentum without routinely causing run-ending chaos,
- no conventional stop-and-melee requirement.

## 7. Complete Stage — Gate S0

A complete stage:

- starts deterministically,
- has broad forward progression,
- includes optional lines,
- spawns gameplay content,
- supports fall recovery,
- allows exit without killing every enemy,
- does not require boost on mandatory primary route,
- exits cleanly without previous-stage leakage.

Harness (T1, the stage-lifecycle case on the "three exits" sample, Highlands seed 4): a run starts at stage 0 of its
run seed and `--stage 3` builds a stage hash-identical to a direct request for it; reaching a pad raises completion
exactly once carrying the exit index, and the finished pad cannot complete the stage that follows; the outro ignores
steering, jump and boost input while the ball keeps rolling; after the transition the clock, exit, anchor and progress
are clean, the ball is on the new spawn pad at rest with the camera down the route, Flow and boost both carry
(P-010), the exit taken picked the archetype (P-011) and the same seed and exit pick it again; three transitions
leave the node count and the orphan count where the first build left them.

Measure clear time; use results to validate stage/run pacing rather than forcing the previous paper target.

## 8. Run — Gate R0

Complete nine-stage run:

- route choices show promised metadata,
- normal transitions present two choices unless intentionally replaced,
- temporary progression resets on run death,
- meta progression persists,
- upgrade/item/shop accounting is correct,
- victory/death recap works,
- final encounter uses core movement/combat verbs.

Manual:

- at least three complete runs with materially different route/build outcomes,
- base movement remains viable without a mandatory upgrade,
- build power grows without removing movement skill,
- no safe indefinite farming becomes optimal.

**Delivered (progression P1, D-119, 2026-09-20; `docs/16 §6`):** on a director of its own, XP 10 + 20 + … + 70 reaches level 8
at exactly 280 and idles past it, a stat takes three ranks and no more with each choice spending one queued level, every
ladder steps from its baseline in order, a new run resets all of it; every batch seed places its orbs and cash by the rule
(count bounds, off every pad and lid, clear of the start and of every covered run, deterministic on the re-generated seeds);
on the built stage every pickup rests on its ground and the drive's magnetised pickups are all collected and credited as
XP and cash; each rank at its read point (Hangtime 3 flies a full charge 1/0.7 as long, Auto Refill 3 regains 2/s at rest
while rank 0 regains none, Ball Size 3 rests ×1.36 higher, Max Speed 3 holds 175.2 m/s on the runway) with the run left at
rank 0 for every other drive; the outro holds at the choice panel with a level queued and continues on the choice; a level-up mid-run opens the panel,
pauses the run and resumes on the choice with the rank raised (D-120; the drives run with `ChooseAtOnce` off, at rank 0); the
HUD's level and XP lines. Default 428/428, canyon 446/446, sky 429/429; no hash moved. The ridge-line drive snaps the camera down
the line before it starts (it steered from the recovery's leftover yaw and ran a dive 19 m wide into the trench wall).

## 9. Presentation — Gate V0

At maximum playable speed:

- player remains visually distinct,
- charge state is readable,
- slam landings and the landing burst have clear feedback,
- gaps/ramps/banks are readable early enough,
- enemy archetypes are distinguishable,
- reward lines do not read as hazards,
- Flow/boost state is readable,
- particles do not obscure landing surfaces,
- game remains playable with camera shake reduced/disabled.

## 10. Performance — Gate P0

Measure on target Apple Silicon machine.

Initial expectations:

- stable 60 FPS at intended development resolution in representative stage,
- no routine physics hitching at max playable speed,
- no unbounded node growth across stages,
- no obvious managed-allocation hot-loop spikes,
- stage generation transition instrumented in milliseconds.

Physics tick rate starts at 60. Test 120 only if collision/feel evidence justifies it.

## 11. Regression seeds

Maintain known problematic seeds once discovered.

Every meaningful procedural bug should become reproducible where possible.

The list is `tests/RegressionSeeds.cs`: one entry per stage request (run seed / stage index, as the
telemetry seed row shows them) with the reason it is there. The harness generates every entry on every
run and asserts it is valid without the known-safe fallback, so a fixed failure stays fixed. Add the
seed when the bug is reproduced, before the fix; never remove one.

`tests/GoldenHashes.cs` pins the hash of every sample stage (docs/10): the harness regenerates each sample data-only
and asserts the hash is unchanged, so any change to generation fails the run at once. An intended generator change
updates the table in the same commit and names it in its decision; a branch that must not touch generation
(`docs/handoff`) never edits it.

## 12. Definition of Done per task

Done means:

- compiles,
- intended behavior exists,
- relevant checks pass,
- no unrelated scope was added,
- runtime warnings are understood,
- owning docs updated only if contract changed,
- current slice remains playable.
