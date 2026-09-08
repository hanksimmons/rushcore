# Handoff status ledger

One row per packet. The implementing agent updates its row before every push. The main track fills the last column on
reconciliation (`docs/handoff/README.md` §8). States: `not started` · `in progress` · `pushed` · `merged` · `reconciled`.

| Task | Packet | Branch | State | Last commit | Harness (full run) | P-entries | Reconciled |
|---|---|---|---|---|---|---|---|
| T1 | `T1_STAGE_LIFECYCLE.md` | `opus/t1-stage-lifecycle` | **merged** into `develop-secondary` | 4db5348 | 337/337; canyon 334/334, dunes 337/337, sky 338/338 | P-012 (P-003, P-010, P-011 implemented) | — |
| T2 | `T2_HUD.md` | `opus/t2-hud` | pushed | (first commit) | 365/365; canyon 362/362, dunes 365/365; **sky red on one unrelated flaky guard** (see Needs main track) | none new (P-004, P-005 implemented) | — |
| T3 | `T3_ENEMY_PICKUP_VISUALS.md` | `opus/t3-visuals` | not started | — | — | — | — |
| T4 | `T4_PROP_SCATTER.md` | `opus/t4-prop-scatter` | **merged** into `develop-secondary` | 524dc21 | 323/323; canyon 320/320, dunes 323/323, sky 324/324 | P-013 (P-008 implemented) | — |
| T5 | `T5_MEASUREMENTS.md` | `opus/t5-measurements` | not started | — | — | — | — |
| T6 | `T6_SAMPLE_FIXES.md` | `opus/t6-sample-fixes` | blocked on the user's notes | — | — | — | — |
| T8 | `T8_TUBE_RUBBER_BAND.md` | `opus/t8-tube-arrest` | camera half **merged**; the arrest is open but rare (1 of 915 ticks, none near the bottom) and no longer felt in play | — | — | — | — |
| T7 | `T7_TUBE_JUDDER.md` | `opus/t7-tube-judder` | **merged** into `develop-secondary` (play verdict given: the rubber-banding is gone) | (final commit) | 313/313; canyon 311/311, dunes 313/313, sky 314/314 | P-009 | — |

## Merged into `develop-secondary`

- T1 (4db5348) and T4 (524dc21) merged 2026-09-07. Full harness on the merged branch: 346/346 default, canyon
  343/343, dunes 346/346, sky 347/347; golden hashes unchanged. Three text conflicts (the runbook, this file's
  decisions table, the harness's tail) were both-sides additions and both sides were kept.

## Baseline at handoff

- **Integration branch for the parallel track is `develop-secondary`** (9419b18, 2026-09-07): `develop` a793dbe plus
  D-105 branching exits, the golden hashes and these packets. The user merges it to `develop` once the exits are refined;
  every `opus/` branch forks from and merges into `develop-secondary`, never `develop`.

- `develop` a793dbe (2026-09-07): Phase 3 complete (D-097..D-104), sample-stage selector merged. This branch is stacked on
  `feature/branching-exits` (34db4be: D-105 plus the golden hashes); merge that first. Full harness there 309/309 plus the golden-hash check.
- D-105 branching exits delivered on `feature/branching-exits` (e5ab011, 2026-09-07): `StageDefinition.Exits`, any pad ends
  the stage (`MovementToyWorld.StageExitIndex`). T1 reads the list, not a single exit.
- Main-track reservations: Phase 4 impact model and Flow integration; Sky Terraces second cut; Dune Sea optional line;
  spiral ramp; Phase 6 run and route generation (the route cards on the fork signs, the next-stage seed from the exit taken).

## Open for the user's read (collected)

Questions this track cannot answer, because they are judgements about how something looks or feels in play and no AI
agent plays stages. Copied from the packets' Delivery records so a playtest session can pick them all up at once.

- **T4, Dune Sea scatter density:** about 190 instances a stage against Highlands' 1264, because the rule puts things
  on wave crests only. Accents on the crest lines, or a bare stage? Lever: the `DuneSea` branch of `WorldDressing.Suits`.
- **T4, Canyon Run scatter shape:** no crystals at all, 1458 rocks, every one on a wall top, none on the slot floors.
  Does the bare floor beside the corridor read as a canyon or as a fence? Lever: the `CanyonRun` branch of `Suits`.
- **T4, frame time at density 0.19 and 1.0:** cannot be measured headless. `godot --path . -- --seed 8`, F2, read the
  `frame` row at both densities; at 1.0 the stage carries 2700 instances (the caps).
- **T1, the completion outro:** whether it reads as "done" rather than a freeze, and whether anything of the finished
  stage shows through the fade.

Each is also written up where it happened, with the numbers: `T4_PROP_SCATTER.md` and `T1_STAGE_LIFECYCLE.md`, under
Open items. `T6_SAMPLE_FIXES.md` carries them into the sample playtest so they are asked while the samples are open.

## Needs main track (collected)

- **T2: the T8 tube-camera guard flakes across its threshold on the base branch.** "the tube camera does not snap
  along the axis sample grid" bounds the worst change in the lens's step between ticks at 3.5 m on the sky tube
  ride. Two runs of `develop-secondary` (004894b) with no changes at all measured 3.37 m and 3.65 m: one passes,
  one fails. On `opus/t2-hud`, 3.63 m and 2.63 m; with the HUD node simply not added to the tree, 2.94 m. The
  metric is sampled per rendered frame against an interpolated player transform, so it varies with how much work a
  frame did, and no UI change can reach physics. It needs re-expressing per physics tick, or against the lens's
  station along the tube — the quantity the snap actually moved. The tolerance has not been touched.

Items the parallel track could not close inside its boundaries. Copied here from the packets' Delivery records so the
reconciliation sees them in one place.

- **T7:** `TubeDefinition.Nearest` / `MovementToyWorld.Nearest` answer with the nearest axis *sample* and its tangent
  rather than the nearest point on the axis. Measured error against the axis polyline: 1.4 cm (Dune Sea tube), 3.5 cm
  (Highlands), **21 cm** (Sky, which climbs steeply). The tube follow has to hold the ball clear of that error, so the
  ball floats ≈ 12 cm off the glass instead of ≈ 4 cm, and Sky's ride is the least precise of the four. Interpolating
  the nearest point along the two adjoining segments would remove it, let `PlayerPhysics.TubeAxisUncertainty` go to
  zero and tighten every tube ride; the camera's `PushOutOfTubes` reads the same query and would gain the same accuracy.
  Outside T7's boundary (the packet allows `TryTubeFollow` and `TubeMesh` only).
