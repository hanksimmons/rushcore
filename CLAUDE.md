# CLAUDE.md — RUSHCORE Agent Instructions

**Status:** Draft 0.3 — final audited  
**Applies to:** Claude Code or any coding agent working in this repository

## Mission

Implement the RUSHCORE MVP defined by the repository documentation while preserving:

- movement-first game feel,
- KISS,
- YAGNI,
- deterministic procedural generation,
- Godot .NET 4.7.2 best practices,
- clean C# ownership,
- programmatic runtime construction,
- rapid runtime tuning.

## Read order

Before meaningful changes:

1. `README.md`
2. `docs/00_GAME_VISION.md`
3. `docs/01_MVP_SCOPE.md`
4. owning system spec(s)
5. `docs/05_TECHNICAL_ARCHITECTURE.md`
6. `docs/08_TEST_ACCEPTANCE.md`
7. current phase in `docs/09_IMPLEMENTATION_PLAN.md`
8. `docs/DECISIONS.md`

This file is an operating contract, not the product specification.

## Authority/conflicts

If documents conflict:

1. do not invent a compromise,
2. use the hierarchy in `README.md`,
3. preserve higher-authority intent,
4. report the conflict,
5. do not silently change accepted gameplay to simplify implementation.

## Engine/platform

- Godot .NET 4.7.2.
- C#.
- Development machine: macOS arm64 / Apple Silicon.
- Prefer Godot built-in node/resource types when appropriate.
- When uncertain about engine API/behavior, consult the official Godot documentation for the applicable stable/4.7 API before inventing a workaround.

## Critical player contract

The Movement Toy must implement these accepted behaviors:

- real 3D rigid-body physics with arcade authority,
- WASD camera-relative steering,
- S / stick-back is a brake only; there is no reverse drive (D-076),
- tight low-speed steering and broader high-speed arcs,
- hard tunable max playable locomotion speed,
- downhill slope acceleration,
- player propulsion on flat/uphill terrain,
- Space press/hold while grounded begins jump charge,
- a fresh Space press while airborne never starts a new charge,
- Space release jumps,
- hold duration maps min→max vertical takeoff speed,
- charge changes steering authority only: it does not reduce normal longitudinal drive, terrain acceleration, boost availability, or otherwise impose artificial slowdown,
- if ground is lost while already charging, a short release grace may preserve the jump,
- **no steering while charging**,
- preserve useful horizontal momentum at takeoff,
- no double jump,
- new Space press while airborne slams,
- slam preserves lateral momentum and commits downward,
- every slam landing is the power impact (stronger impact and feedback than a plain fall),
- a fresh Space press at the slam touchdown, inside a short tunable window (early presses during the slam are buffered), fires the **landing burst**: blue sparks, a mini sonic boom with an air-parting ring, and locomotion speed multiplied by a tunable factor (1.0–1.3) along the current heading, limited by the effective cap (D-088); it never slows the ball, never changes direction, and the press never starts a charge,
- **Flow headroom** (D-088): the hard cap is the base cap; the effective cap is base × (1 + Flow × headroom). Flow is gained by perfect actions (burst, slam landing, charged jump; later crushes and challenge lines), lost by mistakes (brake, hard impact, hard landing without a slam, recovery) and never by time while a chain is alive. Steering saturates at the base cap. With headroom 0 the controller is the frozen baseline,
- there is no perfect-apex mechanic: it was prototyped and removed after playtest (D-077),
- landing-burst logic stays simple and local (two timers, one window),
- boost works in air,
- boost direction blends current travel and desired input,
- slow emergency passive boost refill plus active refill hooks,
- **carve** (D-089; the D-007 removal covered a different, speed-costing verb): hold Left Alt / LB while grounded above a minimum speed; the facing swings toward the stick at a tunable yaw rate while the velocity understeers wide; release (or ground loss) re-aims the velocity along the facing at no less than the entry speed; the camera tracks the facing; a real turn grants Flow.

Do not reinterpret charge jump as “jump on press with variable gravity.” Jump occurs on **release**.

## Charge-jump KISS rule

Use the smallest maintainable model.

A simple hold timer and linear interpolation between min/max vertical takeoff speeds is the initial implementation.

Do not add:

- animation timelines as gameplay authority,
- rhythm/combo subsystem,
- complicated jump-curve framework,
- multiple charge stages/classes,
- separate “landing-burst manager.”

The landing burst is detected from time since slam touchdown and the age of a press buffered during the slam; nothing more.

## Programmatic-content rule

Minimize editor-authored gameplay composition.

The coding agent may create required Godot/.NET bootstrap files.

Gameplay/runtime content should be constructed/configured in C#:

- player,
- camera,
- terrain,
- enemies,
- pickups,
- UI,
- procedural meshes,
- materials/shaders,
- lighting/environment,
- particles/VFX.

Do not add editor-authored content files merely out of convention.

## KISS

Prefer:

- explicit ownership,
- small concrete classes,
- simple data,
- direct composition,
- readable equations,
- a small movement state model.

Avoid:

- DI frameworks,
- service locators,
- generic event buses without need,
- generalized factories for a handful of types,
- speculative abstraction,
- premature ECS,
- reflection-heavy tuning systems.

## YAGNI

Do not implement future systems before their phase.

No:

- multiplayer hooks,
- mod framework,
- audio engine now,
- endless streaming,
- generalized save migration,
- broad pooling,
- speculative content pipeline.

## Physics

Current accepted direction:

- `RigidBody3D`,
- spherical primitive collider,
- `_IntegrateForces()` for controller shaping,
- real gravity/collision,
- hard locomotion speed cap using the canonical grounded-tangent / airborne-horizontal decomposition from `03`,
- `CanSleep = false` on the player-controlled rigid body,
- continuous collision detection enabled on the high-speed player,
- `CustomIntegrator` left disabled initially so standard Jolt gravity/damping remain active,
- contact monitoring plus a small sufficient contact-report budget for direct-state ground detection,
- the analytic ground follow (03 §3, D-092): the controller reads the terrain grid under the ball and, where the surface could physically carry it (v²κ below gravity), removes the outward velocity and counts the ball as grounded, so the collider's facets never hop it; launches stay real; a velocity rule only, toggle off = baseline,
- `HeightMapShape3D` main terrain,
- structures (D-096): lids as `BoxShape3D`, tubes as inward-facing `ConcavePolygonShape3D` with backface collision, on a structure layer; the ground follow reads the terrain heightfield only and the contact baseline carries the ball on a structure.

Never repeatedly set the rigid body's transform each frame to fake movement.

After any checkpoint/debug/stage teleport of an interpolated moving branch, reset physics interpolation after setting the new transform.

Start at 60 physics Hz. Only test/raise to 120 if measured high-speed behavior requires it.

## Procedural generation

- deterministic explicit seeds,
- gameplay/route structure before noise,
- broad directional stage,
- guaranteed primary route,
- 1–3 meaningful optional lines,
- mandatory traversal cannot require a stocked boost meter,
- one logical height source for render/collision,
- the ground is one single-valued heightfield; walls, terraces, slots and spirals are stamps in it; lids and tubes are the only structures (04 §5I, D-096); never a second height layer,
- bounded validators/regeneration,
- regression seeds for fixed failures.

Do not solve validity with unconstrained noise + endless retries.

## Presentation

Placeholder art must still be:

- distinctive,
- readable at speed,
- low-poly/stylized,
- coherent,
- efficient.

Charge state, the slam power impact and the landing burst require clear player-local visual feedback.

Normal HUD does not need raw m/s; debug overlay does.

## Runtime tuning

Core feel values must be centralized and adjustable through developer UI.

Expose important charge/slam parameters:

- min/max jump takeoff,
- charge duration,
- burst window / burst speed fraction,
- slam impact multiplier,
- speed cap,
- steering curve,
- boost values.

Do not scatter duplicate constants.

## Change discipline

Before coding:

- inspect existing code,
- identify owning spec,
- plan smallest change for current slice.

While coding:

- keep edits local,
- do not prebuild later phases,
- do not add architectural scaffolding “for someday.”

After coding:

- build,
- run relevant checks,
- perform/describe acceptance gate,
- report changed files,
- report empirical tuning findings,
- update owning docs only if accepted behavior changed.

## Current phase

Until the implementation plan advances:

> **Phase 2C — Flow headroom** (D-088), inserted after Gate G0's objective closure (D-087) and before
> Phase 3. The preset is locked (D-091 + D-095: headroom 0.715, ball 0.66 m, steering 222.66, jump
> 84.63, slam 90 + 250, carve 84°/s, camera), the analytic ground follow is in (D-092) and the ceiling addendum
> (D-094: two-speed generation, the model's airborne phase, `docs/11 §6`) is delivered. Phase 3 opens at
> Gate F0 with the G0 manual sample played with Flow on, and carries the vertical grammar (D-096: walls,
> terraces, spiral pits, lids, see-through tubes, Sky Terraces; `docs/04 §5I`, `docs/11 §7`).

The accepted movement baseline in `docs/03 §15` and `DECISIONS.md` (D-095: the user's `manual-small-3` preset, D-091 plus the slam and VFX overrides, promoted verbatim on 2026-09-06 on top of D-078) is frozen input to generation. Do not retune it; a change only enters through a saved preset that the user names final, promoted verbatim and logged. Flow headroom (D-088) is part of that baseline (0.715); with headroom 0 the controller is the base-cap kit.
