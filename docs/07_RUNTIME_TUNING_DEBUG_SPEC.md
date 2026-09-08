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
- frame band and pitch release damping (the framing pivot, D-090),
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

`World › Prop Density` (default 0.19) scales the placement attempts on a generated stage; the instance caps (1800
rocks, 900 crystals, 240 markers) are a ceiling the default sits well under, so the slider keeps working, and they
bind only past about 0.6. Density changes scenery only: it never moves a stage hash (T4, P-008).

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

The `HUD` category (T2) holds `Visible`, `Scale` and `Band Word`. They are presentation handles, not feel values, and
are not part of the frozen movement baseline.

## 8. Persistence

Debug-only:

- Save override → `user://tuning_override_v1.json` (diff from compiled defaults; applied on launch; state always visible — D-075)
- Named presets → `user://tuning_presets/<name>.json` (stashes for comparison)
- Load override
- Reset category
- Reset all

Compiled defaults remain clean-build authority.

## 9. Seed/debug tools

Required eventually (the seed row reads `run/stage` since T1: the run seed and the stage index inside it; **Copy
Seed** copies the run seed alone, which is what `-- --seed N` takes):

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
- structures (lid and tube bounds, tube paths and mouths),
- floors and drains,
- player ground normal/contact (contact, raw, ground follow),
- impact vector.

Disabled by default. `World › Sample Stage` picks a named archetype and seed from the runbook's sample list (the G0
manual sample from the panel). Delivered (D-104): `World › Route Debug Lines` (route, optional lines by floor, checkpoints,
tube axes, terminal lines in their own colour; on by default in the toy) and `World › Stage Debug Views` (corridor
bounds, challenge zones, structure bounds, floors and drains, exit pad rings (D-105); off by default). Player contact
and impact vectors remain in the telemetry rows. `World › Enemy Showcase` (T3, off by default) stands a lab row 40 m
beside the calibration lane carrying one of each enemy, one elite, one of each pickup shape and a reward-burst pad, so
the silhouettes can be judged at the cap; it is lab terrain only, never the strip and never a generated stage, and
toggling it rebuilds the world. The `stage` row names the exit reached, leads with the stage index
in the run (`3/9`) and shows `(outro)` while the completion sequence plays (T1). The `health` row carries the value
the HUD draws (T2). The developer overlay keeps the numbers; the player HUD (06 §12) carries none of them.

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

Delivered (T3): **Play Crush**, **Play Fail**, **Play Damage** and **Burst 12 Coins**, on the panel. The first two
fire on the showcase row's Pylon and Bulwark when it is up and at the ball when it is not; the third is the player's
own damage pulse; the fourth throws twelve coins at the ball and lets it magnetise them into the run's wallet, which
is also what the row's burst pad does when the ball rolls over it. None of them has a hotkey: the panel is enough for
a cue that is judged by looking at it.

Delivered (T2): **Kill Player** (`K`) takes health to zero, which recovers to the checkpoint and refills on arrival
(P-004); **Heal Player** (`H`) returns it to full. Both are on the panel too. Nothing damages health yet.

Delivered (T1): **Teleport Near Exit** (`E`, or the panel button) drops the ball on the primary 200 m short of exit
A, facing down the route and grounded, and carries stage progress and the armed anchor with it so the tracker never
trails at the start pad. It reaches the completion sequence without driving the whole stage. `Restart Same Seed`
rebuilds the current stage index; `New Seed` starts a new run at index 0.

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
