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
    string SeedText { get; }

    void RestartSameSeed();
    void RestartNewSeed();
    void RecoverPlayer();
    void TeleportToStart();
    void RefillBoost();
    void CopySeedToClipboard();
}
