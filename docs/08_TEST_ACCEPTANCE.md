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

## 2. Build gate

On target macOS arm64 development machine:

- Godot .NET project compiles,
- normal startup has no unexplained persistent errors,
- main project launches,
- debug composition logs are clean enough to diagnose failures.

Exact local commands are implementation/bootstrap work.

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
- Slow passive boost regeneration and active refill follow tuning.
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
- vertical grammar (D-096), for each archetype that uses it: the headroom, wall-clearance, drain and tube-clearance validators hold across the batch; the harness drives the ball into a tube at the cap and asserts it is carried through and exits along the axis with no face crossed; a lid holds the ball from above and from below; from the top floor of a Sky Terraces stage a dropped ball lands and the follower drives from the landing back onto the primary; the camera stays outside a tube for a whole ride with a clear line of sight to the ball against the terrain layer on every tick, and stays inside the declared clearance through a wall tunnel.

Manual sample:

- play/inspect diverse seeds,
- archetype identity recognizable from geometry,
- stages are broad directional landscapes,
- optional lines are meaningful,
- terrain is not noise soup,
- high-speed lines exist,
- a vertical stage reads top to bottom from the cloud band, the camera sees the ball through every tube, and a fall reads as a setback, not a death.

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

**Result (tubes, D-101, 2026-09-07):** the batch places tubes on every archetype (Highlands 16 on 15 seeds, Canyon
7, Dune Sea 41) and every tube passes its validators (cruise clearance ≥ 30 m above the ground, mouths inside the
level width, carried profile never stalls, graph acyclic); the harness drives the ball into a tube at the cap
(Highlands seed 9/0, 2 040 m, borrowed when the drive seed has none; the dune drive seed has its own) and asserts
it is carried through 100% grounded with its centre inside the radius, exits 0° off the axis at the carried
model's speed, and that the camera stays outside the shell on every frame with a clear line of sight to the ball
against the terrain layer; 285/285 Highlands, 284/284 dunes.

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

## 12. Definition of Done per task

Done means:

- compiles,
- intended behavior exists,
- relevant checks pass,
- no unrelated scope was added,
- runtime warnings are understood,
- owning docs updated only if contract changed,
- current slice remains playable.
