# Handoff — parallel work packets (Opus 5 track)

**Status:** Active from 2026-09-07 for a few days  
**Authority:** Operating contract for the parallel track only. It never overrides `README.md`'s hierarchy, `CLAUDE.md`, or any numbered spec.  
**Owner of reconciliation:** the main track (Fable 5.1 sessions) with the user.

## 1. Why this directory exists

The main track is between Phase 3 (delivered, D-097..D-105; D-105 branching exits is on `feature/branching-exits`) and
the impact-combat core of Phase 4, which the user has reserved for it. For a few days a second
agent (Opus 5) works ahead on slices that are well specified, self-contained, harness-checkable, and never touch route
topology, generator rules or the frozen movement baseline.

Everything the parallel track does must be reconcilable with the pre-existing plan (`docs/09`) without archaeology. This
directory is the ledger: one packet per task, one status table, one file of provisional decisions. If it is not written
here, it did not happen.

## 2. Start-of-session prompt (paste verbatim)

```text
Resume RUSHCORE (Godot .NET 4.7.2, C#, macOS arm64, repo ~/rushcore). You are the parallel track described in
docs/handoff/README.md. Read, in order: CLAUDE.md, README.md, docs/00, docs/01, docs/handoff/README.md,
docs/handoff/STATUS.md, then the packet you are assigned (docs/handoff/T<n>_*.md) and every spec section it names.

Rules: feature branch off develop-secondary named opus/t<n>-<slug> -> full harness green -> push -> give the compare URL
(no gh CLI) -> the user merges or instructs. No attribution lines in commits or PR text. Docs with code, same commit.
The movement baseline (D-095) is frozen: never change a compiled default in Movement, Boost, Camera, Flow, Carve or VFX.
Never change a generator rule, validator tolerance, WorldScale number or route topology: if a task seems to need one,
stop, record it in the packet's Delivery record under "Needs main track", and continue with the rest.
KISS, YAGNI, programmatic content, pure-data generation, one height source. No AI agent plays stages.
Write only P-numbers in docs/handoff/PROVISIONAL_DECISIONS.md; never write D-numbers, never edit docs/09,
docs/DECISIONS.md or the CLAUDE.md phase paragraph. Fill the packet's Delivery record and the STATUS.md row before
every push. Tell me in one line what you are about to do, then go.
```

## 3. Read order for the parallel track

1. `CLAUDE.md` (operating contract), `README.md` (authority hierarchy)
2. `docs/00_GAME_VISION.md`, `docs/01_MVP_SCOPE.md`
3. this file, then `STATUS.md`
4. the assigned packet, then every spec section it lists under "Owning specs"
5. `docs/05_TECHNICAL_ARCHITECTURE.md`, `docs/08_TEST_ACCEPTANCE.md` §1, §2, §12
6. `docs/10_MOVEMENT_TOY_RUNBOOK.md` (build, run, harness commands, controls)
7. `docs/DECISIONS.md` D-088..D-104 (read only: the accepted state the packets build on)

## 4. Standing rules (the user's, verbatim in intent)

- Workflow: feature branch off `develop-secondary` → push → compare URL → the user merges or instructs. No `gh` CLI.
- **No attribution lines** in commit messages or PR descriptions.
- Commit docs with code. Update an owning spec only when accepted behaviour changed, and only the sections the packet names.
- The D-095 baseline is frozen input. Only a preset the user names final changes it, promoted verbatim by the main track.
- World toggles are never promoted to compiled defaults.
- KISS / YAGNI. No scaffolding for later phases. No DI, service locators, event buses, generic factories.
- Pure-data generation; explicit seeds; one height source (structures are lids and tubes only; never a second height layer).
- No AI agent plays stages. The harness follower on one seed is a smoke test, not a judgement of feel.
- Difficulty is parked until after Phase 4. Do not harden terrain, enemies or timings.
- Walls on the outside of bends.
- Before every push: `dotnet build`, then the full harness (§6), green. Report the count as N/N in the Delivery record.
- Report faithfully: a failing check is reported with its output; a skipped step is reported as skipped.

## 5. Boundaries

**Allowed to change:** `src/Core/GameBootstrap.cs` (wiring), `src/World/MovementToyWorld.cs` (lifecycle and dressing
hooks only), `src/World/WorldDressing.cs`, `src/DebugUi/*`, `src/Tuning/GameplayTuning.cs` (new categories and new
fields only), `tests/*`, new files under `src/Run`, `src/UI`, `src/Enemies`, `src/Pickups`, `src/Vfx`, the docs a packet
names, and this directory.

**Off limits without a "Needs main track" note:** `src/Generation/RouteSkeletonBuilder.cs`, `OptionalLineBuilder.cs`,
`TubeBuilder.cs`, `LidBuilder.cs`, `StageHeightField.cs`, `RouteSpeedModel.cs`, `WorldScale.cs` values, every
validator in `StageGenerator.cs` (T5 may add *reporting* there, never a verdict change), movement rules in
`src/Player/PlayerPhysics.cs` (T2 may add a health value holder; nothing that reads or writes velocity), any compiled
default in Movement / Boost / Camera / Flow / Carve / VFX tuning, `docs/02`, `docs/03`, `docs/04` behaviour text.

**Reserved numbers:** D-numbers belong to the main track (D-105 is taken by branching exits). The parallel track writes P-numbers only.

## 6. Harness

```sh
dotnet build
/Applications/Godot_mono.app/Contents/MacOS/godot --headless --fixed-fps 60 --path . -- --rushcore-selftest
```

About 65 s. `RUSHCORE_SELFTEST_DATA_ONLY=1` runs the pure-data cases in about 35 s (use while iterating, never as the
pre-push run). Packets that touch stage dressing (T4, T6) also run `RUSHCORE_ARCHETYPE=canyon|dunes|sky` once each.
macOS has no `timeout`; foreground `sleep` is blocked by the tooling; run long chains in the background and wait for the
notification.

Every new behaviour gets a harness check in `tests/MovementToySelfTest.cs` using `Check(name, ok, detail)`. Never loosen
an existing tolerance; if one fails for a reason outside the packet, record it under "Needs main track" and stop the push.

**Golden hashes.** `tests/GoldenHashes.cs` pins the hash of every sample stage. If the check "every sample stage generates
its golden hash" fails on your branch, generation moved: never update the table, find the change you made to generation
data, undo it, and record what happened under "Needs main track". Dressing, HUD, lifecycle and instruments never move a
hash; the check runs in the 35 s data-only mode, so run it after every edit near `src/Generation` or `src/World`.

## 7. What each task must leave behind

1. **The packet's Delivery record** filled in (branch, commits, harness count, files, what was built, what deviated from
   the packet and why, measurements, open items, "Needs main track").
2. **A row in `STATUS.md`** kept current at every push.
3. **P-entries** in `PROVISIONAL_DECISIONS.md` for every choice a packet marks "P-entry", plus any choice the packet did
   not anticipate that changes accepted behaviour, a spec sentence, a default, or a data contract.
4. **Owning-spec edits in place**, limited to the sections the packet names, each edit tagged `(P-nnn)` so the
   reconciliation can find it.
5. **Runbook lines** in `docs/10` for every new flag, hotkey, panel entry or env knob.

## 8. Reconciliation protocol (main track, on return)

1. `git log --first-parent develop-secondary` for `opus/` merges; read `STATUS.md`, every Delivery record, `PROVISIONAL_DECISIONS.md`.
2. Run the full harness on `develop-secondary`; record the count in `STATUS.md`.
3. For each P-entry: the user's verdict. Accepted → promote verbatim to `docs/DECISIONS.md` as the next free D-number
   (D-106 onward; D-105 is branching exits), mark the P-entry `PROMOTED D-nnn`. Rejected → mark `REJECTED`, open a fix
   task. Search the docs for the `(P-nnn)` tags and replace them with the D-number.
4. Update `docs/09` bullets (Phase 4 and 5 partial deliveries carry the D-number and date), the `CLAUDE.md` phase
   paragraph, and the memory file `rushcore-phase-state.md`.
5. Cross-check any spec edit against `README.md`'s hierarchy: a sentence changed in 02, 03 or 04 needs a D-entry or a
   revert.
6. Mark each `STATUS.md` row `RECONCILED (D-nnn)`. The directory stays as the record; nothing is deleted.

## 9. Packets

| Packet | Task | Branch | Depends on |
|---|---|---|---|
| `T1_STAGE_LIFECYCLE.md` | Stage completion, transition, run seed and stage index | `opus/t1-stage-lifecycle` | none |
| `T2_HUD.md` | Player HUD and the health value | `opus/t2-hud` | T1 merged (stage index, currency) |
| `T3_ENEMY_PICKUP_VISUALS.md` | Enemy silhouettes, pickup shapes, reward burst, combat VFX one-shots | `opus/t3-visuals` | none |
| `T4_PROP_SCATTER.md` | Archetype-aware prop scatter on generated stages | `opus/t4-prop-scatter` | none |
| `T5_MEASUREMENTS.md` | Generation instruments: wall probe, tube ride, floor-3 room, landing run | `opus/t5-measurements` | none |
| `T6_SAMPLE_FIXES.md` | Presentation fixes from the user's sample-stage playtest | `opus/t6-sample-fixes` | the user's notes |

Suggested order: T1, T4, T5 (independent, quick to merge), then T2, T3, then T6 when the notes exist.
