# T6 — Presentation fixes from the user's sample-stage playtest

**Branch:** `opus/t6-sample-fixes` off `develop`  
**Phase:** 3 close-out (visuals of the vertical grammar were unverified in the toy until the user's sample)  
**Gate lines served:** 08 §5 G0 manual sample per archetype; 08 §9 V0 lines on readability  
**Owning specs:** 06 §2, §3, §4, §11 (Structures, D-096), §16, §17; 04 §16; 07 §11; 10 (sample table)

## Goal

Turn the user's notes from playing the five sample stages (`World › Sample Stage` 1–5) into presentation-only fixes,
one commit per note, each reproduced on the named sample before and after. Anything that turns out to need a
generator rule, a validator, a `WorldScale` number or a camera baseline value is logged, not fixed.

## Playtest notes (the user fills this in; the agent does not invent entries)

Format: `sample # · what was seen · where (route metre or landmark) · expected`.

1.
2.
3.

Already known and **out of scope** here: "maps do not truly branch, every stage has one exit" is D-105, main track.

## Candidate issues from the code audit (unverified; confirm on the sample before touching)

- Tube shell (`src/World/TubeMesh.cs`, `WorldDressing.TubeShellMaterial`): translucency against fog at distance; rib
  band contrast; whether the ×2 mouth flare reads as an entrance from 300 m out.
- Lid underside (`WorldDressing.LidMaterial`, `MovementToyWorld.BuildStructures`): dark under the roof; an unshaded
  edge band or a faint emissive underside may be needed so the tunnel reads as a tunnel, not a hole.
- Pit rim (`SpiralPit`, the `PIT ↓` sign): readability from the approach at the cap; a ring of markers at the rim.
- `FLOOR n ↑` signs on terraces: placement at the ramp foot vs the plateau; whether the cliff edge reads from above.
- Cloud sheets (`BuildStageDressing`, `CloudBand`): ordering and flicker between the two planes; visibility of the
  floor-3 plateau inside the band.
- Dune palette: crest contrast in the sand family; whether a train's launch crests read as launches.
- `TUBE ✗` signs: a failed tube should not be built at all; confirm none is on the samples.
- Sign glyph sizes at the D-091 scale (the ball is 0.66 m; signs were sized before the rescale).

## Rules for a fix

- Presentation only: materials, meshes, signs, marker placement, cloud sheets, sizes of dressing. Camera push-out
  margins (`WorldScale.TubeCameraMargin`, `LidCameraMargin`) are dressing numbers, not baseline, and may change with a
  P-entry; every other camera value is D-095 and frozen.
- Reproduce on the sample (command line in docs/10 or the selector), capture a `--rushcore-screenshot` frame before
  and after into `user://` and name the files in the Delivery record.
- One commit per note, message naming the sample and the note number.
- Every fix keeps the stage hash unchanged (dressing never touches generation data): assert with the existing hash
  print on the sample seed before and after.
- Full harness plus the three archetype runs before the push.

## Non-goals

- Anything on the "Needs main track" list from the audit (route topology, line placement, validator verdicts, tube
  swing, floor-3 rarity, drain rules, spiral geometry).
- Difficulty, new features, new dressing types beyond what a note asks for.

## Acceptance

- Each note: the user's re-play verdict on the sample. The harness: unchanged count; stage hashes on the five sample
  seeds identical before and after (print them in the Delivery record).

## Files expected

`src/World/WorldDressing.cs`, `src/World/TubeMesh.cs`, `src/World/MovementToyWorld.cs` (structure materials),
`src/Generation/WorldScale.cs` (camera margins only, with a P-entry), docs 06 (the touched subsection), 10, this
packet, `STATUS.md`, `PROVISIONAL_DECISIONS.md`.

## Delivery record (filled by the implementing agent)

- Branch / commits (one per note):
- Harness (full run and the three archetype runs):
- Files changed:
- Per note: what was seen, what changed, before/after screenshot names, hash before/after:
- P-entries written:
- Spec sections edited:
- Open items:
- Needs main track (notes that needed a rule change, with the reason):
