using Godot;
using Rushcore.Core;
using Rushcore.Player;
using Rushcore.Tuning;

namespace Rushcore.Camera;

/// <summary>
/// Fixed-orientation follow rig (03 §14, 06 §11). It reads the player's interpolated
/// transform every rendered frame and applies its own damping, so it never inherits
/// raw physics-step jitter.
/// </summary>
public partial class CameraRig : Node3D, ICameraBasis
{
    private readonly GameplayTuning _t;
    private readonly PlayerPhysics _player;
    private Camera3D _camera = null!;

    private Vector3 _focus;
    private float _zoom = 1f;
    private float _shake;

    // Occlusion probe: a sphere cast from the focus toward the camera, run in the
    // physics step (space-state access is guaranteed there in every threading mode).
    private readonly SphereShape3D _probeShape = new();
    private readonly PhysicsShapeQueryParameters3D _probe = new();
    private Vector3 _probeOffset;          // full-distance camera offset requested by _Process
    private float _probeFraction = 1f;     // latest safe fraction from the physics step
    private float _occlusion = 1f;         // smoothed fraction actually applied
    private readonly RandomNumberGenerator _rng = new();

    public CameraRig(GameplayTuning tuning, PlayerPhysics player)
    {
        _t = tuning;
        _player = player;
    }

    public Vector3 FlatForward { get; private set; } = Vector3.Forward;
    public Vector3 FlatRight { get; private set; } = Vector3.Right;
    public Camera3D Camera => _camera;
    public float Zoom => _zoom;
    /// <summary>1 = unobstructed; lower = camera pulled in toward the focus by that fraction.</summary>
    public float OcclusionFraction => _occlusion;
    public float CurrentDistance { get; private set; }
    public float CurrentLookAhead { get; private set; }

    public override void _Ready()
    {
        Name = "CameraRig";
        // This rig is repositioned every rendered frame, so engine physics
        // interpolation must not also smear it.
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;

        _camera = new Camera3D { Name = "Camera", Current = true, Near = 0.25f, Far = 4000f };
        AddChild(_camera);

        _rng.Randomize();
        _probe.Shape = _probeShape;
        _probe.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        _probe.CollideWithAreas = false;
        _probe.CollideWithBodies = true;
        UpdateOrientation();
        _focus = _player.GlobalPosition;
        ApplyTransform(1f, 0f);
        _player.Landed += OnLanded;
        _player.Slammed += OnSlammed;
        _player.Recovered += SnapToPlayer;
    }

    public override void _ExitTree()
    {
        _player.Landed -= OnLanded;
        _player.Slammed -= OnSlammed;
        _player.Recovered -= SnapToPlayer;
    }

    public void AddShake(float amount) => _shake = Mathf.Min(_shake + amount, 4f);

    public override void _PhysicsProcess(double delta)
    {
        var c = _t.Camera;
        if (!c.OcclusionProbe || _probeOffset.LengthSquared() < 1e-4f)
        {
            _probeFraction = 1f;
            return;
        }
        _probeShape.Radius = Mathf.Max(0.05f, c.OcclusionMargin);
        _probe.Transform = new Transform3D(Basis.Identity, _focus);
        _probe.Motion = _probeOffset;
        float[] hit = GetWorld3D().DirectSpaceState.CastMotion(_probe);
        _probeFraction = hit.Length > 0 ? Mathf.Clamp(hit[0], 0f, 1f) : 1f;
    }

    public void SnapToPlayer()
    {
        _focus = _player.GlobalPosition;
        _shake = 0f;
        _occlusion = 1f;
        _probeFraction = 1f;
        ApplyTransform(1f, 0f);
        ResetPhysicsInterpolation();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (Input.IsActionJustPressed(InputBootstrap.ZoomIn)) SetZoom(_zoom - _t.Camera.ZoomStep);
        if (Input.IsActionJustPressed(InputBootstrap.ZoomOut)) SetZoom(_zoom + _t.Camera.ZoomStep);

        UpdateOrientation();

        Vector3 playerPos = _player.GetGlobalTransformInterpolated().Origin;
        Vector3 flatVel = new(_player.Velocity.X, 0f, _player.Velocity.Z);
        float cap = Mathf.Max(0.001f, _t.Movement.HardMaxLocomotionSpeed);
        float speed01 = Mathf.Clamp(flatVel.Length() / cap, 0f, 1f);

        var c = _t.Camera;
        Vector3 lookAhead = Vector3.Zero;
        if (flatVel.LengthSquared() > 1f)
            lookAhead = flatVel.Normalized() * Mathf.Lerp(c.LookAheadMin, c.LookAheadMax, speed01);
        CurrentLookAhead = lookAhead.Length();

        Vector3 target = playerPos + lookAhead + Vector3.Up * c.HeightOffset;

        // Exponential damping, vertical handled on its own slower constant so hills
        // and jump arcs do not throw the horizon around.
        float aH = 1f - Mathf.Exp(-Mathf.Max(0.01f, c.FollowDamping) * dt);
        float aV = 1f - Mathf.Exp(-Mathf.Max(0.01f, c.VerticalDamping) * dt);
        _focus.X = Mathf.Lerp(_focus.X, target.X, aH);
        _focus.Z = Mathf.Lerp(_focus.Z, target.Z, aH);
        _focus.Y = Mathf.Lerp(_focus.Y, target.Y, aV);

        if (_shake > 0f) _shake = Mathf.Max(0f, _shake - _shake * Mathf.Max(0.01f, c.ShakeDecay) * dt);

        // Pull in immediately when blocked; ease back out so a cleared line of sight
        // does not pop the camera.
        float occlusionTarget = c.OcclusionProbe ? _probeFraction : 1f;
        _occlusion = occlusionTarget < _occlusion
            ? occlusionTarget
            : Mathf.Lerp(_occlusion, occlusionTarget, 1f - Mathf.Exp(-Mathf.Max(0.01f, c.OcclusionRecoverSpeed) * dt));
        ApplyTransform(speed01, _shake);
    }

    private void SetZoom(float value) =>
        _zoom = Mathf.Clamp(value, _t.Camera.ZoomMin, _t.Camera.ZoomMax);

    private void UpdateOrientation()
    {
        var c = _t.Camera;
        Basis b = Basis.FromEuler(new Vector3(Mathf.DegToRad(c.PitchDegrees), Mathf.DegToRad(c.YawDegrees), 0f));
        Basis = b;

        Vector3 fwd = -b.Z;
        fwd.Y = 0f;
        if (fwd.LengthSquared() < 1e-6f) fwd = Vector3.Forward; else fwd = fwd.Normalized();
        Vector3 right = b.X;
        right.Y = 0f;
        right = right.LengthSquared() < 1e-6f ? Vector3.Right : right.Normalized();
        FlatForward = fwd;
        FlatRight = right;
    }

    private void ApplyTransform(float speed01, float shake)
    {
        var c = _t.Camera;
        GlobalPosition = _focus;

        float fullDist = (c.Distance + c.SpeedDistanceGain * speed01) * _zoom;
        _probeOffset = Basis.Z * fullDist;                       // world offset focus -> camera
        float dist = Mathf.Max(1.5f, fullDist * _occlusion);
        CurrentDistance = dist;
        Vector3 local = new(0f, 0f, dist);
        if (shake > 0f)
        {
            float s = shake * c.ShakeStrength;
            local += new Vector3(_rng.RandfRange(-s, s), _rng.RandfRange(-s, s), _rng.RandfRange(-s, s));
        }
        _camera.Position = local;
        _camera.Fov = Mathf.Lerp(c.FovMin, c.FovMax, speed01);
    }

    private void OnLanded(float impactSpeed, bool wasSlam, bool wasPerfect)
    {
        float baseShake = Mathf.Clamp(impactSpeed / 40f, 0f, 1f) * 0.35f;
        if (wasSlam) baseShake += 0.35f;
        if (wasPerfect) baseShake += 0.35f;
        AddShake(baseShake);
    }

    private void OnSlammed(bool perfect) => AddShake(perfect ? 0.30f : 0.15f);
}
