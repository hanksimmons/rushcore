using Rushcore.Tuning;

namespace Rushcore.Run;

/// <summary>The eight stats a level-up may raise (docs/16 §4, D-119). The order is the panel's order.</summary>
public enum UpgradeStat { MaxSpeed, Acceleration, TurnRadius, BallSize, JumpHeight, SlamBoost, AutoBoostRefill, Hangtime }

/// <summary>
/// The run's stat ranks (docs/16 §4): one integer per stat, 0–3, and the multiplier each rank means. Rank 0 of every
/// stat is the frozen D-095 baseline: an upgrade is a factor read <em>beside</em> a tuning value at its point of use
/// (<see cref="Player.PlayerPhysics"/>), and the tuning, its presets and the harness's rank-0 drives never see it.
/// A plain class with no engine dependency; the run owns one and a new run resets it.
/// </summary>
public sealed class UpgradeState
{
    public const int StatCount = 8;
    public const int MaxRank = 3;

    private readonly int[] _rank = new int[StatCount];
    private readonly UpgradeTuning _t;

    public UpgradeState(UpgradeTuning tuning) => _t = tuning;

    /// <summary>A rank rose: the stat and its new rank.</summary>
    public event Action<UpgradeStat, int>? Changed;

    public int Rank(UpgradeStat stat) => _rank[(int)stat];
    public bool CanRaise(UpgradeStat stat) => _rank[(int)stat] < MaxRank;
    /// <summary>Every rank held, over all stats (0 on a fresh run).</summary>
    public int TotalRanks { get { int n = 0; foreach (int r in _rank) n += r; return n; } }

    /// <summary>Raises a stat one rank; false at rank 3.</summary>
    public bool Raise(UpgradeStat stat)
    {
        if (!CanRaise(stat)) return false;
        _rank[(int)stat]++;
        Changed?.Invoke(stat, _rank[(int)stat]);
        return true;
    }

    public void Reset() => Array.Clear(_rank);

    // ---- read points (docs/16 §4): what each stat multiplies, at its current rank ----

    /// <summary>× the base cap (`HardMaxLocomotionSpeed`); the D-088 effective cap multiplies the upgraded base.</summary>
    public float MaxSpeed => Pick(UpgradeStat.MaxSpeed, 1f, _t.MaxSpeed1, _t.MaxSpeed2, _t.MaxSpeed3);
    /// <summary>× ground drive (`GroundDriveAcceleration`); slopes and boost unchanged.</summary>
    public float Acceleration => Pick(UpgradeStat.Acceleration, 1f, _t.Acceleration1, _t.Acceleration2, _t.Acceleration3);
    /// <summary>× the steering lateral acceleration at every speed (both the low- and the high-speed authority).</summary>
    public float TurnRadius => Pick(UpgradeStat.TurnRadius, 1f, _t.TurnRadius1, _t.TurnRadius2, _t.TurnRadius3);
    /// <summary>× `BallRadius`: the collider, the visual and the follow's rest height.</summary>
    public float BallSize => Pick(UpgradeStat.BallSize, 1f, _t.BallSize1, _t.BallSize2, _t.BallSize3);
    /// <summary>× both jump takeoff speeds (height goes with the square).</summary>
    public float JumpHeight => Pick(UpgradeStat.JumpHeight, 1f, _t.JumpHeight1, _t.JumpHeight2, _t.JumpHeight3);
    /// <summary>The landing burst's factor: the tuning's own at rank 0, the rank's value above (D-088's 1.0–1.3 ceiling).</summary>
    public float BurstMultiplier(float baseline) => Pick(UpgradeStat.SlamBoost, baseline, _t.SlamBoost1, _t.SlamBoost2, _t.SlamBoost3);
    /// <summary>Boost regained per second while not boosting, added to `PassiveBoostRegen` (0 at rank 0, D-106).</summary>
    public float AutoRefillPerSecond => Pick(UpgradeStat.AutoBoostRefill, 0f, _t.AutoRefill1, _t.AutoRefill2, _t.AutoRefill3);
    /// <summary>× gravity (moon physics): the ball's gravity scale and the follow's launch threshold both read it.</summary>
    public float Gravity => Pick(UpgradeStat.Hangtime, 1f, _t.Hangtime1, _t.Hangtime2, _t.Hangtime3);

    /// <summary>The value a stat would take at a rank (the panel's "next rank" text and the harness read this).</summary>
    public float ValueAt(UpgradeStat stat, int rank, float baseline = 1f) => stat switch
    {
        UpgradeStat.MaxSpeed => At(rank, 1f, _t.MaxSpeed1, _t.MaxSpeed2, _t.MaxSpeed3),
        UpgradeStat.Acceleration => At(rank, 1f, _t.Acceleration1, _t.Acceleration2, _t.Acceleration3),
        UpgradeStat.TurnRadius => At(rank, 1f, _t.TurnRadius1, _t.TurnRadius2, _t.TurnRadius3),
        UpgradeStat.BallSize => At(rank, 1f, _t.BallSize1, _t.BallSize2, _t.BallSize3),
        UpgradeStat.JumpHeight => At(rank, 1f, _t.JumpHeight1, _t.JumpHeight2, _t.JumpHeight3),
        UpgradeStat.SlamBoost => At(rank, baseline, _t.SlamBoost1, _t.SlamBoost2, _t.SlamBoost3),
        UpgradeStat.AutoBoostRefill => At(rank, 0f, _t.AutoRefill1, _t.AutoRefill2, _t.AutoRefill3),
        _ => At(rank, 1f, _t.Hangtime1, _t.Hangtime2, _t.Hangtime3),
    };

    public static string Label(UpgradeStat stat) => stat switch
    {
        UpgradeStat.MaxSpeed => "MAX SPEED",
        UpgradeStat.Acceleration => "ACCELERATION",
        UpgradeStat.TurnRadius => "TURN RADIUS",
        UpgradeStat.BallSize => "BALL SIZE",
        UpgradeStat.JumpHeight => "JUMP HEIGHT",
        UpgradeStat.SlamBoost => "SLAM BOOST",
        UpgradeStat.AutoBoostRefill => "AUTO BOOST REFILL",
        _ => "HANGTIME",
    };

    /// <summary>What the next rank does, in words (docs/16 §3: "the effect of the next rank in words").</summary>
    public string Describe(UpgradeStat stat, int rank, float burstBaseline) => stat switch
    {
        UpgradeStat.MaxSpeed => $"speed cap ×{ValueAt(stat, rank):0.00}",
        UpgradeStat.Acceleration => $"drive ×{ValueAt(stat, rank):0.00}",
        UpgradeStat.TurnRadius => $"steering ×{ValueAt(stat, rank):0.00} at every speed",
        UpgradeStat.BallSize => $"ball ×{ValueAt(stat, rank):0.00}",
        UpgradeStat.JumpHeight => $"takeoff ×{ValueAt(stat, rank):0.00} (height ×{ValueAt(stat, rank) * ValueAt(stat, rank):0.00})",
        UpgradeStat.SlamBoost => $"landing burst ×{ValueAt(stat, rank, burstBaseline):0.00}",
        UpgradeStat.AutoBoostRefill => $"boost +{ValueAt(stat, rank):0.#} / s",
        _ => $"gravity ×{ValueAt(stat, rank):0.00}",
    };

    private float Pick(UpgradeStat stat, float r0, float r1, float r2, float r3) => At(_rank[(int)stat], r0, r1, r2, r3);
    private static float At(int rank, float r0, float r1, float r2, float r3) => rank switch { <= 0 => r0, 1 => r1, 2 => r2, _ => r3 };
}
