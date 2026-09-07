# T7 — Tube judder while boosting (bug)

**Branch:** `opus/t7-tube-judder` off `develop-secondary`  
**Phase:** 3 defect (D-101 see-through tubes)  
**Gate lines served:** 08 §5 "the harness drives the ball into a tube at the cap and asserts it is carried through"; 08 §3 no solver chaos  
**Owning specs:** 04 §5I (tubes), 03 §3 (ground follow) and §12 (collision), 11 §7d; DECISIONS D-101, V-015  
**Reported:** 2026-09-07 by the user on `develop-secondary`, on several archetypes: the player model judders continuously
for seconds while **boosting through a tube**. Entry is fine; cruise without boost is fine. Not seen before the
branching-exits merge, which touched no tube, player or camera code.

## Hypothesis (unverified; verify before fixing)

The tube collider is a 10-sided polygon shell (`src/World/TubeMesh.cs`, `Sides = 10`, a `ConcavePolygonShape3D` with
backface collision on the structure layer). The tube follow (`src/Player/PlayerPhysics.cs`, `TryTubeFollow`) holds the
ball to the **analytic** circle: `gap = radius - BallRadius - dist` from `IStructureSurface.Nearest`, which returns
`TubeDefinition.Radius`. Between a facet's middle and its corners the polygon is R·(1 − cos(π/10)) ≈ 0.29 m inside the
circle. Boosting inside a tube raises the wall-ride angle and sweeps the ball across facet edges several times a
second, so the collider pushes the ball inward at each corner and the follow pulls it back out over
`GroundFollowCloseSeconds`. That is a judder that lasts exactly as long as the boost, on every archetype, and it is
invisible at cruise, where the ball sits low on the wall and crosses no edge. The harness rode one tube at the cap
without boost (D-101), which is why it passed.

Alternative causes to rule out, in order: (2) the visual (`PlayerVisual` squash and roll from an alternating contact
normal) rather than the body; (3) `TubeFollowActive` toggling because `vOut > GroundFollowSnapDistance / dt` trips at
boost speeds on a bend; (4) physics interpolation against a body whose velocity is rewritten every tick.

## Reproduce first

1. `-- --seed 9` (sample stage 1), enter the tube at 1.1 km, hold boost. Confirm the judder.
2. Harness: extend the tube ride (`tests/MovementToySelfTest.cs`, "Tube ride (08 §5, D-101)") with a second pass holding
   `InputBootstrap.Boost` from the mouth to the exit after `RefillBoost(BoostCapacity)`. Record per tick: radial distance
   from the axis, `TubeFollowActive`, `IsRawGrounded`, the contact normal. Print the largest tick-to-tick radial change
   and the count of follow-state flips. A judder shows as radial oscillation of about 0.1–0.3 m at 5–20 Hz.
3. Print the same trace at cruise (no boost) as the control.

## Fix (if the hypothesis holds)

1. `TubeMesh.Sides` 10 → 24. Facet error drops to R·(1 − cos(π/24)) ≈ 0.05 m. Vertex count per tube stays small
   (rings every few metres × 24). Keep the ribs and the mouth flare as they are.
2. In `TryTubeFollow`, hold the ball to the polygon's inscribed radius, not the analytic one:
   `float wall = radius * Mathf.Cos(Mathf.Pi / TubeMesh.Sides);` then `gap = wall - BallRadius - dist`. Inside a tube the
   collider then never fires while the follow is active, and the walls are analytic. Nothing else in the method changes;
   nothing outside tubes changes (the ground follow is untouched; `TubeContact` off must still equal the D-101 baseline
   minus the follow).
3. If the alternative (2) is the cause instead, the fix is in `PlayerVisual` (smooth the contact normal inside a tube),
   not in physics. If (3), widen only the tube branch's launch test, never the ground follow's.

## Boundaries

This packet may edit `TryTubeFollow` and `TubeMesh` only. It may not touch the ground follow, steering, boost, the cap,
or any compiled default. The golden hashes must not change (a tube's mesh is not in the hash; its axis is).

## Acceptance (harness)

- Boosted ride on the borrowed tube seed: carried 100% grounded, max tick-to-tick radial change < 0.03 m, follow-state
  flips ≤ 2 (mouth and exit), exit speed within 10% of the carried model at the boosted speed (report the number), lens
  outside the shell and sight clear as the existing ride asserts.
- The existing cruise ride unchanged (100% grounded, radial ≤ R − ball + 0.5, exit ≤ 20° off axis).
- Node count and golden hashes unchanged; full harness green plus `RUSHCORE_ARCHETYPE=canyon|dunes|sky`.

Manual: the user boosts through the sample tubes (stages 1 and 4) and the judder is gone.

## Files expected

`src/World/TubeMesh.cs`, `src/Player/PlayerPhysics.cs` (the tube follow only), `tests/MovementToySelfTest.cs`, docs 04 §5I
(one sentence: the follow holds the inscribed radius), 11 §7d (side count), this packet, `STATUS.md`,
`PROVISIONAL_DECISIONS.md` (P-entry: sides 24, inscribed-radius follow).

## Delivery record (filled by the implementing agent)

- Branch / commits:
- Harness (full run and the three archetype runs):
- Reproduction (trace numbers before: radial oscillation amplitude and rate, follow flips):
- Cause confirmed:
- Fix applied:
- Numbers after:
- P-entries written:
- Spec sections edited:
- Open items:
- Needs main track:
