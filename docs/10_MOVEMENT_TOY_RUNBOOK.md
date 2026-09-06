# 10 — Movement Toy Runbook

**Status:** Working document — implementation notes, not product specification
**Authority:** None over design. Describes how to run and playtest the Phase 1 Movement Toy.

## Build / run

```sh
dotnet build                                                    # offline: Godot nupkgs from the app bundle
/Applications/Godot_mono.app/Contents/MacOS/godot --path .      # play (or open in the Godot editor and press Play)
/Applications/Godot_mono.app/Contents/MacOS/godot --path . -- --seed 34     # play a named world seed (stage 34/0)
/Applications/Godot_mono.app/Contents/MacOS/godot --headless --path . -- --rushcore-selftest    # objective checks: M0, M1 strip, model calibration, G0
/Applications/Godot_mono.app/Contents/MacOS/godot --path . --resolution 1280x720 -- --rushcore-screenshot  # PNGs to user://
```

The self-test drives the real controller with synthetic input on an isolated rig plus the
calibration terrain and exits non-zero on any failure. It does not judge feel.

## Controls

| Input | Action |
|---|---|
| W A S D / left stick | camera-relative steering. The camera follows the trajectory, so **W keeps going, A/D turn, S brakes** (never reverses, D-076). Spawn faces down the lane (−X). |
| Space / A | press on ground = charge; release = jump; new press in air = slam; **press again at the slam touchdown = landing burst** (±0.10 s; a press during the slam is buffered; ×1.15 of your speed, D-088) |
| Shift / X | boost (ground and air) |
| Left Alt / LB | **carve** (hold): the ball drifts wide while its facing swings toward the stick; release to bite off along the facing at no less than your entry speed (D-089) |
| Mouse wheel, + / −, D-pad up/down | camera zoom (bounded) |
| F1 | tuning panel (mouse works while open; "Pause while editing" checkbox) |
| F2 | telemetry overlay |
| F4 | physics 60 ↔ 120 Hz (V-007) |
| F5 | new world seed (instruments are seed-invariant; only scenery hills change) |
| R | recover to last checkpoint |
| T | teleport to spawn |
| B | refill boost |

Falling below the kill plane recovers automatically. Recovery resets physics interpolation.

## Tuning baseline rule

Compiled defaults in `src/Tuning/GameplayTuning.cs` mirror `03 §15` and are the comparison
baseline. Slider changes live in memory until you press **Save Override**, which writes only
the values that differ from defaults to `user://tuning_override_v1.json`
(`~/Library/Application Support/Godot/app_userdata/RUSHCORE/`). That file is **applied
automatically on the next launch**. The panel header and the telemetry `tuning` row show
`OVERRIDE ACTIVE — n values differ`, and modified rows are highlighted, so you always know
whether you are on the baseline. **Reset All** returns to compiled defaults (save again to
clear the file's contents). The self-test always runs on compiled defaults.

**Presets** (F1 › PRESETS): type a name and press Enter / **Save As** to stash the current
values as `user://tuning_presets/<name>.json`; pick one from the dropdown and **Load** to apply
it live for comparison; **Delete** removes it. Presets are stashes, not the startup state —
after loading the one you prefer, press **Save Override** to make it load on launch. Typing in
the name field pauses the game and suspends hotkeys.

Starter presets ship in `tuning/presets/` and are installed into `user://` on first launch
(never overwriting one you have edited). Each is a deliberate concept, not a recommendation (absolute values, saved before acceptance):

| Preset | Concept |
|---|---|
| `baseline` | compiled defaults; the comparison point |
| `heavy-marble` | weight and momentum: stronger gravity, softer drive, hard slams, camera back |
| `arcade-snap` | tight and forgiving: strong steering everywhere, quick charge, wide burst window |
| `glide-and-soar` | air game: low gravity, big jumps, real air control, generous boost |
| `overdrive-rush` | top-speed fantasy: cap 80, higher bands, wide arcs, hungry boost |
| `technical-lines` | line craft: cap 50, sharper steering, small tank |
| `chunky-lowgrav` | scale probe (V-005): 1.5 m ball, low gravity, slower world |

Accepted baseline (playtest 2026-09-05, final preset `boost-finetune-final` promoted to compiled
defaults verbatim, so the preset shows as "compiled defaults"; D-078): cap 148.5 m/s, steering
151.25 m/s² rising ×1.45 at the cap, gravity 39.38, air control 0.308, ball radius 2.125 m, jump
2.03→58.21 m/s over 0.445 s, slam 42.95 m/s + 141.1 m/s², slam impact ×1.35 on every slam landing,
landing burst ±0.10 s → ×1.15 of the current speed (D-088, was 80% of the cap), boost 88.64 m/s²,
Overdrive at 141.06 m/s, chase camera (follow 4.99/s). Above the base cap sits Flow headroom (below).
The first carve was removed (D-007); the drift carve of D-089 is a new verb on top of the baseline.
`docs/03 §15` and `DECISIONS.md` carry the full register. Starter
presets hold absolute values from before acceptance; they still load but read as variations on
the old, slower baseline.

**Presets** (F1 › PRESETS): type a name and press Enter / **Save As** to stash the current
values as `user://tuning_presets/<name>.json`; pick one from the dropdown and **Load** to apply
it live for comparison; **Delete** removes it. Presets are stashes, not the startup state —
after loading the one you prefer, press **Save Override** to make it load on launch. Typing in
the name field pauses the game and suspends hotkeys.

Starter presets ship in `tuning/presets/` and are installed into `user://` on first launch
(never overwriting one you have edited). Each is a deliberate concept, not a recommendation:

| Preset | Concept |
|---|---|
| `baseline` | compiled defaults; the comparison point |
| `heavy-marble` | weight and momentum: stronger gravity, softer drive, hard slams, camera back |
| `arcade-snap` | tight and forgiving: strong steering everywhere, quick charge, wide burst window |
| `glide-and-soar` | air game: low gravity, big jumps, real air control, generous boost |
| `overdrive-rush` | top-speed fantasy: cap 80, higher bands, wide arcs, hungry boost |
| `technical-lines` | line craft: cap 50, sharper steering, small tank |
| `chunky-lowgrav` | scale probe (V-005): 1.5 m ball, low gravity, slower world |

Deliberate deviations from the `03 §15` placeholders, made at gap closure and to be judged in
playtest:

- **Landing burst** (`Jump › Burst Window (s)` = 0.10, `Burst Multiplier` = 1.15, slider 1.0–1.3;
  D-077/D-088): press Space again at the slam touchdown to multiply your speed along your heading,
  limited by the effective cap. Telemetry's `burst` row shows the window while it is open.
  `VFX › Burst Effect` scales the sparks and rings.
- **Carve** (`Carve` panel category, D-089): hold Alt above 15 m/s and steer; the `carve` row shows
  the facing's angle off travel and the entry speed. Facing swings at 220°/s, the velocity keeps
  25% of its steering authority (`Understeer`), release re-aims at max(entry, current) speed. A
  carve that turned ≥ 30° grants 0.10 Flow. Try it on the turn pad rings of the strip: enter the
  160 m ring at the cap, hold, release when the facing points down the exit.
- **Flow headroom** (`Flow` panel category, D-088): the `flow` telemetry row shows Flow 0..1, the
  Flow cap (base 148.5 × (1 + Flow × `Headroom` 0.33) → 197.5 at full Flow), seconds since the last
  gain and the impact count; the `locomotion` row shows speed over the effective cap. Gains: burst
  0.35, slam landing 0.15, charged jump 0.10. Losses: brake 1.0 per second held, impact 0.5 (a tick
  that sheds > 20 m/s), plain landing 0.25 (no slam, vertical ≥ 30 m/s); recovery → 0. No decay for
  6 s after a gain, then 0.05 per second. Tune it on the runway: boost to the cap, charge-jump, slam,
  burst at touchdown, keep driving: the ball climbs past 148.5. Brake (S) and watch the cap fall.
  `Headroom` 0 is the frozen baseline. Name the preset final and it is promoted verbatim.
- **Landing Cap Bleed = 39.95 m/s²** (new, `Move`). Landing on a slope at the cap makes tangent
  speed exceed the cap by 1/cos(slope) (D-069); the excess now bleeds over ~0.2 s instead of
  clipping in one tick. Set very high to restore the instant clamp.
- **Camera occlusion probe** (`Camera`, on by default): sphere-cast from focus to camera pulls
  the camera in behind berms/mesa faces; disable to compare.
- **Max Visual Roll = 3 rev/s** (`VFX`): visual-only spin cap so the ball does not strobe at speed.
- **Chase camera** (D-072): yaw tracks the flat velocity heading (damping 3/s, ≤140°/s, holds
  below 2 m/s). After a wall bounce or backward slide the view is held only while you push W
  against it and at most `Yaw Reverse Hold Seconds` (1.0 s); release or steer and it swings
  behind you at once. Telemetry's camera row shows `REV-HOLD` while the hold is active. The
  fixed-yaw A/B toggle and the `iso-classic` preset were removed once the baseline locked.
- **Speed bands** (`Move › Rush / Crush Threshold` = 50 / 95 m/s, Overdrive 141.06): readability
  and Flow hooks only, no physics reads them (V-004). Rush ≈ 1/3 of the cap, Crush ≈ 2/3.
- Clipping defence: focus floored above ground; two same-frame sphere casts (focus→camera and
  ball→camera); shake bounded to the probe margin; lens floored above the heightfield
  (`Camera › Ground Clearance`).

## Calibration terrain (seed-invariant)

Terrain spans ±512 m, 4 m facets. Spawn on the flat plain at (470, 380). `World › Terrain Amplitude`
(default 2.125) scales only the scenery hills; the instruments below keep their stated geometry.

| Instrument | Where | Measures |
|---|---|---|
| Calibration lane | z = 380, x 450 → −50, banded every 10 m, posts every 50 m, gantries every 100 m | acceleration, top speed, stopping, thin-post collision |
| Spawn pillars | just north of the lane start, 5 / 10 / 20 / 40 m tall | vertical scale, jump height |
| Grade fan | west (x −450 / −300 / −150), crest z = 150, drops 45 m | 8° / 15° / 25° downhill acceleration; boost rings on the 15° lane |
| Climb + mesa | x 0..225 from z = 130 up 12° to +25.5 m | uphill propulsion |
| Launch ramps | off the mesa lip at x 40 / 112 / 184 (11° / 19° / 27°) | charge jump, slam landings, landing burst |
| Chasms | south rim z = −220 (34 m) and z = −380 (58 m) | gap jumps; 25° exit wall |
| Bowl | centre (340, −320), r 160, floor −48 m | momentum storage |
| Banked hairpin | centre (330, 155), r 145, berm +18 m; boost rings on the line | high-speed banked turns |

Rocks, crystals, pylons, posts and pillars are solid (thin-object / CCD targets).

## Scale strip (Gate M1)

`World › Calibration Strip (M1)` (F1, World tab) swaps the lab for a 6.4 km × 640 m straight
run of stations, each a feature at a stated size, so world scale is measured at the cap instead
of estimated (08 §4, D-079). It rebuilds the world on toggle; F5/T still work. Spawn at
x = +3110 facing −X; distance s is metres from the spawn, on every 100 m post.

| s (m) | Station | Measures |
|---|---|---|
| 0–1000 | Runway: flat, 100 m posts, 500 m gantries, boost rings 550–900 | 0→cap (≈ 580 m / 7.1 s unboosted, measured), braking, burst surge, boosted 0→cap |
| 1000–2000 | Corridor widths 300 / 150 / 75 / 40 m, 250 m each, 30 m walls | minimum corridor width at speed |
| 2000–4200 | Hills: wavelength/height 100/10, 200/20, 400/40, 800/80 m | wavelength, height, crest sightline, "monumental" scale |
| 4200–5080 | Gaps 40 / 80 / 160 m, 150 m runways, 25° exit walls | mandatory/optional gap sizes vs jump range |
| 5080–5700 | Ramps 11 / 19 / 27° with 20 m lips in lanes z −180 / 0 / +180 | launch sizes, landing distance |
| 5700–6300 | Turn pad: flat, painted rings r 80 / 160 / 240 m | turn radius at chosen speeds (≈100 m at the cap) |

Reference envelope at the frozen baseline: turn radius ≈ 100 m at the cap; full-charge jump
43 m up, 2.96 s hang, ≈ 180 m range at 60 m/s and ≈ 440 m at the cap; 0→cap **7.1 s / ≈ 580 m**
unboosted (measured by the harness on the runway; the earlier paper figure of 5.3 s / 394 m
ignored drag); landing burst → ×1.15 of the touchdown speed (D-088; the 118.8 m/s floor is gone).
Flow ceiling 197.5 m/s at full Flow with headroom 0.33: turn radius ≈ 178 m, crest contact radius
≈ 990 m; the ceiling addendum (08 §4) measures these on the strip.

### Route speed model (D-081)

`src/Generation/RouteSpeedModel.cs` is the generator's speed oracle: given a route polyline it
integrates the frozen baseline at a fixed 60 Hz step (gravity × grade, drive held, the 0.02
solver friction as a constant slip loss, drag, the hard cap) and clamps every bend to the
steering envelope's corner speed (v² = r·a_lat(v)). No boost, no burst, no airborne phases;
a vertex it cannot reach reads speed 0 / time ∞. It is pure data, deterministic (profiles
hash), and the harness calibrates it every run:

| Case | Real ball | Model | Error |
|---|---|---|---|
| Runway 0→cap, drive held | 7.1–7.2 s / 575–585 m | 7.06 s / 571 m | ≤ 0.8% at 50–500 m |
| Grade fan 8° / 15° / 25° descents, drive held, at 25 / 50 / 75 m | 39–66 / 43–71 / 47–78 m/s | same to 0.3 m/s | ≤ 0.8% |

Without the slip term the model ran ≈ 2% fast on the runway and 4.4% long to the cap: the
controller re-slips the ball every tick, so the "negligible" friction is a steady 0.79 m/s²
loss. The corner clamp is deliberately conservative (instant loss at the vertex, no brake
distance modelled), so profiles under-predict speed after bends and never over-predict it.

## Generation (Phase 2)

`src/Generation/` is pure data with no SceneTree dependency: `WorldScale` (the accepted M1 family,
D-082), `SeedChain` / `SeededRandom` (explicit seed chain, SplitMix64), `StageGenerationRequest` →
`StageGenerator.Generate` → `StageDefinition` with a `ValidationReport` (named checks, attempts, phase
timings). The route skeleton is a chain of straights (250–600 m) and circular bends from the ladder
(160 / 100 / 50 m at 50 / 35 / 15%), headings within ±45° of the stage axis, wandering inside the
±600 m route band, sampled every 4 m; every bend carries its corner speed limit. Validation fails →
the route seed is bumped (≤ 4 attempts) → the straight axis route is the known-safe fallback, flagged
in the report. The harness generates 100 requests every run and asserts: all valid, no fallback,
same request → same hash, distinct seeds → distinct stages, lengths and base-kit times in range,
≈ 0.5 ms per stage skeleton. Measured over 1000 seeds (2026-09-05): 1000 valid, 0 fallbacks, 6.3–6.9 km,
base-kit 46–53 s on the flat skeleton.

**Regression seeds (08 §11).** `tests/RegressionSeeds.cs` is the list the harness generates on every run
and asserts valid without fallback: the toy default seed (20260905/0, the first playtested stage) plus
seeds that exercise the second route attempt (8/0, 15/5, 57/5), no ridge line (4/8), three lines or no
crest (34/6, 13/5) and the slowest/longest and fastest stages of the 1000-seed scan (90/5, 7/4). To add
one: read the seed from the telemetry `seed` row (or the log line `Stage generated seed=N/0`), append
`new(N, 0, "why")`, and keep it there once the bug is fixed; `-- --seed N` launches the toy on it. The log prints each entry's attempts, length,
base-kit time, crests, lines, anchors and hash, and for a second-attempt seed the reason attempt 1 failed.

### Generated stage in the toy (Phase 2, PR 2)

`World › Generated Stage (Phase 2)` swaps the lab for a Rolling Highlands stage generated from the
current seed (F5 = new seed, T = back to the start pad; the strip toggle is ignored while it is on).
What you see: a 6.0 × 2.0 km footprint; a green **START** pad and blue **EXIT** pad 5.8 km apart along
+X; the stamped 150 m corridor (wider and banked on bends) as a lighter track; **CREST ▲** signs on
launch-crest straights; boost rings on the line every 1.2 km; scatter kept out of the corridor.
`World › Route Debug Lines` (04 §16) draws the primary route 3 m up: cyan straights, orange bends,
magenta crest zones. The telemetry `seed` row shows the generation summary (valid / fallback,
length, base-kit time, bends, crests, generation ms) and the log prints every validation check.
Build: ≈ 3.6 s headless for 752 k samples / 1.5 M triangles / 48 tiles (world sampling dominates;
the definition itself is ≈ 25 ms). The harness builds one stage every run and drives its whole primary
route with a route follower (a smoke test of one seed, not an agent): the ball must reach the exit pad,
stay inside the corridor (off-line ≤ 15 m measured), keep contact ≥ 60% of the way, and arrive within 10%
of the route speed model's base-kit time (Gate G0, D-087: ball 49.9 s vs model 47.8 s, 4.3%; the log prints
ball vs model time and speed every kilometre with the contact fraction over that kilometre: 90 / 94 / 58 /
87 / 76 / 22% on the default seed, the low values on the two launch-crest kilometres; after the second
crest at the cap the ball skips for most of the last kilometre and reaches the exit at 119 m/s). The same case
asserts no solid prop inside a corridor, that the SceneTree node count is flat across three regenerations
and that neither generation nor the build writes to tuning or the rigid body (04 §7).

Relief rules (D-085): three long swells with summed crest curvature ≤ 0.7 / 560 m and summed slope
≤ 0.18, micro relief inside the remaining curvature budget (≈ 0.9 m at λ 400), corridor profile =
relief smoothed ± 150 m along the route, level across, 120 m falloff, banks 18/145 · r on the outer
half of bends, crests λ 300–500 with height trimmed so crest + local slope ≤ 0.36, flat 60 m pads.

**Optional lines and checkpoints (PR 3, D-086).** Green route lines are ridge lines: they leave the
primary on the outside of its bends, shift 200 m sideways over a 360 m S-transition, climb 25–40 m
onto a plateau, and drop back to rejoin; corridor 75 m, no bank. They are distinct by elevation and
exposure, not by distance (a 45°-bounded primary is already nearly straight, so chords save little;
distance shortcuts arrive with the Phase 3 gap modules). Posts with a glowing cap are the recovery
anchors, every 400 m of primary route on straights only, pushed past any crest and its landing run.
On a generated stage the toy's rolling auto-checkpoint is off: R or a fall restores to the last anchor
you legitimately passed (progress only advances while you are inside the corridor reach), the telemetry
`stage` row shows clock, progress, armed anchor and the exit time once you cross the blue pad, and the
log prints the exit time against the route speed model's base-kit prediction. Measured 2026-09-05 over
1000 seeds: all valid, no fallback, 95% of seeds carry 1–3 ridge lines (1.5 avg), ≥ 15 anchors each,
≈ 34 ms per definition; the harness drives the first ridge line of the built stage (≈ 70% grounded).

**Deferred M1 feel verdicts to take here** (D-082): fog onset at the cap (`World › Fog End` /
`Camera › Far Plane` are live); corridor width at the cap (150 m typical here; the 75 m minimum is
still a strip question); whether the swells read as a landscape and the crests as intended launches;
frame time at the cap with F2 (and again at Fog End 4000 / Far Plane 5000). Ramp and gap verdicts
wait for the Phase 3 modules or the strip.

### Terrain budget (the other half of M1, D-080)

Scale must be buildable and drawable as well as fun. Every build logs its budget
(`World built ... k samples, k tris in N tiles, MB`), and three live knobs exist for it:
`World › Cell Size (m)` (rebuilds when the slider settles), `World › Fog End (m)` and
`Camera › Far Plane (m)` for the draw-distance-versus-sightline read. Render terrain is tiled
(128 cells square, one ArrayMesh each, frustum-culled); collision stays one heightfield.

Measured 2026-09-05 (Apple Silicon, headless build times, warm JIT):

| World | Cells | Samples | Triangles | Tiles | Build |
|---|---|---|---|---|---|
| Lab 1.0 × 1.0 km | 4 m | 66 k | 131 k | 4 | ~130 ms |
| Strip 6.4 × 0.64 km | 4 m | 258 k | 512 k | 26 | ~500 ms (950 cold) |
| Strip 6.4 × 0.64 km | 8 m | 65 k | 128 k | 7 | ~280 ms |

Model: samples ≈ L·W/c², triangles = 2·samples, build ≈ 2 µs per sample warm, heights 4 B per
sample, each tile ≤ ~4 MB of vertex arrays. A 6 × 2 km stage at 4 m is ≈ 750 k samples,
1.5 M triangles, ≈ 1.5 s; at 8 m a quarter of that.

**Facet size is a feel cost, not only a draw cost.** The harness drives the 800 m / 80 m hill
station with W held from 60 m/s: at 4 m cells the ball is grounded 99% of the way with 2 short
hops; at 8 m it is grounded 86% with 8 hops. Judge cell size on contact first, then triangles.

**Crest contact.** A ball leaves the ground at any crest whose radius is below v²/g:

| Speed | 60 m/s | 100 m/s | 148.5 (base cap) | 197.5 (Flow ceiling) |
|---|---|---|---|---|
| Minimum crest radius to stay grounded | 91 m | 254 m | 560 m | 990 m |

A cosine hill of wavelength λ and height H has crest radius λ²/(2π²H): the strip's 100/10,
200/20, 400/40 and 800/80 stations keep contact only up to ≈ 45, 63, 89 and 126 m/s. Above
that every crest is a launch, which is a generation input, not a bug (Rolling Highlands lists
crest launches as a feature). Measure frame time at the cap on the strip with F2 open.
