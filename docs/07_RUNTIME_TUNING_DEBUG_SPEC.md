# 07 — Runtime Tuning & Debug Specification

**Status:** Draft 0.3 — final audited  
**Authority:** Canonical developer-facing runtime tuning/debug behavior

## 1. Goal

Highest-risk feel parameters must be editable **during play** without source edits or Godot editor work.

This is a development tool, not a shipping settings UI.

## 2. Panel behavior

- toggleable instantly,
- mouse interaction available while open,
- support pause-while-editing plus live mode if useful,
- generated in C# using Godot `Control` nodes.

Do not build a reflection-heavy generic property editor initially.

## 3. Core Feel parameters

Expose first:

- Gravity
- GroundDriveAcceleration
- GroundSteeringAuthority / high-speed falloff
- HardMaxLocomotionSpeed
- AirControlMultiplier
- MinJumpTakeoffVerticalSpeed
- MaxJumpTakeoffVerticalSpeed
- MaxJumpChargeSeconds
- ChargeReleaseGraceSeconds
- SlamDownwardAcceleration/Speed
- SlamSteeringMultiplier
- SlamImpactMultiplier
- LandingBurstWindowSeconds
- LandingBurstMultiplier
- Carve min speed, yaw rate, understeer, Flow gain and its minimum turn (D-089)
- Flow headroom, gains (burst, slam landing, charged jump), losses (brake, impact, plain landing) with their thresholds, chain window, idle decay (D-088)
- BoostAcceleration
- BoostDirectionBlend
- BoostCapacity
- BoostDrainRate
- PassiveBoostRegen
- active refill values
- Rush/Crush/Overdrive thresholds

## 4. Camera tuning

Expose:

- baseline distance,
- pitch,
- yaw,
- look-ahead min/max,
- follow damping,
- vertical damping,
- FOV min/max,
- speed-distance response,
- shake strengths/decay.

Camera and movement are tuned together.

## 5. Generation tuning

Expose only macro handles initially:

- terrain amplitude,
- terrain feature wavelength/scale,
- noise amplitude/frequency,
- corridor width,
- challenge density,
- enemy density,
- prop density.

Do not expose every internal constant.

## 6. VFX tuning

- player charge effect strength,
- jump release effect,
- trail length/intensity,
- dust,
- slam effect,
- landing-burst effect,
- carve debris effect,
- impact effect,
- motion-effect intensity.

## 7. Central tuning ownership

Conceptual model:

```text
GameplayTuning
├── Movement
├── JumpSlam
├── Boost
├── Combat
├── Camera
├── Generation
└── Vfx
```

Gameplay reads these values; debug UI edits the same source.

No duplicate magic-number copies.

## 8. Persistence

Debug-only:

- Save override → `user://tuning_override_v1.json` (diff from compiled defaults; applied on launch; state always visible — D-075)
- Named presets → `user://tuning_presets/<name>.json` (stashes for comparison)
- Load override
- Reset category
- Reset all

Compiled defaults remain clean-build authority.

## 9. Seed/debug tools

Required eventually:

- show run/stage seed,
- restart same stage seed,
- generate new seed,
- copy seed,
- regenerate without restarting whole application.

## 10. Telemetry overlay

Display as useful:

- FPS/frame time,
- physics tick setting,
- full velocity,
- useful speed,
- hard speed cap,
- speed band,
- grounded,
- jump charge time/normalized,
- computed takeoff speed,
- vertical velocity,
- burst window state/result,
- boost,
- health,
- Flow,
- position,
- current seed/archetype,
- validation status,
- generation timing.

## 11. World debug visualization

Toggles:

- primary route,
- corridor bounds,
- checkpoints,
- challenge zones,
- spawn anchors,
- invalid slopes/regions,
- player ground normal/contact,
- impact vector.

Disabled by default.

## 12. Debug actions

Useful controls:

- restart same stage,
- new seed,
- kill/heal player,
- refill boost,
- invulnerability,
- teleport checkpoint/near exit,
- clear enemies,
- pause,

These intentionally bypass normal game rules to accelerate iteration.

## 13. Shipping behavior

Debug tuning/cheats disabled or unreachable in release builds.

No anti-cheat work for local developer controls.

## 14. Acceptance

A developer must be able to:

1. launch the Movement Toy,
2. alter steering/speed cap/jump charge/slam/boost/camera feel,
3. immediately feel the change,
4. reset values,
5. reproduce the same seed/environment,
6. do all of this without using the Godot editor for scene composition.
