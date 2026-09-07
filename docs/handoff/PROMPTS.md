# Session prompts (paste verbatim)

Three kinds: **kickoff** (a packet's first session), **resume** (a later session on the same packet), **reconcile**
(the main track on return). Every prompt names the branch to work from, the packet, and what "done" means, so a cold
session needs nothing from a previous one but what is in the repo.

The integration branch for this track is `develop-secondary`. Never fork from or merge into `develop`.

---

## Common preamble (goes at the top of every kickoff and resume prompt)

```text
Resume RUSHCORE (Godot .NET 4.7.2, C#, macOS arm64, repo ~/rushcore). You are the parallel track described in
docs/handoff/README.md. Start with: git fetch && git checkout develop-secondary && git pull. Read, in order: CLAUDE.md,
README.md, docs/00, docs/01, docs/handoff/README.md, docs/handoff/STATUS.md, then your packet and every spec section it
names under "Owning specs". Before writing code: dotnet build, then the data-only harness
(RUSHCORE_SELFTEST_DATA_ONLY=1 /Applications/Godot_mono.app/Contents/MacOS/godot --headless --fixed-fps 60 --path . --
--rushcore-selftest) and confirm it is green, so you know the baseline and the run time.

Rules: one packet per branch named opus/t<n>-<slug>, forked from develop-secondary; full harness green before every push;
push and give the compare URL against develop-secondary (no gh CLI); the user merges or instructs. No attribution lines
in commits or PR text. Docs with code, same commit. The movement baseline (D-095) is frozen: never change a compiled
default in Movement, Boost, Camera, Flow, Carve or VFX. Never change a generator rule, validator tolerance, WorldScale
number, route topology or tests/GoldenHashes.cs; if the golden-hash check fails on your branch, generation moved: undo
your change and record it under "Needs main track". KISS, YAGNI, programmatic content, pure-data generation, one height
source. No AI agent plays stages. Write only P-numbers in docs/handoff/PROVISIONAL_DECISIONS.md; never write D-numbers,
never edit docs/09, docs/DECISIONS.md or the CLAUDE.md phase paragraph. Fill the packet's Delivery record and your
STATUS.md row before every push. Report faithfully: a failing check is reported with its output. Tell me in one line
what you are about to do, then go.
```

---

## Kickoff prompts (preamble + one of these)

**T1 — stage lifecycle**
```text
Your packet is docs/handoff/T1_STAGE_LIFECYCLE.md. Branch opus/t1-stage-lifecycle. Build RunDirector (run seed, stage
index, Advance), make the world take the request, add the completion outro on any exit pad (StageDefinition.Exits,
StageExitIndex; carry the exit index on the completion event), the TeleportNearExit debug action and the --stage N flag.
Use the packet's pre-answered choices (P-001..P-003) unless you have a reason, and write the P-entry if you deviate.
Done means: every acceptance line in the packet is a harness check that passes, the Delivery record and STATUS row are
filled, docs 05 §7, 07 §12, 08 §7 and 10 are updated, and the branch is pushed with its compare URL.
```

**T2 — HUD and health value** (after T1 is merged, or say you are stacking on opus/t1-stage-lifecycle)
```text
Your packet is docs/handoff/T2_HUD.md. Branch opus/t2-hud. If T1 is not yet merged into develop-secondary, fork from
opus/t1-stage-lifecycle and say so in STATUS. Build PlayerHealth (value, debug kill/heal, P-004) and PlayerHud (health,
boost, Flow with a gain pulse, stage/run progress, currency; band word off by default, P-005) with a Hud tuning category.
No m/s on the HUD. Done means the packet's acceptance lines are harness checks that pass (including the per-frame
allocation measurement), docs 06 §12, 07 §7, 07 §12 and 10 are updated, the Delivery record and STATUS row are filled, and
the branch is pushed with its compare URL.
```

**T3 — enemy and pickup visuals**
```text
Your packet is docs/handoff/T3_ENEMY_PICKUP_VISUALS.md. Branch opus/t3-visuals. Build EnemyVisuals (Pylon, Bulwark,
Strider, Shooter, one elite treatment; no colliders, no behaviour, P-006), PickupVisuals, RewardBurst (kinematic coins
that magnetise, P-007), WorldVfx one-shots (crush, fail, damage, pickup, exit) and the World › Enemy Showcase lab row with
its debug actions and one screenshot frame. Tune the sizes on the runway at the cap and record what you settled on.
Done means the packet's acceptance lines are harness checks that pass, docs 06 §8-§10, 07 §11-§12 and 10 are updated,
the Delivery record and STATUS row are filled, and the branch is pushed with its compare URL.
```

**T4 — prop scatter**
```text
Your packet is docs/handoff/T4_PROP_SCATTER.md. Branch opus/t4-prop-scatter. Build the stage clear function from
StageDefinition data (every line, module, tube mouth, lid, pit disc, checkpoint, exit pad), per-archetype prop sets, the
corridor-edge markers, cosmetic-seed determinism (P-008) and the instance/time budget print. Run the default harness plus
RUSHCORE_ARCHETYPE=canyon, dunes and sky. Done means the packet's acceptance lines are harness checks that pass on all
four archetypes, the golden hashes are unchanged, docs 04 §5G, 04 §16, 06 §16, 07 §5 and 10 are updated, the Delivery
record and STATUS row are filled, and the branch is pushed with its compare URL.
```

**T5 — generation measurements**
```text
Your packet is docs/handoff/T5_MEASUREMENTS.md. Branch opus/t5-measurements. Build the four instruments behind
RUSHCORE_MEASURE=1 (wall-probe classification, tube ride source, floor-3 room, landing run vs braking) as prints only:
no validator verdict, builder rule, tolerance or WorldScale number changes, and the harness count must not change. Fill
the four tables and one recommendation per instrument in the Delivery record. Done means the default harness count is
unchanged and green, the golden hashes are unchanged, docs 04 (Empirical validation items) and 10 are updated, the
Delivery record and STATUS row are filled, and the branch is pushed with its compare URL.
```

**T6 — sample-stage presentation fixes** (only once the user's notes are in the packet)
```text
Your packet is docs/handoff/T6_SAMPLE_FIXES.md. Branch opus/t6-sample-fixes. Work the numbered playtest notes in the
packet, one commit per note, presentation only; reproduce each on its sample stage before and after with the screenshot
flag and confirm the sample's golden hash is unchanged. Anything that needs a generator rule, a validator, a WorldScale
number or a camera baseline value goes under "Needs main track", not into code. Done means every note is either fixed
with before/after screenshots named in the Delivery record or logged as needing the main track, the full harness and
the three archetype runs are green, and the branch is pushed with its compare URL.
```

**T7 — tube judder while boosting**
```text
Your packet is docs/handoff/T7_TUBE_JUDDER.md. Branch opus/t7-tube-judder. Reproduce first: seed 9 with boost held
through the tube, then the harness trace the packet describes (radial oscillation, follow flips), and write the numbers
into the Delivery record before touching a fix. If the facet hypothesis holds, apply the two-line fix (TubeMesh.Sides 24,
the tube follow holding the inscribed radius) and nothing else; if not, follow the packet's alternatives. You may edit
TryTubeFollow and TubeMesh only. Done means the boosted-ride acceptance lines are harness checks that pass, the cruise
ride and the golden hashes are unchanged, docs 04 §5I and 11 §7d are updated, the P-entry, Delivery record and STATUS
row are filled, and the branch is pushed with its compare URL.
```

**T7 — handover: the main track started the fix and was interrupted** (use this instead of the T7 kickoff)
```text
Your packet is docs/handoff/T7_TUBE_JUDDER.md. The main track (Fable) began this fix on branch opus/t7-tube-judder and
was interrupted mid-work; you continue it. Do: git fetch && git checkout opus/t7-tube-judder && git pull, then git status.
If the tree is dirty, read git diff first, then commit it as "WIP: main track interrupted (T7)" so nothing is lost. Read
the packet top to bottom: its Analysis section is the reasoning you build on, and its Delivery record is a running log
with "Done" and "Next" lists that the main track kept current at every commit; git log --oneline develop-secondary..HEAD
and git diff develop-secondary --stat show the code. Rebuild and run the data-only harness before changing anything.
Continue from the first item under "Next"; do not redo anything under "Done"; if a "Done" item has no harness check
yet, add the check before moving on. Keep the packet's Delivery record and STATUS row current at every commit. Boundaries
hold as the packet states: TryTubeFollow and TubeMesh only; no ground-follow, steering, boost, cap or compiled-default
changes; the golden hashes must not change. Done means the boosted-ride acceptance lines are harness checks that pass,
the cruise ride and golden hashes are unchanged, docs 04 §5I and 11 §7d are updated, the P-entry is written, the full
harness and the three archetype runs are green, and the branch is pushed with its compare URL against develop-secondary.
```

---

## Resume prompt (a later session on a packet already started)

```text
<preamble>

You are continuing packet T<n> (docs/handoff/T<n>_*.md) on branch opus/t<n>-<slug>. Do: git checkout opus/t<n>-<slug>
&& git pull. Read the packet's Delivery record and STATUS.md row as the record of what is already done, then git log
--oneline develop-secondary..HEAD and git diff develop-secondary --stat to see the code. Rebuild and run the data-only
harness before changing anything. Continue from the first unfinished acceptance line; do not redo finished work. Keep the
Delivery record and STATUS row current at every push. Same rules and definition of done as the packet.
```

---

## Reconcile prompt (the main track, on return)

```text
Resume RUSHCORE. Reconcile the parallel track per docs/handoff/README.md §8: read docs/handoff/STATUS.md, every packet's
Delivery record, and docs/handoff/PROVISIONAL_DECISIONS.md; git log --first-parent develop-secondary for the opus/
merges; run the full harness on develop-secondary and record the count in STATUS. Put every PROPOSED P-entry to me with
a one-line recommendation each; promote the accepted ones verbatim to docs/DECISIONS.md from D-106, tag-replace (P-nnn)
in the docs, update docs/09, the CLAUDE.md phase paragraph and your memory, mark the STATUS rows reconciled. Then list
what develop-secondary still needs before it merges to develop, starting with the branching-exits refinement.
```
