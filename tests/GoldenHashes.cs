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
        // Recorded 2026-09-07 with D-105 (branching exits).
        ["tube"] = "7D24840659B28767",
        ["tunnels + pit"] = "327D9152EADB87E0",
        ["sky floor 3"] = "C453C38B4481CEB3",
        ["dune trains"] = "1903C69DC8E57D46",
        ["gap + turns"] = "3CA4CC5E70D0F945",
        ["three exits"] = "DE6A618E62DEB3EE",
    };
}
