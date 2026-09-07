# T3 — Enemy silhouettes, pickup shapes, reward burst, combat VFX one-shots

**Branch:** `opus/t3-visuals` off `develop`  
**Phase:** 4 "basic reward drops with auto-collect", "enemy/pickup VFX" (presentation only); Phase 4's enemies as visuals only  
**Gate lines served:** 08 §9 "enemy archetypes are distinguishable", "reward lines do not read as hazards", "particles do not obscure landing surfaces"  
**Owning specs:** 06 §2, §3, §8, §9, §10, §17; 02 §7 (what each enemy must communicate), §12; 05 §13, §14, §15 (pooling: no); 04 §5F (placement is later)

## Goal

Everything the impact-combat slice will need to *show*, built now so the main track only adds rules: four enemy
silhouettes plus the elite treatment, the pickup shape language, the currency burst-and-magnetise presentation, and
the one-shot VFX for crush, failed impact, damage, pickup and exit. No behaviour, no colliders, no placement in
generated stages.

## Current state

- `src/World/WorldDressing.cs` owns all placeholder scenery: `Flat(colour)` / `Glow(colour, energy)` material helpers,
  `AddMultiMesh`, `AddSign`, the boost ring pickups (`AddPickup` around line 936: a `Pickup` record with `Visual`,
  `Core`, `Collected`, `Respawn`, `Phase`; collection by distance in `_Process`; `BoostPickupCollected` event).
- `src/Player/PlayerVfx.cs` is the pattern for one-shot effects: shared draw materials, `BuildX(material)` builders,
  `FirePendingBursts`, `AnimateBurst`, radius-scaled by `ApplyRadius(r)`; it owns no gameplay authority.
- `src/Player/PlayerVisual.cs`: the ball's look (06 §5), for palette coherence.
- Scale: the ball radius is 0.66 m (D-091); the camera sits 18.8 m back at −20.6°; fog starts far out. Everything is
  in metres; a 2.5 m enemy is about four ball diameters and reads well at the cap. Test sizes on the strip runway at
  the cap before settling them.
- `World › Calibration Strip` and the lab terrain are where instruments live; generated stages get nothing from this
  packet.

## Design

1. **`src/Enemies/EnemyVisuals.cs`**: one file, one static builder per archetype returning a `Node3D` with shared
   materials (one `StandardMaterial3D` per archetype, one for the elite treatment; never per instance):
   - **Pylon** (02 §7 "stationary low-resistance"): an upright, thin prism about 0.5 × 2.6 m, pale cool body,
     bright emissive cap. Reads "safe to ram".
   - **Bulwark** ("stationary high-resistance"): a wide low block about 3.2 × 1.8 × 1.6 m, dark heavy body, one hazard
     chevron band, slight forward lean. Reads "not at this speed".
   - **Strider** ("slowly crosses likely travel lines"): a long low body about 2.4 m across the travel axis on two
     blade legs; a visible long axis so its crossing direction is obvious; a code-driven bob (no animation player).
   - **Shooter** ("simple, slow, readable projectiles"): a 1.2 m sphere core with a clear barrel cone and an
     orientation ring; a placeholder projectile mesh (slow, big, bright) built here but not fired.
   - **Elite treatment** (`SetElite(true)`): the same mesh ×1.3, a rotating halo ring, a warmer emissive. One
     treatment for all four (02 §7 "one constrained modifier", "no parallel elite architecture").
   - each visual exposes `PlayCrush()`, `PlayFail()`, `PlayDamage()` that fire the matching one-shot from `WorldVfx`
     and (for crush) hide the body for 1.5 s then restore, so the lab row can be re-used.
2. **`src/Pickups/PickupVisuals.cs`** (06 §9 shape language, one shape each, one material each): boost (keep the
   existing ring), currency (small flat hexagonal coin, warm), XP/reward (a spinning tetrahedron, cool), item (a cube
   in a thin frame), stage exit marker (a tall arch or pillar pair at the pad: replace the `EXIT` sign's bare text with
   sign plus marker), high-risk shortcut marker (a red-orange twin chevron post). Rewards must not read as hazards:
   no red on reward shapes; hazard tones only on the shortcut marker and the Bulwark band.
3. **`src/Pickups/RewardBurst.cs`**: `Spawn(origin, kind, count, rng)` creates coins as one `MultiMeshInstance3D` (one
   node per burst, not per coin) with per-coin state arrays; code-integrated arcs for 0.4 s, then magnetise toward the
   ball at up to 60 m/s with a small ease, collect within 1.2 m; raises `Collected(kind, amount)` once per coin; frees
   itself when empty or after 6 s (P-007). The ball is never slowed or steered by it.
4. **`src/Vfx/WorldVfx.cs`** (a `Node3D` on the world, `PlayerVfx` style): one-shots for crush (fast expanding shards +
   a flat ring), failed impact (a dull dark puff + a short screen-space flash on the player's VFX, not here),
   damage (a red-edged pulse on the ball: expose `PlayerVfx.PlayDamage()` and call it), pickup (a small rising sparkle),
   exit (a tall ring pulse at the pad; T1 calls it if merged, otherwise fire it from the showcase). Shared materials,
   pre-built meshes, no allocation at fire time beyond a small pool of `N` reusable instances (`N` = 8; 05 §15 allows a
   small pool where a burst is routine).
5. **Lab row `World › Enemy Showcase`** (bool, off by default, `WorldTuning`): on the calibration terrain (not the strip,
   not stages) places one of each enemy plus one elite Pylon, one of each pickup and a reward burst trigger pad in a
   row 40 m left of the spawn, facing the runway. Debug actions in the panel: `Play Crush`, `Play Fail`, `Play Damage`,
   `Burst 12 Coins`. This is how the user judges the shapes at speed.
6. **Screenshot:** add one frame of the showcase to `--rushcore-screenshot` so the user can review from another device.

## Pre-answered choices

- No colliders, no behaviour, no stage placement (P-006). Enemies do not exist outside the showcase row.
- Reward burst is kinematic (P-007).
- Sizes above are starting points; the agent tunes them on the runway at the cap and records what it settled on. The
  main track re-judges with the impact model.

## Non-goals

- The impact model, health, damage, defeat, Flow gains, projectile motion, spawn placement, pooling beyond the small VFX
  pool, sound.
- Any change to `PlayerPhysics`. Any change to generated-stage dressing beyond replacing the exit sign with sign plus
  marker.

## Acceptance (harness)

- With `Enemy Showcase` on: the row builds; every enemy root has zero `CollisionShape3D` descendants; no node of the
  row is inside the runway's corridor half-width (existing "no solid prop in corridor" pattern); unique material count
  added by the row ≤ 12.
- `PlayCrush()` on the Pylon: one `WorldVfx` instance active, the body hidden, node count back to baseline within 2 s.
- Reward burst of 12 coins with the ball parked 5 m away: 12 `Collected` events within 3 s, node count back to baseline
  within 6 s; with the ball at the cap driving past at 30 m lateral, every coin collects or the burst frees itself at
  6 s (no leak either way).
- Node count after toggling the showcase on and off equals the baseline.
- Screenshot frame captured with the ground-visibility guard passing.

Manual (the user): V0 lines above; whether each silhouette says what 02 §7 wants at the cap.

## Files expected

`src/Enemies/EnemyVisuals.cs`, `src/Pickups/PickupVisuals.cs`, `src/Pickups/RewardBurst.cs`, `src/Vfx/WorldVfx.cs`
(all new), `src/World/WorldDressing.cs`, `src/World/MovementToyWorld.cs` (hook), `src/Player/PlayerVfx.cs`
(`PlayDamage`, flash), `src/Core/GameBootstrap.cs` (screenshot frame, actions), `src/Core/IDebugActions.cs`,
`src/DebugUi/TuningPanel.cs`, `src/Tuning/GameplayTuning.cs` (`EnemyShowcase` bool), `tests/MovementToySelfTest.cs`,
docs 06 §8, §9, §10 (delivered shapes and sizes, one line each), 07 §11, §12, 10, this packet, `STATUS.md`,
`PROVISIONAL_DECISIONS.md`.

## Stop conditions

Stop and write "Needs main track" if a shape cannot be made readable without changing the 06 §3 palette or the ball's
visual (06 §5), or if any part seems to need a collider to look right.

## Delivery record (filled by the implementing agent)

- Branch / commits:
- Harness (full run, count and wall time):
- Files changed:
- What was built (shapes and final sizes per archetype and pickup):
- Deviations from the packet and why:
- Measurements (material counts, node counts, burst timings):
- P-entries written:
- Spec sections edited:
- Open items:
- Needs main track:
