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
- A Space press inside the burst window either side of a slam touchdown fires the landing burst: speed set to the tuned fraction of the cap along the unchanged heading, ball still grounded, and the press never starts a charge or a jump.
- A press outside the window, or after a plain (non-slam) landing, is an ordinary charge and never a burst.
- The burst never slows a faster ball.
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
- archetype rules do not modify hidden player physics.

Manual sample:

- play/inspect diverse seeds,
- archetype identity recognizable from geometry,
- stages are broad directional landscapes,
- optional lines are meaningful,
- terrain is not noise soup,
- high-speed lines exist.

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

## 12. Definition of Done per task

Done means:

- compiles,
- intended behavior exists,
- relevant checks pass,
- no unrelated scope was added,
- runtime warnings are understood,
- owning docs updated only if contract changed,
- current slice remains playable.
