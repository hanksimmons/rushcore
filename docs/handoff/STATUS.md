# Handoff status ledger

One row per packet. The implementing agent updates its row before every push. The main track fills the last column on
reconciliation (`docs/handoff/README.md` §8). States: `not started` · `in progress` · `pushed` · `merged` · `reconciled`.

| Task | Packet | Branch | State | Last commit | Harness (full run) | P-entries | Reconciled |
|---|---|---|---|---|---|---|---|
| T1 | `T1_STAGE_LIFECYCLE.md` | `opus/t1-stage-lifecycle` | not started | — | — | — | — |
| T2 | `T2_HUD.md` | `opus/t2-hud` | not started | — | — | — | — |
| T3 | `T3_ENEMY_PICKUP_VISUALS.md` | `opus/t3-visuals` | not started | — | — | — | — |
| T4 | `T4_PROP_SCATTER.md` | `opus/t4-prop-scatter` | not started | — | — | — | — |
| T5 | `T5_MEASUREMENTS.md` | `opus/t5-measurements` | not started | — | — | — | — |
| T6 | `T6_SAMPLE_FIXES.md` | `opus/t6-sample-fixes` | blocked on the user's notes | — | — | — | — |
| T7 | `T7_TUBE_JUDDER.md` | `opus/t7-tube-judder` | in progress (main track: reproduced, fix applied, after-run pending; Opus continues via the T7 handover prompt) | see branch | before 311/313 (2 expected) | P-009 | — |

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

## Needs main track (collected)

Items the parallel track could not close inside its boundaries. Copied here from the packets' Delivery records so the
reconciliation sees them in one place.

- (none yet)
