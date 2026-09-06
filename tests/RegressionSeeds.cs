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
        new(20260905, 0, "toy default seed: the first playtested stage (2 ridge lines, 2 crests)"),
        new(8, 0, "route attempt 1 failed; regeneration path (D-084) must recover on attempt 2"),
        new(15, 5, "route attempt 1 failed; regeneration path (D-084) must recover on attempt 2"),
        new(57, 5, "attempt 2 with four committed 50 m bends"),
        new(4, 8, "no ridge line fits (the ~5% of seeds without optional lines)"),
        new(34, 6, "three ridge lines and no launch crest"),
        new(13, 5, "no launch crest: checkpoints and time without a feature straight"),
        new(90, 5, "slowest base-kit time and the longest route in the 1000-seed scan (upper bounds)"),
        new(7, 4, "fastest base-kit time in the 1000-seed scan (lower bound)"),
    };
}
