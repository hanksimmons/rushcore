using Godot;

namespace Rushcore.Generation;

/// <summary>
/// What an archetype changes (04 §6, §7): geometry only, never the player's physics. One record per
/// archetype, read by the skeleton builder (feature mix, bend mix, straight lengths, the dune train),
/// the height field (walls, falloffs, bank, the swell budget, the dune wave) and the validators.
/// Rolling Highlands is the D-085 family; Canyon Run (D-098) is the same corridor cut as a channel
/// through raised side terrain with slot walls; Dune Sea (D-099) rides a seeded dune wave as trains of
/// launch crests.
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
    float BankScale,
    /// <summary>Summed slope (tan) the long swells may reach: the corridor grade left over for a crest on top.</summary>
    float SwellMaxSlope,
    /// <summary>Route between feature straights, the chance a straight at that spacing hosts one, and the
    /// feature mix (crest or dune train, then gap; the rest are ramps).</summary>
    float FeatureSpacing, float FeatureChance, float CrestWeight, float GapWeight,
    /// <summary>Crests per dune train (04 §6 Dune Sea); 0 on archetypes without the dune wave.</summary>
    int TrainCrestsMin, int TrainCrestsMax,
    /// <summary>Largest heading off the stage axis: 45° is the family's wander; a dune sea keeps to 12° so its
    /// 2 km train straights fit the route band.</summary>
    float MaxHeading,
    /// <summary>Chance a clear section hosts a see-through tube (04 §5I, D-101); 0 = no tubes.</summary>
    float TubeChance,
    /// <summary>Chance the stage ends in a spiral pit (04 §6 Canyon Run set-piece, D-102); 0 = never.</summary>
    float SpiralChance,
    /// <summary>Chance a clear slot straight carries a lid, a wall tunnel (D-102); 0 = no lids (an open landscape has no slot to roof).</summary>
    float LidChance,
    /// <summary>Sky Terraces (D-103): the highest floor its optional lines climb to (1 = ridge lines only, 3 = terraces on floors 2 and 3).</summary>
    int Floors)
{
    public bool HasDunes => TrainCrestsMax > 0;

    public static readonly ArchetypeRules RollingHighlands = new(
        TerrainArchetype.RollingHighlands, 0f, 0f, StageHeightField.FalloffWidth, StageHeightField.FalloffWidth,
        0.50f, 0.35f, 250f, 600f, 1f,
        WorldScale.LongSwellMaxSlope, WorldScale.LaunchCrestSpacing, 0.6f, 0.4f, 0.35f, 0, 0, Mathf.Pi / 4f, 0.6f, 0f, 0f, 1);

    /// <summary>Canyon Run (04 §6): a winding low channel between walls, broad banked bends, the inside wall set back.</summary>
    public static readonly ArchetypeRules CanyonRun = new(
        TerrainArchetype.CanyonRun, WorldScale.CanyonWallHeightMin, WorldScale.CanyonWallHeightMax, WorldScale.CanyonWallFalloff, StageHeightField.FalloffWidth,
        0.45f, 0.45f, 200f, 450f, WorldScale.CanyonBankScale,
        WorldScale.LongSwellMaxSlope, WorldScale.LaunchCrestSpacing, 0.6f, 0.4f, 0.35f, 0, 0, Mathf.Pi / 4f, 0.6f, 0.5f, 0.7f, 1);

    /// <summary>Dune Sea (04 §6): broad repeating waves, most straights a train of launch crests, cruise-heavy bends.</summary>
    public static readonly ArchetypeRules DuneSea = new(
        TerrainArchetype.DuneSea, 0f, 0f, StageHeightField.FalloffWidth, StageHeightField.FalloffWidth,
        0.60f, 0.30f, 250f, 600f, 1f,
        WorldScale.DuneSwellMaxSlope, 300f, 0.85f, 0.7f, 0.15f, WorldScale.DuneTrainCrestsMin, WorldScale.DuneTrainCrestsMax, Mathf.Pi / 15f, 0.6f, 0f, 0f, 1);

    /// <summary>Sky Terraces (04 §6, D-103): the Highlands family with terrace lines on two floors above the primary and a
    /// corridor falloff long enough that every terrace edge drains inside the route grade.</summary>
    public static readonly ArchetypeRules SkyTerraces = new(
        TerrainArchetype.SkyTerraces, 0f, 0f, WorldScale.SkyPrimaryFalloff, WorldScale.SkyPrimaryFalloff,
        0.50f, 0.35f, 500f, 900f, 1f,
        WorldScale.LongSwellMaxSlope, WorldScale.LaunchCrestSpacing, 0.6f, 0.4f, 0.35f, 0, 0, Mathf.Pi / 4f, 0.6f, 0f, 0f, 3);

    public static ArchetypeRules For(TerrainArchetype archetype) => archetype switch
    {
        TerrainArchetype.CanyonRun => CanyonRun,
        TerrainArchetype.DuneSea => DuneSea,
        TerrainArchetype.SkyTerraces => SkyTerraces,
        _ => RollingHighlands,
    };

    /// <summary>Short label for summaries and check names.</summary>
    public static string Label(TerrainArchetype archetype) => archetype switch
    {
        TerrainArchetype.CanyonRun => "canyon",
        TerrainArchetype.DuneSea => "dunes",
        TerrainArchetype.SkyTerraces => "sky",
        _ => "highlands",
    };
}
