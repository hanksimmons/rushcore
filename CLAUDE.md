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
- slam near vertical apex receives a stronger perfect-apex bonus only when the current airborne arc was created by the player's jump,
- generic falls/external launches are not perfect-apex eligible,
- perfect-apex logic stays simple and local,
- boost works in air,
- boost direction blends current travel and desired input,
- slow emergency passive boost refill plus active refill hooks,
- carve is a prototype/validation feature, not a guaranteed permanent verb.

Do not reinterpret charge jump as “jump on press with variable gravity.” Jump occurs on **release**.

## Charge-jump KISS rule

Use the smallest maintainable model.

A simple hold timer and linear interpolation between min/max vertical takeoff speeds is the initial implementation.

Do not add:

- animation timelines as gameplay authority,
- rhythm/combo subsystem,
- complicated jump-curve framework,
- multiple charge stages/classes,
- separate “perfect slam manager.”

Perfect apex can be detected from the player's vertical velocity against a forgiving tunable threshold.

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
- `HeightMapShape3D` main terrain.

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

Charge state and perfect-apex slam require clear player-local visual feedback.

Normal HUD does not need raw m/s; debug overlay does.

## Runtime tuning

Core feel values must be centralized and adjustable through developer UI.

Expose important charge/slam parameters:

- min/max jump takeoff,
- charge duration,
- apex threshold,
- apex slam strength/impact,
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

> **Bootstrap → Movement Toy → Scale Calibration.**

The first real success criterion is:

> The ball, terrain, charge jump, slam, boost, camera, and optional carve are fun for ten minutes with no roguelike progression.
