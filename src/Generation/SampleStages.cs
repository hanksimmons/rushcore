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
        new("tube", TerrainArchetype.RollingHighlands, 9, "a see-through tube at 1.1 km (D-101)"),
        new("tunnels + pit", TerrainArchetype.CanyonRun, 2, "two wall tunnels and the spiral pit finale (D-102)"),
        new("sky floor 3", TerrainArchetype.SkyTerraces, 30, "a floor-2 and floor-3 terrace stack (D-103)"),
        new("dune trains", TerrainArchetype.DuneSea, 1, "a three-crest dune train and a tube (D-099)"),
        new("gap + turns", TerrainArchetype.RollingHighlands, 8, "a mandatory gap and four committed banked turns (D-097)"),
    };

    /// <summary>The entry an index selects, or null for 0 / out of range.</summary>
    public static Entry? At(int index) => index >= 1 && index <= All.Length ? All[index - 1] : null;

    public static string Label => "0 none, " + string.Join(", ", All.Select((e, i) => $"{i + 1} {e.Name}"));
}
