# T3 — Enemy silhouettes, pickup shapes, reward burst, combat VFX one-shots

**Branch:** `opus/t3-visuals` off `develop-secondary`  
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

- **Branch / commits:** `opus/t3-visuals` off `develop-secondary` (f2e0774, with T1, T2, T4 and the tube fixes merged).
- **Harness (full run, count and wall time):** **386/386 default (110.5 s)**, **canyon 382/382**, **dunes 386/386**,
  **sky 387/387** — all four green, including the T8 tube-camera guard that flaked on the base branch (B-1). Golden
  hashes unchanged: `[tube 7D24840659B28767] [tunnels + pit 327D9152EADB87E0] [sky floor 3 C453C38B4481CEB3]
  [dune trains 1903C69DC8E57D46] [gap + turns 3CA4CC5E70D0F945] [three exits DE6A618E62DEB3EE]`. The archetype runs
  were not required by this packet but were run anyway, since the exit marker lands on every generated stage's pads.
- **Files changed:** `src/Vfx/PlaceholderPalette.cs`, `src/Vfx/WorldVfx.cs`, `src/Enemies/EnemyVisuals.cs`,
  `src/Pickups/PickupVisuals.cs`, `src/Pickups/RewardBurst.cs` (all new), `src/Player/PlayerVfx.cs` (`PlayDamage`),
  `src/World/WorldDressing.cs` (the showcase row, the exit marker, the exit one-shot), `src/World/MovementToyWorld.cs`
  (the VFX node, `Showcase`, `RewardPadTriggered`, `BuiltShowcase`), `src/Core/GameBootstrap.cs` (the four debug
  actions, the burst wiring, the showcase screenshot frame), `src/Core/IDebugActions.cs`, `src/DebugUi/TuningPanel.cs`,
  `src/Tuning/GameplayTuning.cs` (`World › Enemy Showcase`), `tests/MovementToySelfTest.cs`, docs 06 §8, §9, §10,
  07 §11, §12, 10, this packet, `STATUS.md`, `PROVISIONAL_DECISIONS.md`, `OPEN_DECISIONS.md`.
- **What was built (shapes and final sizes per archetype and pickup):**
  - **`PlaceholderPalette`** — twelve shared materials and the unit meshes the whole placeholder language draws
    from, including a real tetrahedron built from the alternating corners of a cube. Nothing here is per instance:
    the showcase row measured **45 draws off 12 materials**.
  - **Enemies** (`EnemyVisual`, metres against the 0.66 m ball): **Pylon** 0.5 m prism, 2.6 m tall, pale cool body
    under one lit cap. **Bulwark** 3.2 × 1.8 × 1.6 m dark block on planted feet, leaning 8° into the line, one
    hazard chevron band proud of the face. **Strider** a 2.4 m body 1.6 m up on two blade legs with a small head at
    one end, its long axis across the travel line, bobbing on a code timer. **Shooter** a 1.2 m core on a 1.1 m
    pillar with a barrel cone along its facing, an orientation ring in the aim colour, and the projectile shape it
    never fires. **Elite** the same silhouette at 1.3× under a rotating halo and crown.
  - **Pickups** (`PickupVisual`): boost ring (unchanged shape, 3.1 m outer), currency a 1.2 m hexagonal coin, reward
    a 1.6 m spinning tetrahedron, item a 1 m cube inside a thin twelve-bar frame, exit a 14 m pillar pair 18 m
    across with a lintel, shortcut a 5 m post carrying two hazard chevrons.
  - **`RewardBurst`** — one node and one `MultiMesh` per burst, per-coin state in plain arrays, P-007's arc and
    magnet, `Collected(kind, amount)` per coin, self-freeing on the last coin or at 6 s.
  - **`WorldVfx`** — a fixed pool of eight slots, each an emitter plus a ring, serving crush, failed impact, pickup
    and exit. Firing swaps a prebuilt process material in and restarts: no allocation, no node. `PlayerVfx.PlayDamage()`
    carries the damage cue, which is player-local.
  - **`World › Enemy Showcase`** — the lab row 40 m left of the spawn, running back down the lane at 14 m spacing:
    four enemies, an elite Pylon, six pickup shapes and a burst pad whose trigger throws twelve coins into the run's
    wallet. Panel actions **Play Crush**, **Play Fail**, **Play Damage**, **Burst 12 Coins**.
  - **Generated stages** gained exactly one thing, the packet's sanctioned exception: every exit pad now stands its
    pillar pair beside the EXIT sign, and the completion pulse fires the exit one-shot off the pad.
  - **Screenshot**: `--rushcore-screenshot` turns the row on after its last frame, parks the ball opposite the middle
    of it and captures `rushcore_09_showcase.png` under the ground-visibility guard.
- **One bug found in play and fixed (the user, 2026-09-08):** the first cut compared `World › Enemy Showcase` against
  what the last build actually produced, in `MovementToyWorld.MatchesTuning`. The row is lab-only, so with the toggle
  on and a generated stage loaded — which is what the saved tuning override launches — the world could never match
  its tuning and rebuilt itself **every frame**: 65 stage builds at ~2.5 s each, and the screenshot run dropped to
  1 fps. The toggle is now compared only where it can be honoured (`wantStage || wantStrip || …`), and the harness
  gained "asking for the showcase row on a generated stage rebuilds nothing" to hold it there. The same cause made
  the screenshot's showcase frame capture a stage instead of the row; the capture now forces the lab.
- **Deviations from the packet and why:**
  - **A fifth new file, `src/Vfx/PlaceholderPalette.cs`.** The packet expected four. Enemies and pickups share the
    chassis and hazard materials, and CLAUDE.md forbids scattering duplicate constants; a pickup class reaching into
    an enemy class for its dark metal is worse than one small palette both read. It is also what keeps the row inside
    the packet's twelve-material budget.
  - **The elite's "warmer emissive" is the halo, not a warmed body.** Warming each role's body would have cost four
    more materials for one modifier and split the treatment across the archetypes, which 02 §7 rules out
    ("no parallel elite architecture"). Recorded in P-017.
  - **No screen-space flash on the failed impact.** The packet puts it "on the player's VFX, not here", but
    `PlayerVfx` is a `Node3D` and a screen-space element would mean adding to the HUD layer, which is T2's. The
    world half (the dull puff) is delivered and the damage cue is player-local; a screen flash, if it is wanted, is a
    HUD change and belongs with whoever owns the HUD.
  - **The Strider is turned 90° in the row** so its long axis lies across the lane's travel line rather than facing
    the runway with the others. That is the whole point of the silhouette (02 §7), and facing only matters for the
    Shooter's barrel.
  - **Sizes were not tuned at the cap.** The packet asked the agent to settle them on the runway; the harness is
    headless and no agent plays stages, so the packet's own starting points shipped unjudged. Recorded as A-7.
- **Measurements (material counts, node counts, burst timings):**
  - Showcase row: 45 mesh draws off **12** unique materials; nearest part of the row **40 m** from the lane centre
    against a 24 m corridor half width; **0** `CollisionShape3D` and **0** `PhysicsBody3D` under any enemy or
    pickup; the burst pad is one `Area3D` trigger and nothing solid.
  - Crush on the Pylon: one pool slot active, the body hidden, and at 2 s the slot free, the body back and the node
    count exactly the row's baseline. Failed impact and damage together added **0** nodes.
  - Reward burst, ball parked 5 m away: **12 of 12** collected inside 3 s, all twelve in the run's wallet and on the
    HUD, the burst freed and the node count back.
  - Reward burst, thrown 30 m beside a ball at **73 m/s**: **0 of 12** collected, freed at its 6 s limit, no leak.
    The magnet's 60 m/s ceiling cannot catch a ball near the 148.5 m/s cap — see A-8.
  - Toggling the row off returns the node count to the pre-row baseline exactly.
  - `--rushcore-screenshot` end to end: **7 s wall, 2 world builds** (see the bug below for what that was before).
- **P-entries written:** P-017 (the delivered visual language, its sizes and the twelve-material palette). P-006 and
  P-007 are implemented as written, both marked delivered with the notes above.
- **Spec sections edited:** 06 §8 (the delivered silhouettes and sizes), 06 §9 (the delivered pickup shapes and the
  burst), 06 §10 (the delivered one-shots and where each lives), 07 §11 (`World › Enemy Showcase`), 07 §12 (the four
  panel actions), 10 (the showcase row, the panel buttons, the exit marker, the screenshot's last frame).
- **Open items:** both are the user's read and both are in `OPEN_DECISIONS.md`. **A-7** — the four silhouettes have
  never been seen at the cap, so the packet's sizes are unjudged. **A-8** — a burst thrown beside a ball at speed
  collects nothing, because P-007's magnet is 60 m/s against a 148.5 m/s cap; the constraint that follows is that
  Phase 4 must spawn a burst on the ball's line. The screenshot's showcase frame is delivered and its
  ground-visibility guard passes (luminance std 0.146), but composing a static frame of a twelve-figure row from a
  chase camera 18.8 m off the ground is a compromise: the row reads across the frame at about 60 m, which is small.
  It is a review aid, not an acceptance gate.
- **Needs main track:** nothing new. T3 touched no generator rule, no validator, no `WorldScale` number and no route
  topology; the golden hashes are unchanged. The exit marker is dressing, drawn from stage data that was already
  there.
