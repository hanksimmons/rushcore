using Godot;
using Rushcore.Vfx;

namespace Rushcore.Enemies;

/// <summary>The four enemy roles (02 §7). Behaviour arrives with the impact model; these are silhouettes.</summary>
public enum EnemyKind
{
    /// <summary>Stationary, low resistance: ram it (02 §7).</summary>
    Pylon,
    /// <summary>Stationary, high resistance: not at this speed.</summary>
    Bulwark,
    /// <summary>Slowly crosses likely travel lines; the long axis says which way.</summary>
    Strider,
    /// <summary>Simple slow readable projectiles; the barrel says where they will go.</summary>
    Shooter,
}

/// <summary>
/// Placeholder enemy silhouettes (06 §8, 02 §7). Presentation only: no collider, no behaviour, no placement
/// in a generated stage — they exist on the <c>World › Enemy Showcase</c> lab row and nowhere else (P-006).
/// The main track adds the impact model, health, defeat and Flow on top of these bodies.
///
/// <para>Every body draws with a shared material from <see cref="PlaceholderPalette"/>, so a row of five
/// enemies creates no resources at all. What varies is the silhouette, which is what has to be recognised at
/// the cap (06 §2 puts hostile silhouette third, above pickup lines and above cosmetic detail).</para>
/// </summary>
public partial class EnemyVisual : Node3D
{
    /// <summary>How long a crushed body stays hidden before the lab row can be used again.</summary>
    private const float CrushHiddenSeconds = 1.5f;

    private readonly WorldVfx? _vfx;
    private Node3D _body = null!;
    private Node3D? _halo;
    private float _hidden = -1f;
    private float _clock;
    private float _bobBase;

    public EnemyKind Kind { get; }
    public bool IsElite { get; private set; }
    /// <summary>True while a crush is playing and the body is out of sight.</summary>
    public bool BodyHidden => _hidden >= 0f;
    /// <summary>The projectile the Shooter would fire. Built, never fired: motion is the main track's (02 §7).</summary>
    public Node3D? Projectile { get; private set; }

    private EnemyVisual(EnemyKind kind, WorldVfx? vfx)
    {
        Kind = kind;
        _vfx = vfx;
        Name = kind.ToString();
    }

    /// <summary>
    /// Builds one enemy of the given role, standing on its own origin so the caller only has to place it on
    /// the ground and aim it. Sizes are in metres against the 0.66 m ball (D-091).
    /// </summary>
    public static EnemyVisual Create(EnemyKind kind, WorldVfx? vfx = null)
    {
        var e = new EnemyVisual(kind, vfx);
        e._body = new Node3D { Name = "Body" };
        e.AddChild(e._body);
        switch (kind)
        {
            case EnemyKind.Pylon: BuildPylon(e._body); break;
            case EnemyKind.Bulwark: BuildBulwark(e._body); break;
            case EnemyKind.Strider: BuildStrider(e._body); break;
            default: BuildShooter(e); break;
        }
        return e;
    }

    /// <summary>
    /// The elite treatment (02 §7 "one constrained modifier", 06 §8): the same silhouette at 1.3×, a rotating
    /// halo and a crown, both in the one elite colour. One treatment serves all four roles, so an elite is
    /// recognised before the role is.
    /// </summary>
    public void SetElite(bool elite)
    {
        if (IsElite == elite) return;
        IsElite = elite;
        _body.Scale = Vector3.One * (elite ? 1.3f : 1f);
        if (!elite)
        {
            _halo?.QueueFree();
            _halo = null;
            return;
        }

        float top = Height * 1.3f;
        _halo = new Node3D { Name = "Elite", Position = Vector3.Up * (top + 0.9f) };
        AddChild(_halo);
        _halo.AddChild(Piece(PlaceholderPalette.Halo, PlaceholderPalette.Elite, Vector3.Zero, new Vector3(3.0f, 0.5f, 3.0f)));
        _halo.AddChild(Piece(PlaceholderPalette.Prism, PlaceholderPalette.Elite, Vector3.Up * 0.7f, new Vector3(0.8f, 1.1f, 0.8f)));
    }

    /// <summary>Nominal standing height, before the elite scale: what the caller hangs signs and haloes off.</summary>
    public float Height => Kind switch
    {
        EnemyKind.Pylon => 2.6f,
        EnemyKind.Bulwark => 1.8f,
        EnemyKind.Strider => 2.2f,
        _ => 2.4f,
    };

    // ---------------- one-shots (06 §10) ----------------

    /// <summary>The ball went through it: shards, a ring, and the body gone for a moment so the row can be re-used.</summary>
    public void PlayCrush()
    {
        _vfx?.Play(WorldVfxKind.Crush, GlobalPosition + Vector3.Up * Height * 0.5f);
        _body.Visible = false;
        _hidden = CrushHiddenSeconds;
    }

    /// <summary>The ball hit it too slowly: a dull puff at the contact height and nothing else.</summary>
    public void PlayFail() => _vfx?.Play(WorldVfxKind.FailedImpact, GlobalPosition + Vector3.Up * Height * 0.45f);

    /// <summary>This enemy hurt the player: the world half of the cue. The red pulse is on the ball (PlayerVfx).</summary>
    public void PlayDamage() => _vfx?.Play(WorldVfxKind.FailedImpact, GlobalPosition + Vector3.Up * Height * 0.6f);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _clock += dt;

        if (_hidden >= 0f)
        {
            _hidden -= dt;
            if (_hidden < 0f) _body.Visible = true;
        }

        // A code-driven bob, not an animation player (KISS): the Strider's crossing has to read as motion
        // even while it is parked on a lab row.
        if (Kind == EnemyKind.Strider)
            _body.Position = _body.Position with { Y = _bobBase + Mathf.Sin(_clock * 1.6f) * 0.18f };

        if (_halo is { } halo)
        {
            halo.Rotate(Vector3.Up, dt * 1.4f);
            halo.Position = halo.Position with { Y = Height * 1.3f + 0.9f + Mathf.Sin(_clock * 2.2f) * 0.12f };
        }
    }

    // ---------------- silhouettes ----------------

    /// <summary>
    /// Pylon: a thin upright prism with a lit cap, 0.5 m across and 2.6 m tall. Nothing about it is heavy,
    /// which is the whole message — 02 §7 wants "ram it" read before the player has to think.
    /// </summary>
    private static void BuildPylon(Node3D body)
    {
        body.AddChild(Piece(PlaceholderPalette.Prism, PlaceholderPalette.PylonBody, Vector3.Up * 1.15f, new Vector3(0.5f, 2.3f, 0.5f)));
        body.AddChild(Piece(PlaceholderPalette.Sphere, PlaceholderPalette.PylonCap, Vector3.Up * 2.45f, Vector3.One * 0.42f));
    }

    /// <summary>
    /// Bulwark: 3.2 m wide, 1.8 m tall, 1.6 m deep, leaning 8° into the oncoming line with one hazard
    /// chevron band across its face. Wide and low and dark: the silhouette of something that does not move.
    /// </summary>
    private static void BuildBulwark(Node3D body)
    {
        var block = new Node3D { Name = "Block", Rotation = new Vector3(Mathf.DegToRad(-8f), 0f, 0f) };
        body.AddChild(block);
        block.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.Chassis, Vector3.Up * 0.9f, new Vector3(3.2f, 1.8f, 1.6f)));
        // The band sits proud of the face so it survives being seen edge-on at speed.
        block.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.Hazard, new Vector3(0f, 1.25f, -0.83f), new Vector3(3.24f, 0.34f, 0.08f)));
        // Feet: the block is planted, not resting.
        block.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.Chassis, new Vector3(-1.3f, 0.1f, 0f), new Vector3(0.5f, 0.2f, 2.1f)));
        block.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.Chassis, new Vector3(1.3f, 0.1f, 0f), new Vector3(0.5f, 0.2f, 2.1f)));
    }

    /// <summary>
    /// Strider: a 2.4 m body lying across the travel axis on two blade legs, with a small head at one end.
    /// The long axis is the message (02 §7 "shape/motion makes lateral crossing obvious"), so the body is
    /// four times as long as it is deep and rides high enough to see under.
    /// </summary>
    private static void BuildStrider(Node3D body)
    {
        body.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.StriderBody, Vector3.Up * 1.6f, new Vector3(2.4f, 0.5f, 0.6f)));
        body.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.StriderBody, new Vector3(1.2f, 1.6f, 0f), new Vector3(0.5f, 0.7f, 0.7f)));
        body.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.Chassis, new Vector3(-0.8f, 0.8f, 0f), new Vector3(0.16f, 1.6f, 0.5f)));
        body.AddChild(Piece(PlaceholderPalette.Box, PlaceholderPalette.Chassis, new Vector3(0.8f, 0.8f, 0f), new Vector3(0.16f, 1.6f, 0.5f)));
    }

    /// <summary>
    /// Shooter: a 1.2 m core on a short pillar, a barrel cone pointing along local −Z and an orientation ring
    /// around it in the aim colour, so the firing line is readable from the side as well as head on. The
    /// projectile is built here and parked beside the muzzle; nothing fires it (02 §7's motion is Phase 4).
    /// </summary>
    private static void BuildShooter(EnemyVisual e)
    {
        Node3D body = e._body;
        body.AddChild(Piece(PlaceholderPalette.Cylinder, PlaceholderPalette.Chassis, Vector3.Up * 0.55f, new Vector3(0.9f, 1.1f, 0.9f)));
        body.AddChild(Piece(PlaceholderPalette.Sphere, PlaceholderPalette.ShooterCore, Vector3.Up * 1.7f, Vector3.One * 1.2f));
        // Cone's nose is +Y in the mesh: lay it down so the muzzle points along −Z, the enemy's facing.
        var barrel = Piece(PlaceholderPalette.Cone, PlaceholderPalette.Chassis, new Vector3(0f, 1.7f, -0.95f), new Vector3(0.55f, 1.5f, 0.55f));
        barrel.RotateX(Mathf.DegToRad(-90f));
        body.AddChild(barrel);
        var ring = Piece(PlaceholderPalette.Halo, PlaceholderPalette.ShooterAim, new Vector3(0f, 1.7f, -0.45f), new Vector3(1.5f, 0.35f, 1.5f));
        ring.RotateX(Mathf.DegToRad(90f));
        body.AddChild(ring);

        var shot = new Node3D { Name = "Projectile", Position = new Vector3(0f, 1.7f, -2.4f) };
        shot.AddChild(Piece(PlaceholderPalette.Sphere, PlaceholderPalette.ShooterAim, Vector3.Zero, Vector3.One * 0.8f));
        body.AddChild(shot);
        e.Projectile = shot;
    }

    private static MeshInstance3D Piece(Mesh mesh, Material material, Vector3 position, Vector3 scale) => new()
    {
        Mesh = mesh,
        MaterialOverride = material,
        Position = position,
        Scale = scale,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
    };
}
