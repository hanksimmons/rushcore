namespace Rushcore.Testing;

/// <summary>
/// Known seeds the harness always generates (08 §11): stages that once failed, exercised a fix,
/// or sit at the edge of the validated ranges. Each entry is the stage request as the telemetry
/// seed row shows it (world seed / stage index) plus the reason it is here. The harness asserts
/// every entry still generates a valid stage without the known-safe fallback; add a seed the
/// moment a generation bug is reproduced, before fixing it, and leave it in once fixed.
/// </summary>
public static class RegressionSeeds
{
    public readonly record struct Entry(int RunSeed, int StageIndex, string Why, Rushcore.Generation.TerrainArchetype Archetype = Rushcore.Generation.TerrainArchetype.RollingHighlands);

    public static readonly Entry[] All =
    {
        // Refreshed 2026-09-07 (D-099): the builder's clipped-feature and clipped-turn rules moved every wander, so the
        // reasons describe the stages as they generate now.
        new(20260905, 0, "toy default seed: the first playtested stage (one launch crest, 2 ridge lines)"),
        new(1, 0, "the harness drive seed: two committed banked turns, no feature straight, 2 ridge lines"),
        new(1, 1, "a mandatory gap on the first attempt"),
        new(8, 0, "a mandatory gap plus four committed banked turns on one route"),
        new(15, 5, "a launch ramp and a banked turn (once the grade-delta regeneration case, D-084)"),
        new(57, 5, "a gap and four committed banked turns with two ridge lines"),
        new(4, 8, "a launch ramp, one banked turn and two ridge lines"),
        new(34, 6, "a gap, a banked turn and two ridge lines"),
        new(13, 5, "a launch ramp and two banked turns (once the regeneration bound's edge)"),
        new(90, 5, "a gap and three banked turns: the longest reservations"),
        new(7, 4, "attempt 1 fails the ceiling bend clearance (a swell flight lands too fast for an r100 bend); attempt 2 recovers with a gap"),
        new(20260905, 0, "the toy default seed as a Canyon Run (D-098): two launch ramps and a banked turn between slot walls", Rushcore.Generation.TerrainArchetype.CanyonRun),
        new(3, 0, "Canyon Run with slot walls through two gap modules", Rushcore.Generation.TerrainArchetype.CanyonRun),
        new(20260905, 0, "the toy default seed as a Dune Sea (D-099): the harness dune drive seed, a two-crest train and a lone dune", Rushcore.Generation.TerrainArchetype.DuneSea),
        new(1, 0, "Dune Sea with a train of three crests on the wave", Rushcore.Generation.TerrainArchetype.DuneSea),
        new(1, 8, "Dune Sea: attempt 1 fails a ramp's free path after a three-crest train; attempt 2 recovers", Rushcore.Generation.TerrainArchetype.DuneSea),
    };
}
