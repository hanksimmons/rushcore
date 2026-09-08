using Rushcore.Generation;

namespace Rushcore.Run;

/// <summary>
/// The run's explicit lifecycle state (02 §2, 09 Phase 5/6): which run, which stage of it, and which
/// exit the last stage was left by. A plain class with no engine dependency: the composition root owns
/// one and hands its <see cref="Request"/> to the world, so the stage index stops being a hard-coded 0.
///
/// <para>Currency, XP, route cards, difficulty and death are later phases and deliberately absent.</para>
/// </summary>
public sealed class RunDirector
{
    /// <summary>Stages in one run (02 §2). Reaching the end wraps to 0 until the run summary exists (Phase 10).</summary>
    public const int StageCount = 9;

    private static readonly TerrainArchetype[] Archetypes =
        { TerrainArchetype.RollingHighlands, TerrainArchetype.CanyonRun, TerrainArchetype.DuneSea, TerrainArchetype.SkyTerraces };

    public RunDirector(int runSeed) => RunSeed = runSeed;

    public int RunSeed { get; private set; }
    public int StageIndex { get; private set; }
    /// <summary>Exit index the current stage was entered by; −1 on the first stage of a run.</summary>
    public int EnteredByExit { get; private set; } = -1;
    /// <summary>Stages completed since the run started; the run wrap resets nothing else.</summary>
    public int StagesCompleted { get; private set; }
    /// <summary>The run's wallet (02 §14, T2's HUD reads it). T3's reward burst is what fills it; nothing spends
    /// it yet, and the shop that will is Phase 8.</summary>
    public int Currency { get; private set; }

    public void AddCurrency(int amount)
    {
        if (amount > 0) Currency += amount;
    }
    /// <summary>
    /// Debug and harness affordance (P-012): with this off, reaching an exit still raises completion and
    /// still prints, but no outro runs and no next stage is built, so one stage can be driven repeatedly.
    /// </summary>
    public bool AutoAdvance { get; set; } = true;

    /// <summary>Starts a run at a stage index: the `--stage N` flag, a new seed, and the sample stages.</summary>
    public void StartRun(int runSeed, int stageIndex = 0)
    {
        RunSeed = runSeed;
        StageIndex = System.Math.Clamp(stageIndex, 0, StageCount - 1);
        EnteredByExit = -1;
        StagesCompleted = 0;
        Currency = 0;
    }

    /// <summary>The generation request for the current stage of the current run.</summary>
    public StageGenerationRequest Request(TerrainArchetype archetype) => new(RunSeed, StageIndex, archetype);

    /// <summary>Moves to the next stage of the same run. Returns true when the run wrapped past its last stage.</summary>
    public bool Advance(int exitIndex)
    {
        StagesCompleted++;
        EnteredByExit = exitIndex;
        StageIndex++;
        if (StageIndex < StageCount) return false;
        StageIndex = 0;
        return true;
    }

    /// <summary>
    /// The exit taken picks the next stage's archetype (P-011): exit A (index 0, the primary's pad) continues
    /// the one just played; every further exit lands in a different landscape, chosen from the seed chain so the
    /// same run and the same exit always give the same next stage. Bounded: after eight equal draws the next
    /// archetype in order is taken, so a fork always changes the landscape.
    /// </summary>
    public static TerrainArchetype NextArchetype(ulong stageSeed, TerrainArchetype current, int exitIndex)
    {
        if (exitIndex <= 0) return current;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            ulong h = SeedChain.Derive(stageSeed, "route", exitIndex + attempt * Archetypes.Length);
            var pick = Archetypes[(int)(h % (ulong)Archetypes.Length)];
            if (pick != current) return pick;
        }
        return Archetypes[(System.Array.IndexOf(Archetypes, current) + 1) % Archetypes.Length];
    }
}
