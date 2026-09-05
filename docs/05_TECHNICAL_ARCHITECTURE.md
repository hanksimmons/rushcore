# 05 — Technical Architecture

**Status:** Draft 0.3 — final audited  
**Authority:** Canonical runtime structure, ownership, dependency rules, lifecycle, engineering constraints  
**Target:** Godot .NET 4.7.2 / C# / macOS arm64

## 1. Architecture goals

Optimize for:

1. game-feel iteration speed,
2. movement correctness,
3. deterministic procedural generation,
4. runtime tunability,
5. clear ownership,
6. low coupling,
7. adequate measured performance.

Do not optimize for hypothetical multiplayer, modding, infinite worlds, or a large team.

## 2. Bootstrap principle

The coding agent may create the minimum Godot/.NET bootstrap required to run RUSHCORE.

One minimal main scene/root is acceptable. Gameplay/world/UI content is constructed/configured in C# rather than maintained as a large editor-authored scene graph.

Do not spend design effort proving that every last bootstrap byte can be generated dynamically.

## 3. Conceptual runtime ownership

```text
GameRoot
├── RunDirector
├── StageHost
│   └── CurrentStage
├── Player
│   ├── PlayerPhysics
│   ├── PlayerVisual
│   └── PlayerVfx
├── CameraRig
├── UiRoot
└── SharedRuntime
    ├── ProceduralAssetCache
    └── TuningRuntime
```

This is ownership guidance, not a requirement to create wrapper nodes that do no work.

## 4. Dependency direction

Preferred direction:

```text
config/data/math
      ↓
generation + gameplay rules
      ↓
runtime nodes
      ↓
presentation + UI
```

Rules:

- generation data does not depend on UI,
- player physics does not depend on HUD,
- gameplay code does not call audio,
- UI observes state/events rather than owning gameplay state,
- visual nodes do not determine collision outcomes,
- pure generation data holds no unnecessary SceneTree references.

## 5. Communication

No generic global EventBus by default.

Use:

- direct calls for clear ownership,
- explicit references from composition,
- C# events/Godot C# signals for observation.

Introduce broader aggregation only if a concrete coupling problem appears.

## 6. Player architecture

Keep the charge-jump/slam model small.

Conceptual player responsibilities:

### `PlayerPhysics`

Owns:

- rigid-body integration,
- ground state,
- steering/propulsion,
- hard speed cap,
- charge state,
- jump release,
- air control,
- slam/apex timing,
- boost,
- bounded collision response.

A simple internal state model is sufficient, conceptually:

```text
Grounded
ChargingJump
Airborne
Slam
```

These need not become four classes. Prefer flags/small enum plus explicit transition methods if that is clearer.

### Player state/data

Owns:

- health,
- boost amount,
- Flow,
- effective modifiers.

### `PlayerVisual`

Owns:

- generated ball mesh,
- roll,
- charge compression,
- jump/slam deformation,
- speed/emissive presentation.

### `PlayerVfx`

Owns transient particles/effects.

Avoid both extremes:

- one giant `Player.cs` containing the whole game,
- dozens of one-method components for a four-state movement controller.

## 7. Run/stage ownership

`RunDirector` owns:

- run seed,
- stage index,
- run currency,
- temporary progression state,
- route transitions,
- run completion/death.

`StageHost` owns:

- instantiate one selected `StageDefinition`,
- current-stage lifecycle,
- unload,
- completion signal.

Generator creates data; runtime consumes data.

## 8. Data model

Prefer simple C# classes/records/structs for:

- generation requests/results,
- item definitions,
- upgrade definitions,
- route choices,
- score records,
- tuning.

Use Godot `Resource` only when it provides a concrete engine benefit. Do not imitate editor-centric content architecture without need.

## 9. Deterministic random ownership

All gameplay-relevant procedural randomness starts from explicit seeds.

Never let cosmetic RNG consume the same stream that decides geometry/rewards.

Derive named subsystem seeds.

## 10. Physics

Accepted baseline:

- Godot 4.7 3D physics/Jolt default,
- player `RigidBody3D`,
- sphere collision,
- control in `_IntegrateForces()`,
- `HeightMapShape3D` static main terrain,
- physics interpolation,
- start at 60 physics ticks/sec.

**VALIDATE:** 60 vs 120 only if high-speed collision/feel testing demonstrates need.

### Teleport/recovery interpolation rule

Any discontinuous reposition of the player/camera-owned interpolated branch—checkpoint recovery, debug teleport, stage spawn, or similar—must reset physics interpolation immediately after the new transform is established. This prevents interpolation from visually streaking between the old and new positions.

Hard playable locomotion speed is owned by the movement spec and must be enforced consistently.

## 11. Charge jump/apex slam implementation constraint

The mechanic must remain maintainable.

Minimum state/data required:

- charging bool/state,
- charge elapsed,
- min/max takeoff speed,
- max charge duration,
- vertical velocity,
- slam state,
- `jumpArcEligibleForPerfectApex` (or equivalent single boolean),
- apex threshold.

Perfect-apex detection should remain a local movement calculation combining same-jump-arc eligibility with a forgiving vertical-velocity threshold. Do not create:

- rhythm subsystem,
- combo timing framework,
- animation state machine dependency,
- special timeline asset.

## 12. Camera

Separate camera rig reads/interpolates player state.

Applies:

- follow damping,
- velocity look-ahead,
- bounded zoom,
- speed-responsive FOV/distance,
- short shake impulses.

No manual camera rotation in MVP; yaw follows the trajectory (D-072).

Exact values are runtime-tuned.

## 13. Procedural rendering

Preferred tools:

- `ArrayMesh` for generated terrain/custom meshes,
- primitive meshes where sufficient,
- shared `ShaderMaterial`/materials,
- `GPUParticles3D` for transient repeated effects,
- `MultiMeshInstance3D` for high-count repeated static/cosmetic geometry when justified.

Reuse Mesh/Material resources.

## 14. Procedural asset cache

One small cache may own reusable runtime-generated:

- primitive/custom meshes,
- shared materials/shaders,
- particle meshes,
- other immutable procedural resources.

Do not build a generalized asset framework.

## 15. Pooling

Default: no generalized pooling.

Add a specific pool only after profiling a high-churn type such as projectiles.

## 16. Threading

Default: main-thread implementation.

If profiling requires workers:

- compute pure data off-thread,
- keep active SceneTree mutation in controlled main-thread work,
- avoid shared mutable engine collections.

## 17. Persistence

MVP persistence:

- meta progression,
- settings,
- debug tuning override.

Use simple versioned JSON under `user://`.

Avoid generalized migration infrastructure until an actual schema change needs one.

## 18. Input

Gameplay consumes semantic actions.

Prototype contract:

- WASD → steering.
- Space press/hold/release → charge jump.
- Space new press while airborne → slam.

Additional actions:

- boost,
- zoom,
- pause,
- debug tuning/overlay.

Physical mappings for non-Space actions are implementation defaults and can be changed without altering gameplay code.

## 19. Audio-ready events

No audio system now.

Useful semantic events include:

- JumpChargeStarted,
- JumpChargeChanged only if needed for presentation,
- PlayerJumped,
- PlayerSlammed,
- PerfectApexSlam,
- PlayerLanded,
- BoostStarted/Stopped,
- EnemyCrushed,
- PlayerDamaged,
- ItemCollected,
- FlowBandChanged,
- StageCompleted,
- RunEnded.

Avoid high-frequency event spam if direct state reads are simpler.

## 20. Error handling

Fail loudly in debug for broken invariants.

Recoverable procedural failure uses bounded regeneration/fallback.

Do not hide impossible state with silent null checks everywhere.

## 21. Performance philosophy

Profile before optimization.

High-value defaults:

- shared meshes/materials,
- primitive collision,
- heightmap terrain collision,
- bounded physics bodies,
- particles instead of node-heavy debris,
- avoid unnecessary hot-loop allocations,
- no speculative streaming/pooling.

Preserve iteration speed until measured evidence says otherwise.
