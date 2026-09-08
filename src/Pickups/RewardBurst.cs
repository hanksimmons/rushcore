using Godot;
using Rushcore.Vfx;

namespace Rushcore.Pickups;

/// <summary>
/// The currency burst-and-magnetise presentation (02 §12, 06 §9 "currency may burst physically, then
/// magnetize/auto-collect to preserve momentum"; P-007).
///
/// <para>Coins are kinematic: a code-integrated ballistic arc for <see cref="FreeFlightSeconds"/>, then a
/// magnetise toward the ball that eases up to <see cref="MagnetSpeed"/> and collects inside
/// <see cref="CollectRadius"/>. No physics bodies, no colliders, and above all no force on the ball — the
/// whole point of auto-collect is that momentum is never the price of a reward (02 §12).</para>
///
/// <para>One node per burst, not per coin: the coins are instances of a single <see cref="MultiMesh"/> whose
/// per-coin state lives in plain arrays. The burst frees itself when the last coin is collected, or at
/// <see cref="MaxSeconds"/> if a coin can never reach the player, so a stage cannot accumulate them.</para>
/// </summary>
public partial class RewardBurst : Node3D
{
    /// <summary>Ballistic flight before the magnet takes over (P-007).</summary>
    public const float FreeFlightSeconds = 0.4f;
    /// <summary>Top magnetise speed. Above the ball's cap, so a coin catches a player who is leaving.</summary>
    public const float MagnetSpeed = 60f;
    /// <summary>Collected inside this radius of the ball.</summary>
    public const float CollectRadius = 1.2f;
    /// <summary>A burst never outlives this, collected or not.</summary>
    public const float MaxSeconds = 6f;

    private const float Gravity = 34f;
    private const float SpinRate = 7f;

    private MultiMeshInstance3D _draw = null!;
    private MultiMesh _mesh = null!;
    private Vector3[] _position = System.Array.Empty<Vector3>();
    private Vector3[] _velocity = System.Array.Empty<Vector3>();
    private float[] _spin = System.Array.Empty<float>();
    private bool[] _gone = System.Array.Empty<bool>();
    private Func<Vector3>? _player;
    private WorldVfx? _vfx;
    private float _age;

    /// <summary>One event per coin as it lands in the wallet: the kind, and what that coin is worth.</summary>
    public event Action<PickupKind, int>? Collected;

    public PickupKind Kind { get; private set; } = PickupKind.Currency;
    /// <summary>Coins still in the air.</summary>
    public int Remaining { get; private set; }
    /// <summary>Coins this burst has delivered.</summary>
    public int CollectedCount { get; private set; }

    /// <summary>
    /// Throws <paramref name="count"/> coins off <paramref name="origin"/> and adds the burst under
    /// <paramref name="parent"/>. The arcs are seeded from <paramref name="rng"/>, so a burst is reproducible
    /// when the caller wants it to be and incidental when it does not.
    /// </summary>
    public static RewardBurst Spawn(Node parent, Vector3 origin, PickupKind kind, int count,
                                    RandomNumberGenerator rng, Func<Vector3> player, WorldVfx? vfx = null)
    {
        count = Mathf.Clamp(count, 1, 64);
        var burst = new RewardBurst
        {
            Name = "RewardBurst",
            Kind = kind,
            _player = player,
            _vfx = vfx,
            _position = new Vector3[count],
            _velocity = new Vector3[count],
            _spin = new float[count],
            _gone = new bool[count],
            Remaining = count,
        };

        for (int i = 0; i < count; i++)
        {
            float a = rng.RandfRange(0f, Mathf.Tau);
            float out01 = rng.RandfRange(0.35f, 1f);
            burst._position[i] = origin;
            burst._velocity[i] = new Vector3(Mathf.Cos(a) * out01 * 9f, rng.RandfRange(7f, 13f), Mathf.Sin(a) * out01 * 9f);
            burst._spin[i] = rng.RandfRange(0f, Mathf.Tau);
        }

        parent.AddChild(burst);
        return burst;
    }

    public override void _Ready()
    {
        int n = _position.Length;
        _mesh = new MultiMesh
        {
            Mesh = PlaceholderPalette.Coin,
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = n,
            VisibleInstanceCount = n,
        };
        _draw = new MultiMeshInstance3D
        {
            Name = "Coins",
            Multimesh = _mesh,
            MaterialOverride = Kind == PickupKind.Reward ? PlaceholderPalette.Reward : PlaceholderPalette.Currency,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        };
        AddChild(_draw);
        WriteTransforms();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        Vector3 ball = _player?.Invoke() ?? Vector3.Zero;

        for (int i = 0; i < _position.Length; i++)
        {
            if (_gone[i]) continue;

            if (_age < FreeFlightSeconds)
            {
                _velocity[i] += Vector3.Down * Gravity * dt;
            }
            else
            {
                // Ease into the magnet over the first quarter second so the arc is still legible: a coin that
                // snapped to the ball the instant the flight ended would read as a teleport, not a reward.
                Vector3 to = ball - _position[i];
                float distance = to.Length();
                if (distance > 0.001f)
                {
                    float ramp = Mathf.Clamp((_age - FreeFlightSeconds) / 0.25f, 0f, 1f);
                    Vector3 want = to / distance * MagnetSpeed * ramp;
                    _velocity[i] = _velocity[i].Lerp(want, Mathf.Clamp(dt * 9f, 0f, 1f));
                }
            }

            _position[i] += _velocity[i] * dt;
            _spin[i] += SpinRate * dt;

            if (_age >= FreeFlightSeconds && _position[i].DistanceTo(ball) <= CollectRadius) Collect(i);
        }

        WriteTransforms();

        if (Remaining == 0 || _age >= MaxSeconds) QueueFree();
    }

    private void Collect(int i)
    {
        _gone[i] = true;
        Remaining--;
        CollectedCount++;
        _vfx?.Play(WorldVfxKind.Pickup, _position[i]);
        Collected?.Invoke(Kind, 1);
    }

    /// <summary>
    /// Coins are drawn in world space (the instance is top-level), so the transforms are absolute. A collected
    /// coin is scaled to nothing rather than removed: shrinking the instance count would renumber the rest.
    /// </summary>
    private void WriteTransforms()
    {
        for (int i = 0; i < _position.Length; i++)
        {
            if (_gone[i])
            {
                _mesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), _position[i]));
                continue;
            }
            var basis = new Basis(Vector3.Up, _spin[i]) * new Basis(Vector3.Right, Mathf.Pi * 0.5f);
            _mesh.SetInstanceTransform(i, new Transform3D(basis.Scaled(new Vector3(0.9f, 0.14f, 0.9f)), _position[i]));
        }
    }
}
