namespace Rushcore.Testing;

/// <summary>
/// Golden stage hashes (08 §5, §11): the hash every sample stage (docs/10, <c>SampleStages</c>) generates today. The
/// harness regenerates each sample data-only and asserts the hash is unchanged, so a change to generation, intended or
/// not, fails the run at once. When the main track changes generation on purpose, it updates this table in the same
/// commit and says so in the decision; the parallel track (docs/handoff) never edits it: a changed golden hash there
/// means a generator rule moved, which is "Needs main track".
/// </summary>
public static class GoldenHashes
{
    /// <summary>Sample name → hash (as <c>StageDefinition.Hash()</c> prints, upper-case hex). Empty = not yet recorded.</summary>
    public static readonly Dictionary<string, string> BySample = new()
    {
        // Recorded 2026-09-07 with D-105 (branching exits); "sky floor 3" and "dune trains" re-recorded 2026-09-19 with D-112
        // (tubes removed: their tube axes left the hash); "tunnel" recorded and "tunnels + pit" re-recorded 2026-09-19 with D-113
        // (portal tunnels take ridge sites on Canyon Run by a new draw in the line stream: every canyon hash moved, no other did).
        // "dive" recorded and every other hash re-recorded 2026-09-20 with D-116 (dives: the open archetypes draw for a tunnel at
        // every site, so their line streams moved) and D-117 (every offset line's ramps are shaped in the line's own distance and
        // the speed model's launch stencil reads uneven vertex spacing correctly, so every line's floor and every drop verdict
        // moved, the canyon's included).
        ["tunnel"] = "595643CE709110A2",
        ["dive"] = "3B14E37EF17368B8",
        ["tunnels + pit"] = "FFBE3B5C19379B39",
        ["sky floor 3"] = "DF440506AAB0BA1B",
        ["dune trains"] = "36A4187294C461EB",
        ["gap + turns"] = "DE332761DFB63765",
        ["three exits"] = "A26047A4F64EA28D",
    };
}
