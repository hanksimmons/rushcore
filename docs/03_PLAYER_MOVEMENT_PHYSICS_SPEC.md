# 03 — Player Movement & Physics Specification

**Status:** Draft 0.3 — final audited  
**Authority:** Canonical player locomotion, jump/slam/boost behavior, collision feel, camera-follow mechanics, and movement tunables

## 1. Implementation model

**Accepted direction:** `RigidBody3D` with a spherical primitive collision shape, controlled through `_IntegrateForces()`.

The controller is **arcade physics**, not pure torque-driven marble locomotion.

Physics supplies:

- gravity,
- collision detection,
- airborne trajectories,
- surface interaction,
- external impulses.

The controller deliberately shapes:

- propulsion,
- steering authority,
- speed cap,
- traction,
- charge-jump behavior,
- air control,
- slam,
- boost,
- collision recovery.

The visible ball roll is presentation-driven and does not need to be a literal mapping of rigid-body angular velocity.

## 2. Coordinate/input conventions

- `Vector3.Up` is world up.
- Prototype steering is camera-relative **WASD**.
- `Space` is the shared charge-jump / airborne-slam input. Charge starts only from ground contact; a new airborne press is otherwise unambiguously slam.
- Ground movement is projected onto the usable surface tangent.
- Player collision is a true sphere.
- High-speed player collision must use continuous collision detection (CCD) from the first Movement Toy implementation.
- Because grounded-state logic reads direct-body contacts, configure the player to report a small sufficient number of contacts; do not leave contact reporting at Godot's default zero.
- Keep the player awake (`CanSleep = false`) so a player-controlled body never depends on physics wake-up behavior for responsiveness.
- Do not enable `CustomIntegrator` initially; retain Godot/Jolt's standard gravity/damping integration and shape the direct body state in `_IntegrateForces()`.
- No manual camera rotation in MVP.
- Boost/carve are semantic actions; physical key choices may be chosen during implementation and remain configurable.

## 3. Ground detection

Inspect contacts during physics integration and identify usable ground by normal.

Conceptual rule:

```text
dot(contactNormal, Vector3.Up) >= MinGroundNormalDot
```

With multiple contacts, derive a stable representative normal rather than trusting one noisy point.

Track only the state required for behavior:

- grounded,
- ground normal,
- jump charge state,
- charge-release grace after losing ground,
- time/charge amount,
- previous/current vertical velocity,
- slam active.

Do not build a large generic locomotion state framework.

## 4. Steering model

**Accepted experience rule:** low-speed control is tight; high-speed turn radius grows substantially with speed.

High speed must never remove steering entirely, but velocity must not snap instantly to input.

Conceptual approach:

1. compute useful/tangent velocity,
2. compute desired camera-relative input,
3. apply propulsion,
4. bias velocity direction toward desired direction subject to lateral-acceleration authority,
5. preserve speed for broadly aligned steering,
6. require broader arcs at high speed.

Useful mental model:

```text
turnRadius ≈ speed² / lateralAcceleration
```

This need not be a physically exact vehicle model.

**VALIDATE:** exact steering curve/authority falloff in the Movement Toy.

### Charge-jump steering lock

While `Space` is held in a valid grounded charge:

- steering authority is **zero**,
- the player continues rolling with its full normal longitudinal movement behavior,
- charging itself never changes drive strength, drag, gravity, slope acceleration, boost availability, or collision response,
- any propulsion/boost acceleration that would normally be applied is constrained to the existing travel heading rather than using WASD to redirect the ball,
- terrain can still accelerate/decelerate the player naturally.

In other words, charge suppresses **line-control authority only**; it is not an implicit brake or alternate locomotion mode.

This is intentional risk/reward: a longer/higher jump costs temporary line-control authority while the world keeps moving under the player.

## 5. Drive and hard maximum speed

Ground drive adds acceleration rather than setting transforms/teleporting velocity direction.

Normal controller behavior must allow:

- easy start from rest,
- meaningful flat-ground acceleration,
- strong downhill acceleration,
- reduced effectiveness on uphill terrain,
- substantially greater acceleration under boost.

### Hard playable speed cap

**Accepted:** use a hard tunable maximum playable locomotion speed.

The cap prevents normal propulsion, gravity-along-slope, and boost from increasing useful travel speed beyond the effective cap.

The cap is a gameplay parameter and may be modified by upgrades later.

To avoid breaking jump/slam behavior, use one explicit decomposition:

- **Grounded:** treat the velocity component tangent to the representative ground plane as locomotion velocity and clamp only its magnitude to `MaxLocomotionSpeed`.
- **Airborne:** treat the world-horizontal (`XZ`) velocity component as locomotion velocity and clamp only its magnitude to `MaxLocomotionSpeed`.
- Preserve the orthogonal/vertical component used by contact resolution, charged-jump takeoff, gravity, and slam.
- Changing between grounded and airborne reference planes must not rotate velocity or create energy; clamp only when the relevant locomotion component actually exceeds the cap.
- Maintain a separate high technical safety limit only if physics stability requires one.

This definition is the canonical meaning of **locomotion speed** elsewhere in the specs.

The cap should be communicated through feel/VFX, not an abrupt visible “wall” if it can be avoided.

**VALIDATE:** actual maximum speed.

## 6. Gravity and slope behavior

Slopes are a primary acceleration mechanic.

- downhill lines materially accelerate the player until the hard locomotion cap,
- flat terrain supports player-driven acceleration,
- uphill travel resists speed through gravity but remains controllable,
- boost helps overcome unfavorable terrain.

Do not cancel slope gravity merely to make steering easier.

Ordinary landing/contact behavior should be deliberately damped enough to avoid uncontrolled bouncing.

## 7. Charge jump

### Input contract

- Press `Space` while grounded → begin charge.
- Hold `Space` → accumulate charge.
- Release `Space` → jump immediately.
- Quick tap → minimum jump.
- Longer hold → higher takeoff vertical speed, up to a maximum.
- Continuing to hold after max charge does not increase power further and does not auto-jump.
- No double jump.

### Simple charge model

Keep implementation intentionally small:

```text
charge01 = clamp(heldSeconds / MaxJumpChargeSeconds, 0, 1)

jumpTakeoffVerticalSpeed =
    lerp(MinJumpTakeoffVerticalSpeed,
         MaxJumpTakeoffVerticalSpeed,
         charge01)
```

Start linear. Do not add charge curves/easing parameters unless playtesting demonstrates a real need.

### Momentum contract

At takeoff:

- preserve essentially all useful horizontal/tangent velocity,
- add/establish the calculated upward takeoff velocity,
- do not penalize speed merely because the player charged.

### Charging tradeoff

Charging is powerful but not always optimal because:

- steering is locked during the hold,
- the player continues moving through the environment,
- late release can produce a poor line.

### Ground-loss grace behavior

Do **not** start a fresh charge from an airborne coyote state; that would conflict with the rule that a new airborne Space press means slam.

If the player leaves the ground **while already holding an active charge**, preserve that charge for a short tunable `ChargeReleaseGraceSeconds`. Releasing Space inside that grace still performs the charged jump. If the grace expires, cancel the charge; a later new airborne Space press means slam.

This keeps input precedence unambiguous while still forgiving a player who rolls off a lip during a committed charge.

### Visual contract

Charge should be readable through player-local presentation such as increasing compression/emissive tension. Avoid adding a mandatory full-screen charge UI unless playtests show it is needed.

## 8. Air control

Air control is weaker than grounded steering but nonzero.

Goals:

- correct a launch,
- line up a landing,
- influence a gap crossing,
- never permit an instant high-speed 180° reversal.

Use the same general desired-direction idea with a separate authority multiplier.

## 9. Slam and perfect-apex timing

### Base slam

A **new press of `Space` while airborne** triggers slam.

Slam:

- preserves nearly all useful lateral momentum,
- immediately commits the player downward,
- applies strong downward velocity/acceleration,
- retains limited air steering,
- creates an offensive attack context,
- quickly reconnects the player with terrain.

Traversal use is first-class:

> Slam onto a favorable descending backside to return to ground early and continue accelerating.

The base slam does not require a large AoE; that remains an item/upgrade hook.

### Perfect-apex slam

A slam initiated near the apex of a **player-triggered charge-jump arc** receives a clear bonus.

Eligibility stays intentionally small:

- a jump was actually released/performed during the current airborne arc,
- slam has not already begun/resolved,
- the player is still in that same airborne arc.

Simply rolling/falling off a cliff, being launched by an external impulse, or otherwise becoming airborne without performing the jump does **not** create perfect-apex eligibility.

Keep timing detection KISS:

```text
isPerfectApex =
    jumpArcEligible
    && abs(verticalVelocity) <= PerfectApexVerticalSpeedThreshold
```

The threshold is deliberately forgiving and tunable; the mechanic should reward timing rather than a single exact physics frame.

A perfect-apex slam may apply:

- stronger immediate downward slam velocity/acceleration,
- higher slam impact multiplier,
- distinct snappy visual feedback,
- small Flow reward.

Do **not** build a separate combo/timing subsystem.

**VALIDATE:** apex threshold and bonus strength.

## 10. Boost

Boost consumes a finite meter while active.

### Direction

Boost direction blends:

- current useful travel direction,
- desired steering/input direction.

At low speed, input can influence the blend more strongly. At high speed, current trajectory dominates.

This prevents boost from becoming an instant trajectory rewrite.

### Air use

Boost works in air.

Aerial boost respects reduced air-direction authority.

### Charge interaction

Boost remains usable during jump charge, but charge steering lock still applies. Any boost acceleration during charge follows the existing travel heading rather than bypassing the no-steer tradeoff.

### Refill

Baseline:

- slow emergency passive regeneration,
- meaningful refill from successful offensive play,
- boost pickups/lines,
- item/upgrade hooks.

**VALIDATE:** exact capacity, drain, passive refill, active refill.

## 11. Carve / traction action

**Status: VALIDATE via prototype.**

Prototype one held action that:

- increases turn authority,
- increases lateral traction/control,
- increases speed loss,
- produces readable skid/dust feedback.

Purpose:

- trade speed for a tighter line,
- provide intentional recovery,
- combine “brake” and “drift/control” into one verb.

If it does not clearly improve line-choice depth, remove it and remove any progression hooks built around it.

## 12. Collision behavior

### World collision

Ordinary terrain contact should preserve Flow.

Environmental crashes:

- primarily cost velocity,
- use bounded deliberate response,
- only severe clearly communicated hazards need health damage.

Avoid prolonged pinball rebounds.

### Enemy collision

`02` owns combat outcome.

Movement contract:

- successful crush cannot let the physics solver destroy forward momentum,
- failed hostile impact applies deliberate velocity loss/deflection,
- no arbitrary multi-second tumbling.

## 13. Visual roll/deformation

The physical collider remains spherical.

`PlayerVisual` derives readable roll from movement/traveled distance and may apply visual-only:

- charge compression,
- jump release snap,
- boost stretch,
- landing squash,
- slam streak,
- perfect-apex flash,
- impact flash.

Never deform collision to match squash/stretch.

## 14. Camera-follow mechanics

Presentation styling lives in `06`.

Mechanically:

- fixed baseline orientation,
- no manual rotation,
- camera follows with damping,
- focus/look target leads along useful velocity,
- look-ahead grows with speed and is bounded,
- distance/FOV may grow modestly with speed,
- vertical follow is independently damped,
- impact shake is short and bounded,
- player may adjust baseline zoom within limits.

**VALIDATE:** exact pitch, yaw, FOV, distance, look-ahead, damping.

Evaluate camera smoothness separately from rigid-body transform inheritance; do not blindly parent camera motion to raw physics transform.

## 15. Tuning schema

Numbers below are **starting calibration values only**. Accepted behavior matters more than these initial values.

| Parameter | Initial placeholder | Status |
|---|---:|---|
| Physics tick rate | 60 Hz | VALIDATE 60 vs 120 |
| Gravity | 28 m/s² | VALIDATE |
| Ground drive accel | 28 m/s² | VALIDATE |
| Ground steering lateral accel | 42 m/s² | VALIDATE |
| High-speed steering multiplier/curve | TBD | VALIDATE |
| Hard max locomotion speed | 60 m/s | VALIDATE |
| Air control multiplier | 0.35 | VALIDATE |
| Min jump takeoff vertical speed | 8 m/s | VALIDATE |
| Max jump takeoff vertical speed | 16 m/s | VALIDATE |
| Max jump charge seconds | 0.65 s | VALIDATE |
| Charge release grace | 0.10 s | VALIDATE |
| Slam downward acceleration | 70 m/s² | VALIDATE |
| Slam steering multiplier | 0.25 | VALIDATE |
| Perfect-apex vertical-speed threshold | 1.25 m/s | VALIDATE |
| Perfect-apex slam strength multiplier | 1.35 | VALIDATE |
| Perfect-apex impact multiplier | 1.35 | VALIDATE |
| Boost acceleration | 48 m/s² | VALIDATE |
| Boost direction blend | 0.25 | VALIDATE |
| Boost capacity | 100 | VALIDATE |
| Boost drain | 30/s | VALIDATE |
| Passive boost regen | 4/s | VALIDATE |
| Carve steering multiplier | 1.8 | VALIDATE FEATURE |
| Carve drag multiplier | 3.0 | VALIDATE FEATURE |
| Rush threshold | 18 m/s | VALIDATE |
| Crush threshold | 32 m/s | VALIDATE |
| Overdrive threshold | 48 m/s | VALIDATE |

Do not create tuning knobs for every intermediate equation. Keep the runtime panel centered on parameters a designer can reason about.

## 16. Required debug telemetry

Expose:

- full velocity,
- useful/tangent speed,
- vertical velocity,
- effective hard speed cap,
- speed band,
- grounded state,
- ground normal,
- jump charge seconds/normalized charge,
- computed jump takeoff speed,
- slam active,
- perfect-apex eligibility/result,
- boost amount,
- input vector,
- current steering authority,
- impact-power estimate,
- checkpoint,
- camera look-ahead.

## 17. Movement acceptance principles

Before progression systems:

- traversal is fun without rewards,
- low-speed control feels immediate,
- high-speed turning requires anticipation,
- hard speed cap does not feel broken/jarring,
- charging creates useful risk/reward because steering is locked,
- quick taps and full charges are both tactically useful,
- jump release preserves momentum,
- air control corrects rather than rewrites trajectory,
- normal slam feels immediate/powerful,
- perfect-apex slam is learnable, forgiving, and noticeably stronger,
- boost increases route possibility,
- carve remains only if it clearly improves play,
- mistakes are recoverable,
- high-speed collisions are stable with CCD enabled and do not routinely tunnel through valid collision geometry.

Exact cases are defined in `08_TEST_ACCEPTANCE.md`.

## Empirical validation items

The design intent is settled; these values/features must be playtested:

1. exact high-speed steering curve,
2. whether carve earns its input,
3. exact passive boost recovery rate,
4. exact playable speed cap/speed thresholds,
5. ball/world scale relationship,
6. camera composition/response,
7. physics tick rate (60 vs 120 if needed).
