using Rushcore.Player;
using Rushcore.Tuning;
using Rushcore.World;

namespace Rushcore.Core;

/// <summary>
/// The debug UI observes gameplay state and asks the composition root to perform
/// developer actions; it never reaches into system internals (05 §4).
/// </summary>
public interface IDebugActions
{
    GameplayTuning Tuning { get; }
    PlayerPhysics Player { get; }
    MovementToyWorld World { get; }
    /// <summary>The run's seed and stage index (T1); the HUD and the panel read it, nobody else advances it.</summary>
    Rushcore.Run.RunDirector Run { get; }
    /// <summary>The player's health value (T2). Nothing damages it yet; the debug actions kill and heal it.</summary>
    PlayerHealth Health { get; }
    /// <summary>The player HUD (T2); the harness measures the widths it draws.</summary>
    Rushcore.UI.PlayerHud Hud { get; }
    string SeedText { get; }
    /// <summary>True while the stage-completion outro is playing (controls locked, fade running).</summary>
    bool StageOutroActive { get; }

    void RestartSameSeed();
    void RestartNewSeed();
    void RecoverPlayer();
    void TeleportToStart();
    /// <summary>Drops the ball on the primary 200 m short of the exit pad (07 §12).</summary>
    void TeleportNearExit();
    /// <summary>Starts a run at a stage index and rebuilds; the `--stage N` flag takes this path.</summary>
    void StartRun(int runSeed, int stageIndex);
    void RefillBoost();
    /// <summary>Debug (07 §12): take the player to zero health, which recovers and refills (P-004).</summary>
    void KillPlayer();
    /// <summary>Debug (07 §12): back to full health.</summary>
    void HealPlayer();
    /// <summary>Debug (07 §12, T3): the crush one-shot on the showcase Pylon, or at the ball if the row is off.</summary>
    void PlayCrush();
    /// <summary>Debug (07 §12, T3): the failed-impact one-shot on the showcase Bulwark, or at the ball.</summary>
    void PlayFailedImpact();
    /// <summary>Debug (07 §12, T3): the damage pulse on the ball.</summary>
    void PlayDamage();
    /// <summary>Debug (07 §12, T3): throw a burst of twelve coins at the ball and let it magnetise them in (P-007).</summary>
    void BurstCoins();
    void CopySeedToClipboard();
}
