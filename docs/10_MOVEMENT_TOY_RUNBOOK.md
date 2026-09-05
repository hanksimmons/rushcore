# 10 — Movement Toy Runbook

**Status:** Working document — implementation notes, not product specification
**Authority:** None over design. Describes how to run and playtest the Phase 1 Movement Toy.

## Build / run

```sh
dotnet build                                                    # offline: Godot nupkgs from the app bundle
/Applications/Godot_mono.app/Contents/MacOS/godot --path .      # play (or open in the Godot editor and press Play)
/Applications/Godot_mono.app/Contents/MacOS/godot --headless --path . -- --rushcore-selftest    # Gate M0 objective checks
/Applications/Godot_mono.app/Contents/MacOS/godot --path . --resolution 1280x720 -- --rushcore-screenshot  # PNGs to user://
```

The self-test drives the real controller with synthetic input on an isolated rig plus the
calibration terrain and exits non-zero on any failure. It does not judge feel.

## Controls

| Input | Action |
|---|---|
| W A S D / left stick | camera-relative steering. The camera follows the trajectory, so **W keeps going, A/D turn, S brakes/reverses**. Spawn faces down the lane (−X). |
| Space / A | press on ground = charge; release = jump; new press in air = slam |
| Shift / X | boost (ground and air) |
| Ctrl / LB | carve prototype (held) |
| Mouse wheel, + / −, D-pad up/down | camera zoom (bounded) |
| F1 | tuning panel (mouse works while open; "Pause while editing" checkbox) |
| F2 | telemetry overlay |
| F3 | carve on/off (A/B) |
| F4 | physics 60 ↔ 120 Hz (V-007) |
| F5 | new world seed (instruments are seed-invariant; only scenery hills change) |
| R | recover to last checkpoint |
| T | teleport to spawn |
| B | refill boost |

Falling below the kill plane recovers automatically. Recovery resets physics interpolation.

## Tuning baseline rule

Compiled defaults in `src/Tuning/GameplayTuning.cs` mirror `03 §15` and are the comparison
baseline. An override file (`user://tuning_override_v1.json`) is **never applied at startup**;
load it deliberately from the panel. Reset All returns to compiled defaults.

Deliberate deviations from the `03 §15` placeholders, made at gap closure and to be judged in
playtest:

- **Apex window is 0.20 s** (`Jump › Apex Window (s)`), i.e. |vY| ≤ 2.80 m/s at g = 28. The
  detection is still a vertical-speed threshold (D-017); the knob is in seconds so retuning
  gravity cannot silently shrink the window. The spec placeholder 1.25 m/s equals 0.089 s.
- **Landing Cap Bleed = 40 m/s²** (new, `Move`). Landing on a slope at the cap makes tangent
  speed exceed the cap by 1/cos(slope) (D-069); the excess now bleeds over ~0.2 s instead of
  clipping in one tick. Set very high to restore the instant clamp.
- **Camera occlusion probe** (`Camera`, on by default): sphere-cast from focus to camera pulls
  the camera in behind berms/mesa faces; disable to compare.
- **Max Visual Roll = 3 rev/s** (`VFX`): visual-only spin cap so the ball does not strobe at speed.
- **Chase camera** (`Camera › Follow Trajectory Yaw`, on): yaw tracks the flat velocity heading
  (damping 3/s, ≤140°/s, holds below 3 m/s, never flips on reverse). Turning it off restores the
  fixed 45° isometric-like composition for A/B. **This contradicts D-058/D-059, 03 §14 and 06 §11
  as written**; it is the playtest-directed baseline pending reconciliation at acceptance.
- Clipping defence: focus floored above ground; two same-frame sphere casts (focus→camera and
  ball→camera); shake bounded to the probe margin; lens floored above the heightfield
  (`Camera › Ground Clearance`).

## Calibration terrain (seed-invariant)

Terrain spans ±512 m, 4 m facets. Spawn on the flat plain at (470, 380).

| Instrument | Where | Measures |
|---|---|---|
| Calibration lane | z = 380, x 450 → −50, banded every 10 m, posts every 50 m, gantries every 100 m | acceleration, top speed, stopping, thin-post collision |
| Spawn pillars | just north of the lane start, 5 / 10 / 20 / 40 m tall | vertical scale, jump height |
| Grade fan | west (x −450 / −300 / −150), crest z = 150, drops 45 m | 8° / 15° / 25° downhill acceleration; boost rings on the 15° lane |
| Climb + mesa | x 0..225 from z = 130 up 12° to +25.5 m | uphill propulsion |
| Launch ramps | off the mesa lip at x 40 / 112 / 184 (11° / 19° / 27°) | charge jump, apex slam, landings |
| Chasms | south rim z = −220 (34 m) and z = −380 (58 m) | gap jumps; 25° exit wall |
| Bowl | centre (340, −320), r 160, floor −48 m | momentum storage |
| Banked hairpin | centre (330, 155), r 145, berm +18 m; boost rings on the line | high-speed banked turns, carve |

Rocks, crystals, pylons, posts and pillars are solid (thin-object / CCD targets).
