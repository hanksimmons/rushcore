# 03 — Player Movement & Physics Specification

**Status:** Draft 0.4 — Movement Toy accepted 2026-09-05; §15 holds the accepted baseline  
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
- Prototype steering is camera-relative **WASD**. `W` drives along the view, `A`/`D` steer,
  and `S` / stick-back is a **brake**: it sheds speed along the current heading and never
  drives in reverse (D-076). There is no reverse locomotion.
- `Space` is the shared charge-jump / airborne-slam input. Charge starts only from ground contact; a new airborne press is otherwise unambiguously slam.
- Ground movement is projected onto the usable surface tangent.
- Player collision is a true sphere.
- High-speed player collision must use continuous collision detection (CCD) from the first Movement Toy implementation.
- Because grounded-state logic reads direct-body contacts, configure the player to report a small sufficient number of contacts; do not leave contact reporting at Godot's default zero.
- Keep the player awake (`CanSleep = false`) so a player-controlled body never depends on physics wake-up behavior for responsiveness.
- Do not enable `CustomIntegrator` initially; retain Godot/Jolt's standard gravity/damping integration and shape the direct body state in `_IntegrateForces()`.
- No manual camera rotation in MVP; the camera yaw follows the trajectory (§14, D-072).
- Boost is a semantic action; its physical key may be chosen during implementation and remains configurable.

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

Input within ~30° of straight against the heading has no turn side; it only brakes (negative
alignment). Clear lateral intent (W+A/D, a deflected stick) makes the turn side unambiguous and
the normal lateral-authority rotation applies, so a hairpin is always the player's choice,
never a direction picked by numerical noise.

Steering curve/authority falloff was validated in the Movement Toy (§15, V-002).

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

## 9. Slam, power impact and landing burst

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

### Power impact

Every slam landing is the **power impact** (D-077): the impact-power multiplier, the strongest
landing feedback and the hot flash on the ball. There is no separate "perfect" tier; the
perfect-apex mechanic was prototyped and removed after playtest.

### Landing burst

A **fresh `Space` press at the slam touchdown** fires the landing burst:

- a short tunable window either side of the touchdown instant; a press during the slam is
  buffered and counts if it was inside the window when the ball lands,
- the press is consumed: it never starts a charge and never jumps,
- locomotion speed is set to a tunable fraction of the hard cap **along the current heading**,
- it is a floor: a faster ball is never slowed, direction is never rewritten, the ball stays
  grounded, and the normal drive/steer/cap pipeline continues on the same tick, so the burst reads
  as a seamless surge out of the landing rather than a launch,
- feedback: electric-blue sparks, a mini sonic boom (ground shock ring plus an air-parting bow
  ring ahead of the ball), blue flash and a stretch along travel.

Keep timing detection KISS:

```text
burst =
    slamLandedThisWindow
    && (freshSpacePress || bufferedSlamPressAge <= window)
```

Do **not** build a separate combo/timing subsystem.

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

## 11. Carve / traction action — REMOVED

Prototyped in the Movement Toy as one held action trading speed for tighter control, and
playtested with its steering/drag multipliers pushed well past the defaults. It never felt
impactful, so it was removed on 2026-09-05 (D-007, V-002) along with its tuning, input
binding and skid VFX. The movement vocabulary is steer, charge jump, slam, boost (00 P8).

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
- slam-landing power flash,
- landing-burst flash, sparks and boom rings,
- impact flash.

Never deform collision to match squash/stretch.

## 14. Camera-follow mechanics

Presentation styling lives in `06`. Accepted after playtest (D-072): a **chase camera**.

Mechanically:

- yaw follows the player's flat velocity heading with damping and a turn-rate cap,
- yaw holds below a minimum speed, so a resting ball never spins the view,
- after the heading reverses (wall bounce, backward slide) the yaw is held only while the
  player pushes forward against it, and never longer than a tunable bound; a quick recovery
  therefore never swings the view twice, yet the camera always ends up behind the direction
  of travel. There is no reverse-drive hold because `S` cannot reverse (D-076),
- fixed pitch; no manual rotation; player may adjust baseline zoom within limits,
- focus/look target leads along useful velocity; look-ahead grows with speed and is bounded,
- distance/FOV grow modestly with speed,
- vertical follow is independently damped,
- impact shake is short, bounded to the occlusion margin, and event-driven,
- the camera never clips: focus and lens are floored above the heightfield, and same-frame
  sphere casts from both the focus and the ball toward the camera pull it in instantly and
  ease it back out,
- the original fixed 45° yaw remains as a tuning A/B toggle only.

Camera follows with damping from the player's interpolated transform; it is never parented to
the raw physics transform. Teleports snap the camera and re-aim it along the spawn facing.

## 15. Tuning schema

**Accepted baseline** (Movement Toy playtest, 2026-09-05; the user's final preset
`boost-finetune-final`, promoted verbatim on top of `manual-finetune-punchy`, sub-percent slider
values included, so the preset reads as "compiled defaults"; D-078). **This table is the
authoritative movement baseline.** These are the compiled defaults in `GameplayTuning`; the
runtime panel edits the same values and persists overrides (D-075). Min ground-normal dot 0.499,
camera yaw hold below 2.035 m/s, camera follow damping 4.99/s and terrain wavelength 1.005 are
part of the same promotion.

| Parameter | Accepted | Status |
|---|---:|---|
| Physics tick rate | 60 Hz | ACCEPTED (V-007) |
| Gravity | 39.38 m/s² | ACCEPTED |
| Ground drive accel | 27.99 m/s² | ACCEPTED |
| Ground steering lateral accel | 151.25 m/s² | ACCEPTED (V-001) |
| High-speed steering multiplier | 1.45 at the cap (authority rises with speed; radius = v²/(a·mult)) | ACCEPTED (V-001) |
| Hard max locomotion speed | 148.5 m/s | ACCEPTED (V-004) |
| Landing cap bleed | 39.95 m/s² | ACCEPTED (D-074) |
| Drag coefficient | 0.077 | ACCEPTED |
| Air control multiplier | 0.308 | ACCEPTED (D-078) |
| Ball radius | 2.125 m | ACCEPTED (V-005) |
| Min jump takeoff vertical speed | 2.03 m/s (a bare tap is a hop; the charge is the jump) | ACCEPTED (V-010) |
| Max jump takeoff vertical speed | 58.21 m/s | ACCEPTED (V-010) |
| Max jump charge seconds | 0.445 s, linear | ACCEPTED (V-010) |
| Charge release grace | 0.10 s | ACCEPTED |
| Slam initial downward speed | 42.95 m/s | ACCEPTED |
| Slam downward acceleration | 141.1 m/s² | ACCEPTED |
| Slam steering multiplier | 0.25 | ACCEPTED |
| Slam lateral retention | 1.0 | ACCEPTED |
| Slam impact multiplier | 1.35 on every slam landing | ACCEPTED (D-077) |
| Landing-burst window | ±0.10 s around the slam touchdown | ACCEPTED (D-077) |
| Landing-burst speed | 80% of the cap along the current heading (floor only) | ACCEPTED (D-077) |
| Boost acceleration | 88.64 m/s² | ACCEPTED (V-003, D-078) |
| Boost direction blend | 0.25 | ACCEPTED |
| Boost capacity / drain / passive regen | 100 / 30 per s / 4 per s | ACCEPTED (V-003) |
| Boost pickup refill | 35 | ACCEPTED (toy) |
| Rush / Crush / Overdrive thresholds | 18 / 32 / 141.06 m/s | Overdrive ACCEPTED (D-078); Rush/Crush OPEN (V-004) |

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
- burst window state/result,
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
- the landing burst is learnable and reads as a seamless surge,
- boost increases route possibility,
- mistakes are recoverable,
- high-speed collisions are stable with CCD enabled and do not routinely tunnel through valid collision geometry.

Exact cases are defined in `08_TEST_ACCEPTANCE.md`.

## Empirical validation items

Resolved at Movement Toy acceptance (2026-09-05); see `DECISIONS.md` V-001…V-011. Still open:
the Rush and Crush thresholds relative to the accepted cap (V-004), to be settled when Flow and
combat give the bands a purpose.
