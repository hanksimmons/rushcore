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
        // Recorded 2026-09-07 with D-105 (branching exits); "tunnels + pit" re-recorded 2026-09-19 with D-109 (the wall
        // profile); "sky floor 3" and "dune trains" re-recorded 2026-09-19 with D-112 (tubes removed: their tube axes left the hash).
        ["tunnels + pit"] = "6222AEE6856136A7",
        ["sky floor 3"] = "4C9CB24BB5B51F1D",
        ["dune trains"] = "347EA2AC04366D37",
        ["gap + turns"] = "3CA4CC5E70D0F945",
        ["three exits"] = "DE6A618E62DEB3EE",
    };
}
