using Godot;
using Rushcore.Tuning;

namespace Rushcore.Player;

/// <summary>
/// Presentation-only ball (06 §5, 03 §13). Roll is derived from travelled distance
/// rather than the rigid body's angular simulation, and every deformation is visual:
/// the sphere collider is never touched. Charge commitment and the perfect-apex slam
/// get unmistakable player-local cues.
/// </summary>
public partial class PlayerVisual : Node3D
{
    // ---- palette: the ball must never read as terrain (06 §2/§3). Terrain is
    // desaturated olive/grey, so the ball is hot orange with hard dark facets and
    // a few cream accents so rotation stays legible at 60 m/s.
    private static readonly Color FacePrimary = new(1.00f, 0.42f, 0.10f);
    private static readonly Color FaceDark = new(0.08f, 0.09f, 0.12f);
    private static readonly Color FaceAccent = new(1.00f, 0.86f, 0.36f);

    private static readonly Color ChargeColor = new(1.00f, 0.92f, 0.60f);
    private static readonly Color SlamColor = new(0.42f, 0.48f, 1.00f);
    private static readonly Color ApexColor = new(1.00f, 0.96f, 0.80f);

    // Which of the 20 icosahedron base faces gets which palette entry. Subdivided
    // triangles inherit their parent face, so the ball reads as 20 big flat patches
    // instead of per-triangle noise.
    private static readonly int[] FacePattern =
        { 0, 1, 0, 2, 0, 2, 0, 1, 0, 1, 0, 0, 2, 0, 1, 0, 2, 0, 1, 0 };

    /// <summary>Built once and shared: the mesh is unit radius and scaled per frame.</summary>
    private static ArrayMesh? _sharedMesh;

    private const float SpringStiffness = 210f;
    private const float SpringDamping = 20f;
    private const float FlashDecayPerSecond = 4.0f;

    private readonly GameplayTuning _t;
    private readonly PlayerPhysics _player;

    private MeshInstance3D _mesh = null!;
    private MeshInstance3D _shell = null!;
    private StandardMaterial3D _material = null!;
    private StandardMaterial3D _shellMaterial = null!;

    private Basis _roll = Basis.Identity;
    private Vector3 _travelDir = Vector3.Forward;

    // Deformation state. _vert > 0 is stretched tall, < 0 is squashed flat.
    private float _vert;
    private float _vertVel;
    private float _boostBlend;
    private float _flash;
    private float _time;

    private Color _bandColor = FacePrimary;
    private float _bandEnergy = 0.1f;

    public PlayerVisual(GameplayTuning tuning, PlayerPhysics player)
    {
        _t = tuning;
        _player = player;
    }

    public override void _Ready()
    {
        Name = "PlayerVisual";
        // Local transform is authored per rendered frame; the parent body supplies
        // the interpolated world transform.
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;

        Mesh ball = _sharedMesh ??= BuildIcosphere(2);

        _material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 0.55f,
            Metallic = 0f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            EmissionEnabled = true,
            Emission = FacePrimary,
            EmissionEnergyMultiplier = 0.1f,
            // Winding is derived geometrically below, but leaving culling off removes
            // any chance of an inside-out ball for 320 triangles of cost.
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _mesh = new MeshInstance3D { Name = "Ball", Mesh = ball, MaterialOverride = _material };
        AddChild(_mesh);

        _shellMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(ChargeColor, 0f),
        };
        _shell = new MeshInstance3D
        {
            Name = "ChargeShell",
            Mesh = ball,
            MaterialOverride = _shellMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(_shell);

        _player.Jumped += OnJumped;
        _player.Slammed += OnSlammed;
        _player.Landed += OnLanded;
        _player.SpeedBandChanged += OnSpeedBandChanged;
    }

    public override void _ExitTree()
    {
        _player.Jumped -= OnJumped;
        _player.Slammed -= OnSlammed;
        _player.Landed -= OnLanded;
        _player.SpeedBandChanged -= OnSpeedBandChanged;
    }

    // ---------------- events (raised inside the physics step: floats only) ----------------

    private void OnJumped(float charge01)
        => _vertVel += (3.5f + 10f * charge01) * Str(_t.Vfx.JumpReleaseStrength) * Str(_t.Vfx.SquashStretchStrength);

    private void OnSlammed(bool perfect)
    {
        _vertVel += (perfect ? 7f : 2.5f) * Str(_t.Vfx.SlamEffectStrength) * Str(_t.Vfx.SquashStretchStrength);
        if (perfect) _flash = 1f;
    }

    private void OnLanded(float impactSpeed, bool wasSlam, bool wasPerfectApexSlam)
    {
        float impact = Mathf.Min(impactSpeed / 18f, 2f);
        float mult = wasPerfectApexSlam ? 1.9f : wasSlam ? 1.4f : 1f;
        _vertVel -= (2f + 7f * impact) * mult * Str(_t.Vfx.ImpactEffectStrength) * Str(_t.Vfx.SquashStretchStrength);
        if (wasPerfectApexSlam) _flash = 1f;
    }

    private void OnSpeedBandChanged(SpeedBand band)
    {
        // A small pop on every band change so the speed state is felt, not just seen.
        if (band != SpeedBand.Roll) _vertVel += 1.2f * Str(_t.Vfx.SquashStretchStrength);
    }

    private static float Str(float v) => Mathf.Max(0f, v);

    // ---------------- per-frame presentation ----------------

    public override void _Process(double delta)
    {
        float dt = Mathf.Min((float)delta, 0.1f);
        _time += dt;

        var vfx = _t.Vfx;
        float squash = Str(vfx.SquashStretchStrength);
        float r = Mathf.Max(0.05f, _t.Movement.BallRadius);
        float cap = Mathf.Max(1f, _t.Movement.HardMaxLocomotionSpeed);
        float speed01 = Mathf.Clamp(_player.LocomotionSpeed / cap, 0f, 1f);

        // ---- roll about the axis perpendicular to travel, at the rate a real ball would ----
        Vector3 v = _player.Velocity;
        Vector3 flat = new(v.X, 0f, v.Z);
        float flatSpeed = flat.Length();
        if (flatSpeed > 0.05f)
        {
            _travelDir = flat / flatSpeed;
            Vector3 axis = Vector3.Up.Cross(_travelDir);
            if (axis.LengthSquared() > 1e-6f)
                _roll = new Basis(axis.Normalized(), flatSpeed / r * dt) * _roll;
        }

        bool charging = _player.IsCharging;
        float charge01 = _player.Charge01;
        bool slam = _player.SlamActive;
        bool perfectSlam = slam && _player.LastSlamWasPerfect;
        float chargeStrength = Str(vfx.ChargeEffectStrength);
        float slamStrength = Str(vfx.SlamEffectStrength);

        // ---- vertical spring: charge compresses, slam stretches, impulses ring it ----
        float target = 0f;
        if (charging) target = -(0.22f + 0.34f * charge01) * chargeStrength;
        else if (slam) target = (perfectSlam ? 0.70f : 0.42f) * slamStrength;
        target = Mathf.Clamp(target * squash, -0.65f, 1.4f);

        _vertVel += (SpringStiffness * (target - _vert) - SpringDamping * _vertVel) * dt;
        _vertVel = Mathf.Clamp(_vertVel, -40f, 40f);
        _vert = Mathf.Clamp(_vert + _vertVel * dt, -0.70f, 1.60f);

        // ---- horizontal stretch along travel: boost and raw speed ----
        _boostBlend = Mathf.Lerp(_boostBlend, _player.BoostActive ? 1f : 0f, 1f - Mathf.Exp(-9f * dt));
        float along = Mathf.Clamp((0.30f * _boostBlend + 0.10f * speed01) * squash, 0f, 1.2f);

        _flash = Mathf.Max(0f, _flash - dt * FlashDecayPerSecond);

        Basis deform = BuildDeformBasis(along);
        _mesh.Basis = deform * _roll.Orthonormalized().Scaled(new Vector3(r, r, r));

        UpdateEmission(dt, charging, charge01, slam, perfectSlam, chargeStrength, slamStrength);
        UpdateShell(deform, r, charging, charge01, chargeStrength);
    }

    /// <summary>
    /// World-axis squash/stretch applied outside the roll so a "flat" ball stays flat
    /// against the world regardless of how far it has rolled. Scales stay positive.
    /// </summary>
    private Basis BuildDeformBasis(float along)
    {
        float sy = Mathf.Max(0.15f, 1f + _vert);
        float sh = 1f / Mathf.Sqrt(sy);                 // rough volume preservation
        Basis vertical = Basis.Identity.Scaled(new Vector3(sh, sy, sh));

        if (along < 0.005f) return vertical;

        float sz = Mathf.Max(0.15f, 1f + along);
        float st = 1f / Mathf.Sqrt(sz);
        Vector3 fwd = _travelDir;
        Vector3 right = Vector3.Up.Cross(fwd);
        if (right.LengthSquared() < 1e-6f) return vertical;
        right = right.Normalized();
        // Columns x,y,z of an orthonormal frame whose z axis is the travel direction.
        Basis frame = new(right, Vector3.Up, fwd);
        Basis travel = frame * Basis.Identity.Scaled(new Vector3(st, st, sz)) * frame.Transposed();
        return vertical * travel;
    }

    private void UpdateEmission(float dt, bool charging, float charge01, bool slam, bool perfectSlam,
                                float chargeStrength, float slamStrength)
    {
        (Color bandColor, float bandEnergy) = BandLook(_player.Band);
        float k = 1f - Mathf.Exp(-9f * dt);
        _bandColor = _bandColor.Lerp(bandColor, k);
        _bandEnergy = Mathf.Lerp(_bandEnergy, bandEnergy, k);

        Color col = _bandColor;
        float energy = _bandEnergy;

        if (charging)
        {
            // Pulse rate climbs with charge so "nearly full" is audible-in-the-eyes.
            float pulse = 0.55f + 0.45f * Mathf.Sin(_time * (10f + 26f * charge01));
            col = col.Lerp(ChargeColor, 0.35f + 0.65f * charge01);
            energy += (0.5f + 2.6f * charge01) * pulse * chargeStrength;
        }

        if (slam)
        {
            col = col.Lerp(perfectSlam ? ApexColor : SlamColor, 0.85f);
            energy += (perfectSlam ? 2.6f : 0.9f) * slamStrength;
        }

        if (_flash > 0f)
        {
            col = col.Lerp(Colors.White, _flash);
            energy += 9f * _flash * _flash;    // a hard white-out no normal slam produces
        }

        _material.Emission = col;
        _material.EmissionEnergyMultiplier = energy;
    }

    private void UpdateShell(Basis deform, float radius, bool charging, float charge01, float chargeStrength)
    {
        float alpha = 0f;
        Color color = ChargeColor;
        float grow = 0f;

        if (charging)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(_time * (10f + 26f * charge01));
            alpha = (0.16f + 0.46f * charge01) * chargeStrength * pulse;
            grow = 0.12f * charge01;
        }
        if (_player.Band == SpeedBand.Overdrive)
        {
            alpha = Mathf.Max(alpha, 0.16f);
            color = BandOverdrive;
        }
        if (_flash > 0f)
        {
            alpha = Mathf.Max(alpha, _flash);
            grow = Mathf.Max(grow, 0.95f * _flash);      // expanding halo burst
            color = ApexColor;
        }

        alpha = Mathf.Clamp(alpha, 0f, 1f);
        _shell.Visible = alpha > 0.01f;
        if (!_shell.Visible) return;

        _shellMaterial.AlbedoColor = new Color(color, alpha);
        float s = radius * (1.05f + grow);
        _shell.Basis = deform * Basis.Identity.Scaled(new Vector3(s, s, s));
    }

    private static readonly Color BandRoll = new(1.00f, 0.45f, 0.12f);
    private static readonly Color BandRush = new(1.00f, 0.58f, 0.16f);
    private static readonly Color BandCrush = new(1.00f, 0.26f, 0.10f);
    private static readonly Color BandOverdrive = new(0.55f, 0.95f, 1.00f);

    /// <summary>Speed state without a speedometer: colour AND intensity both move (06 §12).</summary>
    private static (Color, float) BandLook(SpeedBand band) => band switch
    {
        SpeedBand.Rush => (BandRush, 0.45f),
        SpeedBand.Crush => (BandCrush, 1.05f),
        SpeedBand.Overdrive => (BandOverdrive, 1.90f),
        _ => (BandRoll, 0.10f),
    };

    // ---------------- procedural icosphere ----------------

    /// <summary>
    /// Unit-radius icosphere with flat per-face normals and per-face vertex colours.
    /// Non-indexed so every triangle owns its normal, which is what gives the faceted
    /// N64 silhouette (06 §4). Built once and shared.
    /// </summary>
    private static ArrayMesh BuildIcosphere(int subdivisions)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] p =
        {
            new(-1f, t, 0f), new(1f, t, 0f), new(-1f, -t, 0f), new(1f, -t, 0f),
            new(0f, -1f, t), new(0f, 1f, t), new(0f, -1f, -t), new(0f, 1f, -t),
            new(t, 0f, -1f), new(t, 0f, 1f), new(-t, 0f, -1f), new(-t, 0f, 1f),
        };
        for (int i = 0; i < p.Length; i++) p[i] = p[i].Normalized();

        int[] baseTris =
        {
            0, 11, 5,  0, 5, 1,   0, 1, 7,   0, 7, 10,  0, 10, 11,
            1, 5, 9,   5, 11, 4,  11, 10, 2, 10, 7, 6,  7, 1, 8,
            3, 9, 4,   3, 4, 2,   3, 2, 6,   3, 6, 8,   3, 8, 9,
            4, 9, 5,   2, 4, 11,  6, 2, 10,  8, 6, 7,   9, 8, 1,
        };

        int perFace = 1;
        for (int i = 0; i < subdivisions; i++) perFace *= 4;
        int triCount = 20 * perFace;
        var verts = new Vector3[triCount * 3];
        var norms = new Vector3[triCount * 3];
        var cols = new Color[triCount * 3];

        int w = 0;
        for (int f = 0; f < 20; f++)
        {
            Color baseColor = FacePattern[f] switch
            {
                1 => FaceDark,
                2 => FaceAccent,
                _ => FacePrimary,
            };
            int before = w;
            w = Subdivide(p[baseTris[f * 3]], p[baseTris[f * 3 + 1]], p[baseTris[f * 3 + 2]],
                          subdivisions, verts, norms, cols, baseColor, w);
            // Subtle per-sub-triangle value break so each patch still shows facets.
            for (int i = before; i < w; i += 3)
            {
                float shade = ((i / 3) % 3 == 0) ? 1f : 0.90f;
                Color c = new(cols[i].R * shade, cols[i].G * shade, cols[i].B * shade);
                cols[i] = cols[i + 1] = cols[i + 2] = c;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = norms;
        arrays[(int)Mesh.ArrayType.Color] = cols;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static int Subdivide(Vector3 a, Vector3 b, Vector3 c, int depth,
                                 Vector3[] verts, Vector3[] norms, Color[] cols, Color color, int w)
    {
        if (depth <= 0)
        {
            Vector3 outward = (a + b + c).Normalized();
            // Emit consistently wound around the outward normal.
            if ((b - a).Cross(c - a).Dot(outward) < 0f) (b, c) = (c, b);
            verts[w] = a; norms[w] = outward; cols[w] = color; w++;
            verts[w] = b; norms[w] = outward; cols[w] = color; w++;
            verts[w] = c; norms[w] = outward; cols[w] = color; w++;
            return w;
        }

        Vector3 ab = ((a + b) * 0.5f).Normalized();
        Vector3 bc = ((b + c) * 0.5f).Normalized();
        Vector3 ca = ((c + a) * 0.5f).Normalized();
        w = Subdivide(a, ab, ca, depth - 1, verts, norms, cols, color, w);
        w = Subdivide(ab, b, bc, depth - 1, verts, norms, cols, color, w);
        w = Subdivide(ca, bc, c, depth - 1, verts, norms, cols, color, w);
        w = Subdivide(ab, bc, ca, depth - 1, verts, norms, cols, color, w);
        return w;
    }
}
