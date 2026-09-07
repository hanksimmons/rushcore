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
    public readonly record struct Entry(int RunSeed, int StageIndex, string Why);

    public static readonly Entry[] All =
    {
        new(20260905, 0, "toy default seed: the first playtested stage (one launch crest, 2 ridge lines since D-097)"),
        new(1, 0, "the harness drive seed with a launch ramp (D-097); attempt 1 failed, regeneration recovers on attempt 2"),
        new(1, 1, "a mandatory gap on the first attempt"),
        new(8, 0, "a mandatory gap plus four committed banked turns on one route"),
        new(15, 5, "route attempt 1 failed on the grade-delta limit; regeneration (D-084) recovers on attempt 2 with a gap"),
        new(57, 5, "one committed bend and two ridge lines, no feature straight"),
        new(4, 8, "no feature straight and three ridge lines: checkpoints and time without a module"),
        new(34, 6, "a gap, a banked turn and two ridge lines"),
        new(13, 5, "three attempts failed; the fourth recovers without the fallback (the regeneration bound's edge)"),
        new(90, 5, "two feature straights (a gap among them) and four banked turns: the longest reservations"),
        new(7, 4, "attempt 1 failed the ceiling bend clearance; attempt 2 recovers with a gap"),
    };
}
