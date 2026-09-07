namespace Rushcore.Generation;

/// <summary>
/// What an archetype changes (04 §6, §7): geometry only, never the player's physics. One record per
/// archetype, read by the skeleton builder (bend mix, straight lengths), the height field (walls,
/// falloffs, bank) and the validators. Rolling Highlands is the D-085 family; Canyon Run (D-098) is
/// the same corridor cut as a channel through raised side terrain with slot walls.
/// </summary>
public sealed record ArchetypeRules(
    TerrainArchetype Archetype,
    /// <summary>Raised side terrain above the corridor floor: 0 for open landscapes, the wall height for a canyon.</summary>
    float WallHeightMin, float WallHeightMax,
    /// <summary>Distance over which the corridor blends into the side terrain on straights and bend outsides.</summary>
    float WallFalloff,
    /// <summary>The same on the inside of a bend, kept long so the inside wall never hides the read horizon (04 §10 blind corners).</summary>
    float InsideFalloff,
    /// <summary>Bend mix: chance of the cruise radius, then of the fast radius; the rest are committed bends.</summary>
    float CruiseWeight, float FastWeight,
    float StraightMin, float StraightMax,
    /// <summary>Multiplier on the family bank (18/145 · r) on every bend.</summary>
    float BankScale)
{
    public static readonly ArchetypeRules RollingHighlands = new(
        TerrainArchetype.RollingHighlands, 0f, 0f, StageHeightField.FalloffWidth, StageHeightField.FalloffWidth,
        0.50f, 0.35f, 250f, 600f, 1f);

    /// <summary>Canyon Run (04 §6): a winding low channel between walls, broad banked bends, the inside wall set back.</summary>
    public static readonly ArchetypeRules CanyonRun = new(
        TerrainArchetype.CanyonRun, WorldScale.CanyonWallHeightMin, WorldScale.CanyonWallHeightMax, WorldScale.CanyonWallFalloff, StageHeightField.FalloffWidth,
        0.45f, 0.45f, 200f, 450f, WorldScale.CanyonBankScale);

    public static ArchetypeRules For(TerrainArchetype archetype) => archetype == TerrainArchetype.CanyonRun ? CanyonRun : RollingHighlands;
}
