using Godot;
using Rushcore.Player;
using Rushcore.Tuning;
using Rushcore.Vfx;

namespace Rushcore.Pickups;

/// <summary>
/// A stage's experience orbs and cash balls (docs/16 §2, D-119): one node, a <see cref="MultiMesh"/> per kind, plain
/// arrays of state, no physics bodies and no colliders (the reward burst of P-007 is the model). Every physics tick it
/// walks the live pickups: inside <see cref="RunTuning.MagnetRadius"/> of the ball a pickup is magnetised and thereafter
/// chases at max(<see cref="RunTuning.MagnetSpeed"/>, ball speed + <see cref="RunTuning.MagnetClosingMargin"/>) until it
/// is within <see cref="CollectRadius"/>, when it is collected. A magnetised pickup never gives up: at the cap the ball
/// outruns nothing. Nothing here touches the ball (02 §12: never a price on momentum).
/// </summary>
public partial class PickupField : Node3D
{
    public const float CollectRadius = 1.5f;
    /// <summary>Diameters (docs/16 §2): a small orb, a larger ball.</summary>
    public const float OrbSize = 0.5f, CashSize = 0.8f;
    /// <summary>Idle pickups farther than this from the ball keep their rest transform (no per-tick write).</summary>
    public const float AnimateRange = 500f;

    private const byte Idle = 0, Magnetised = 1, Gone = 2;

    private readonly RunTuning _t;
    private readonly PlacedPickup[] _placed;
    private readonly Func<PlayerPhysics?> _player;
    private readonly WorldVfx? _vfx;
    private readonly Vector3[] _pos;
    private readonly float[] _phase;
    private readonly byte[] _state;
    private readonly int[] _slot;
    private readonly bool[] _animated;
    private MultiMesh _orbs = null!, _cash = null!;
    private float _clock;

    /// <summary>A pickup landed: its kind and its worth (1).</summary>
    public event Action<FieldPickupKind, int>? Collected;

    public PickupField(RunTuning tuning, IReadOnlyList<PlacedPickup> placed, Func<PlayerPhysics?> player, WorldVfx? vfx)
    {
        _t = tuning;
        _placed = placed.ToArray();
        _player = player;
        _vfx = vfx;
        int n = _placed.Length;
        _pos = new Vector3[n];
        _phase = new float[n];
        _state = new byte[n];
        _slot = new int[n];
        _animated = new bool[n];
        int orbs = 0, cash = 0;
        for (int i = 0; i < n; i++)
        {
            _pos[i] = _placed[i].Position;
            _phase[i] = (i * 0.61803f) % 1f * Mathf.Tau;
            _slot[i] = _placed[i].Kind == FieldPickupKind.Orb ? orbs++ : cash++;
        }
        OrbCount = orbs;
        CashCount = cash;
    }

    public IReadOnlyList<PlacedPickup> Placed => _placed;
    public int OrbCount { get; }
    public int CashCount { get; }
    public int CollectedOrbs { get; private set; }
    public int CollectedCash { get; private set; }
    /// <summary>Pickups magnetised so far (collected ones included).</summary>
    public int MagnetisedTotal { get; private set; }
    /// <summary>Pickups chasing the ball right now.</summary>
    public int Chasing { get; private set; }
    public int Live => _placed.Length - CollectedOrbs - CollectedCash;
    public byte StateOf(int index) => _state[index];

    public override void _Ready()
    {
        _orbs = MakeMesh(PlaceholderPalette.Sphere, OrbCount);
        _cash = MakeMesh(PlaceholderPalette.Sphere, CashCount);
        AddChild(new MultiMeshInstance3D { Name = "Orbs", Multimesh = _orbs, MaterialOverride = PlaceholderPalette.Orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off });
        AddChild(new MultiMeshInstance3D { Name = "Cash", Multimesh = _cash, MaterialOverride = PlaceholderPalette.Currency, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off });
        for (int i = 0; i < _placed.Length; i++) Write(i, 0f, 0f);
    }

    private static MultiMesh MakeMesh(Mesh mesh, int count) => new()
    {
        Mesh = mesh,
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        InstanceCount = count,
        VisibleInstanceCount = count,
    };

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _clock += dt;
        var player = _player();
        if (player is null) return;
        Vector3 ball = player.GlobalPosition;
        float chase = Mathf.Max(_t.MagnetSpeed, player.Velocity.Length() + _t.MagnetClosingMargin);
        float magnet2 = _t.MagnetRadius * _t.MagnetRadius, animate2 = AnimateRange * AnimateRange;
        int chasing = 0;

        for (int i = 0; i < _placed.Length; i++)
        {
            switch (_state[i])
            {
                case Gone:
                    continue;
                case Idle:
                {
                    float d2 = (_pos[i] - ball).LengthSquared();
                    if (d2 <= magnet2)
                    {
                        _state[i] = Magnetised;
                        MagnetisedTotal++;
                        goto case Magnetised;
                    }
                    bool near = d2 <= animate2;
                    if (near || _animated[i]) { Write(i, Mathf.Sin(_clock * 2.2f + _phase[i]) * 0.25f, _clock * 1.6f + _phase[i]); _animated[i] = near; }
                    continue;
                }
                case Magnetised:
                {
                    Vector3 to = ball - _pos[i];
                    float dist = to.Length();
                    float step = chase * dt;
                    if (dist <= CollectRadius || step >= dist) { Collect(i); continue; }
                    _pos[i] += to / dist * step;
                    Write(i, 0f, _clock * 6f);
                    chasing++;
                    continue;
                }
            }
        }
        Chasing = chasing;
    }

    private void Collect(int i)
    {
        _state[i] = Gone;
        var kind = _placed[i].Kind;
        if (kind == FieldPickupKind.Orb) CollectedOrbs++; else CollectedCash++;
        Mesh(kind).SetInstanceTransform(_slot[i], new Transform3D(Basis.Identity.Scaled(Vector3.Zero), _pos[i]));
        _vfx?.Play(WorldVfxKind.Pickup, _pos[i]);
        Collected?.Invoke(kind, 1);
    }

    private MultiMesh Mesh(FieldPickupKind kind) => kind == FieldPickupKind.Orb ? _orbs : _cash;

    private void Write(int i, float bob, float spin)
    {
        var kind = _placed[i].Kind;
        float size = kind == FieldPickupKind.Orb ? OrbSize : CashSize;
        var basis = new Basis(Vector3.Up, spin).Scaled(Vector3.One * size);
        Mesh(kind).SetInstanceTransform(_slot[i], new Transform3D(basis, _pos[i] + Vector3.Up * bob));
    }
}
