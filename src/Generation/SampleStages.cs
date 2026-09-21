namespace Rushcore.Generation;

/// <summary>
/// Sample stages (docs/10): named seeds that show one structure each, for the manual sample of Gate G0. Index 0 is
/// none; the F1 panel's <c>World › Sample Stage</c> selects one, and the runbook lists the equivalent command lines.
/// </summary>
public static class SampleStages
{
    public readonly record struct Entry(string Name, TerrainArchetype Archetype, int Seed, string What);

    public static readonly Entry[] All =
    {
        new("tunnel", TerrainArchetype.CanyonRun, 1, "six portal tunnels through the canyon wall and six lids over 20 km (docs/13, D-113, D-118)"),
        new("dive", TerrainArchetype.RollingHighlands, 1, "a dive beneath the Highlands (the open cut, the portal, the cap over it) with a ridge and two exit lines (docs/13, D-116)"),
        new("tunnels + pit", TerrainArchetype.CanyonRun, 2, "six lids, five portals and the spiral pit finale (D-102)"),
        new("sky floor 3", TerrainArchetype.SkyTerraces, 6, "a floor-2 and floor-3 terrace stack among three dives (D-103; seed 30, then 32, until D-118 lengthened the course)"),
        new("dune trains", TerrainArchetype.DuneSea, 1, "fourteen crests in dune trains on the wave, and a dive (D-099)"),
        new("gap + turns", TerrainArchetype.RollingHighlands, 8, "a mandatory gap and five committed banked turns, three dives (D-097)"),
        new("three exits", TerrainArchetype.RollingHighlands, 4, "two terminal lines forking to exits B and C beside exit A, a dive and five ridges (D-105)"),
    };

    /// <summary>The entry an index selects, or null for 0 / out of range.</summary>
    public static Entry? At(int index) => index >= 1 && index <= All.Length ? All[index - 1] : null;

    public static string Label => "0 none, " + string.Join(", ", All.Select((e, i) => $"{i + 1} {e.Name}"));
}
