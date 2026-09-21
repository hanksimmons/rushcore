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
        // Refreshed 2026-09-20 (D-118, the 3× course): every stage is three times longer, so the reasons describe what each
        // seed carries now (the harness prints the same summary beside every entry); the seeds themselves are the ones that
        // once failed or drove a fix, kept for their history.
        new(20260905, 0, "toy default seed: four launch ramps, a gap and four banked turns; a dive and three ridges; three exits"),
        new(1, 0, "the harness drive seed: three ramps, two gaps, five banked turns, no crest; a dive (D-116) and a ridge; three exits"),
        new(1, 1, "four gaps on one route with a ramp and a banked turn; three dives among six lines"),
        new(8, 0, "a mandatory gap plus five committed banked turns on one route; three dives (the 'gap + turns' sample)"),
        new(15, 5, "two ramps, two gaps and four banked turns; five ridges (once the grade-delta regeneration case, D-084)"),
        new(57, 5, "a gap, two ramps and six banked turns with a dive and two ridges"),
        new(4, 8, "three ramps, a gap and seven banked turns; a dive among four ridges; three exits"),
        new(34, 6, "a gap, a ramp and seven banked turns with a dive and four ridges"),
        new(13, 5, "two ramps, a gap and two banked turns (once the regeneration bound's edge; attempt 1 still fails the grade limit)"),
        new(90, 5, "seven banked turns with a ramp and a gap; three dives among seven lines: the longest reservations"),
        new(7, 4, "attempt 1 fails the ceiling bend clearance (a swell flight lands too fast for a bend); attempt 2 recovers with two gaps"),
        new(20260905, 0, "the toy default seed as a Canyon Run (D-098): five portal tunnels, six lids, a ramp, a gap and four banked turns between slot walls", Rushcore.Generation.TerrainArchetype.CanyonRun),
        new(3, 0, "Canyon Run with slot walls through two gap modules and three portals; attempt 1 fails the ceiling bend clearance", Rushcore.Generation.TerrainArchetype.CanyonRun),
        new(20260905, 0, "the toy default seed as a Dune Sea (D-099): the harness dune drive seed, twelve crests in trains on the wave, no line", Rushcore.Generation.TerrainArchetype.DuneSea),
        new(1, 0, "Dune Sea with fourteen crests in trains on the wave, a dive and an exit line", Rushcore.Generation.TerrainArchetype.DuneSea),
        new(1, 8, "Dune Sea: eleven crests with two launch ramps and three banked turns (once the too-narrow launch window's failure)", Rushcore.Generation.TerrainArchetype.DuneSea),
    };
}
