using System.Collections;
using Godot;
using Rushcore.Core;
using Rushcore.Player;
using Rushcore.Tuning;
using Rushcore.World;

namespace Rushcore.Testing;

/// <summary>
/// Headless acceptance harness for the objective half of Gate M0 (08 §3). It drives
/// the real controller with synthetic input on an isolated high-altitude rig so the
/// results do not depend on the calibration terrain. Subjective feel is NOT tested
/// here; that remains a human playtest.
/// Run with: godot --headless --path . -- --rushcore-selftest
/// </summary>
public partial class MovementToySelfTest : Node
{
    private const float PlatformY = 302f;
    private const float RampAngleDegrees = 20f;
    private static readonly Vector3 PlatformCenter = new(0f, PlatformY, 0f);
    private static readonly Vector3 RampOrigin = new(0f, PlatformY - 2f, 800f);

    /// <summary>Injected input is observed by the player on the next physics frame and
    /// acted on during that frame's integration, so assertions wait two frames.</summary>
    private const int EdgeFrames = 2;
    private const float WallDistance = 110f;
    private static readonly Vector3 WallLaneCentre = new(0f, PlatformY, -800f);
    private static readonly Vector3 LandingLaneCentre = new(0f, PlatformY, 1600f);
    private const float LandingSlopeDegrees = 25f;
    private Vector3 _landingStart;
    private Vector3 _wallCentre;
    private Vector3 _wallLaneStart;

    private readonly IDebugActions _debug;
    private PlayerPhysics _player = null!;
    private IEnumerator _script = null!;

    private readonly List<string> _failures = new();
    private int _checks;
    private bool _done;

    private int _jumpedCount, _chargeCanceledCount, _slammedCount, _recoveredCount, _pickupCount;
    private bool _releaseJumpFromProcess;
    /// <summary>When set, WASD is re-derived every tick so a rotating camera cannot bend the drive line.</summary>
    private Vector3? _worldDrive;
    private bool _visCheck;
    private int _visFrames, _visBlocked;
    private int _groundViolations;
    private Vector3 _laneFwd = Vector3.Forward;
    private float _lastJumpCharge;
    private bool _lastSlamPerfect;

    public MovementToySelfTest(IDebugActions debug) => _debug = debug;

    private static Vector3 RampSurfacePoint(float alongSlope)
    {
        float a = Mathf.DegToRad(RampAngleDegrees);
        // Local (alongSlope, +2, 0) on the box top, rotated about Z, then offset.
        return new Vector3(
            alongSlope * Mathf.Cos(a) - 2f * Mathf.Sin(a),
            alongSlope * Mathf.Sin(a) + 2f * Mathf.Cos(a),
            0f) + RampOrigin;
    }

    public override void _Ready()
    {
        Name = "MovementToySelfTest";
        _player = _debug.Player;
        BuildRig();

        _player.Jumped += c => { _jumpedCount++; _lastJumpCharge = c; };
        _player.JumpChargeCanceled += () => _chargeCanceledCount++;
        _player.Slammed += p => { _slammedCount++; _lastSlamPerfect = p; };
        _player.Recovered += () => _recoveredCount++;
        _debug.World.BoostPickupCollected += _ => _pickupCount++;

        _script = Run();
        GD.Print("[SELFTEST] starting");
    }

    public override void _Process(double delta)
    {
        // Real key-ups land between physics ticks; this mimics that path.
        if (_releaseJumpFromProcess)
        {
            _releaseJumpFromProcess = false;
            Input.ActionRelease(InputBootstrap.Jump);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done) return;

        if (!_player.Velocity.IsFinite())
        {
            Fail("velocity stays finite", $"velocity={_player.Velocity}");
            Finish();
            return;
        }

        if (_worldDrive is { } drive) PressWorldDirection(drive);
        SampleCameraInvariants();

        if (!_script.MoveNext()) Finish();
    }

    /// <summary>Every tick: the lens must be above the heightfield, and while a run is
    /// flagged for visibility the camera->ball line must be clear.</summary>
    private void SampleCameraInvariants()
    {
        if (_player.CameraBasis is not Rushcore.Camera.CameraRig rig) return;
        var world = _debug.World;
        Vector3 cam = rig.Camera.GlobalPosition;
        float half = MovementToyWorld.Extent * 0.5f - 8f;
        if (Mathf.Abs(cam.X) < half && Mathf.Abs(cam.Z) < half && cam.Y < world.Bounds.End.Y + 50f)
        {
            if (cam.Y < world.SampleHeight(cam.X, cam.Z) + _debug.Tuning.Camera.GroundClearance - 0.15f)
                _groundViolations++;
        }

        if (!_visCheck) return;
        var q = PhysicsRayQueryParameters3D.Create(cam, _player.GlobalPosition + Vector3.Up * 0.5f);
        q.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        q.CollideWithAreas = false;
        var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(q);
        _visFrames++;
        if (hit.Count > 0) _visBlocked++;
    }

    private void BuildRig()
    {
        var plate = new StaticBody3D { Name = "TestPlate", Position = PlatformCenter - Vector3.Up * 2f };
        plate.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(400f, 4f, 400f) } });
        plate.PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f };
        AddChild(plate);

        // Isolated CCD lane (08 §3): a runway aligned with the drive direction and a
        // 0.3 m plate across it. Kept away from the main plate so no other case can
        // strike the wall by accident.
        Vector3 fwd = _player.CameraBasis?.FlatForward ?? Vector3.Forward;
        _laneFwd = fwd;
        var laneBasis = Basis.LookingAt(fwd, Vector3.Up);          // lane -Z == drive direction
        var lane = new StaticBody3D
        {
            Name = "TestCcdLane",
            Transform = new Transform3D(laneBasis, WallLaneCentre - Vector3.Up * 2f),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        lane.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(80f, 4f, 340f) } });
        AddChild(lane);

        _wallCentre = WallLaneCentre + fwd * WallDistance + Vector3.Up * 6f;
        _wallLaneStart = WallLaneCentre - fwd * 140f + Vector3.Up * 3f;
        var wall = new StaticBody3D
        {
            Name = "TestThinWall",
            Transform = new Transform3D(laneBasis, _wallCentre),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(80f, 12f, 0.3f) } });
        AddChild(wall);

        // Landing lane: runway into a 25 deg downslope. Arriving at the cap, the ball
        // leaves the crest ballistically and lands on the slope with tangent speed above
        // the cap (1/cos 25 deg); the cap must bleed that, not clip it (D-069, 03 §5).
        var landRunway = new StaticBody3D
        {
            Name = "TestLandingRunway",
            Transform = new Transform3D(laneBasis, LandingLaneCentre - Vector3.Up * 2f),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        landRunway.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(80f, 4f, 300f) } });
        AddChild(landRunway);

        float sa = Mathf.DegToRad(LandingSlopeDegrees);
        Vector3 crest = LandingLaneCentre + fwd * 150f;
        _landingStart = LandingLaneCentre - fwd * 140f + Vector3.Up * 3f;
        // Slope box: tilt the lane basis nose-down about its local X, centre it half a
        // length down the incline, and sink it by half its thickness.
        Basis slopeBasis = laneBasis * new Basis(Vector3.Right, -sa);
        Vector3 slopeCentre = crest + fwd * (150f * Mathf.Cos(sa)) - Vector3.Up * (150f * Mathf.Sin(sa) + 2f * Mathf.Cos(sa));
        var landSlope = new StaticBody3D
        {
            Name = "TestLandingSlope",
            Transform = new Transform3D(slopeBasis, slopeCentre),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f },
        };
        landSlope.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(120f, 4f, 300f) } });
        AddChild(landSlope);

        var ramp = new StaticBody3D { Name = "TestRamp", Position = RampOrigin };
        ramp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(400f, 4f, 200f) } });
        ramp.PhysicsMaterialOverride = new PhysicsMaterial { Friction = PlayerPhysics.ArcadeSurfaceFriction, Bounce = 0f };
        ramp.RotateZ(Mathf.DegToRad(RampAngleDegrees));   // +X is uphill
        AddChild(ramp);
    }

    // ---------------- helpers ----------------
    private void Check(string name, bool ok, string detail = "")
    {
        _checks++;
        if (ok) GD.Print($"[SELFTEST] PASS  {name}");
        else Fail(name, detail);
    }

    private void Fail(string name, string detail)
    {
        _failures.Add(name + (string.IsNullOrEmpty(detail) ? "" : $"  ({detail})"));
        GD.Print($"[SELFTEST] FAIL  {name}  {detail}");
    }

    private void CheckNear(string name, float actual, float expected, float tolerance) =>
        Check(name, Mathf.Abs(actual - expected) <= tolerance,
            $"actual={actual:0.###} expected={expected:0.###} tol={tolerance:0.###}");

    private static IEnumerable Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
    private static IEnumerable Seconds(float s) => Frames(Mathf.Max(1, (int)(s * Engine.PhysicsTicksPerSecond)));
    private static IEnumerable Act() => Frames(EdgeFrames);

    private static readonly string[] AllActions =
    {
        InputBootstrap.MoveForward, InputBootstrap.MoveBack, InputBootstrap.MoveLeft,
        InputBootstrap.MoveRight, InputBootstrap.Jump, InputBootstrap.Boost, InputBootstrap.Carve
    };

    private void ReleaseAll()
    {
        _worldDrive = null;
        foreach (var a in AllActions) Input.ActionRelease(a);
    }

    /// <summary>Presses the WASD combination that steers toward a world-space direction.</summary>
    private void PressWorldDirection(Vector3 worldDir)
    {
        Vector3 d = new Vector3(worldDir.X, 0f, worldDir.Z).Normalized();
        float ix = FlatRight.Dot(d), iy = Forward.Dot(d);
        Input.ActionRelease(InputBootstrap.MoveLeft);
        Input.ActionRelease(InputBootstrap.MoveRight);
        Input.ActionRelease(InputBootstrap.MoveForward);
        Input.ActionRelease(InputBootstrap.MoveBack);
        if (ix >= 0f) Input.ActionPress(InputBootstrap.MoveRight, ix); else Input.ActionPress(InputBootstrap.MoveLeft, -ix);
        if (iy >= 0f) Input.ActionPress(InputBootstrap.MoveForward, iy); else Input.ActionPress(InputBootstrap.MoveBack, -iy);
    }

    private IEnumerable Settle(Vector3 at, float seconds = 1.2f)
    {
        ReleaseAll();
        _player.TeleportTo(at);
        foreach (var _ in Seconds(seconds)) yield return null;
    }

    private Vector3 Forward => _player.CameraBasis?.FlatForward ?? Vector3.Forward;
    private Vector3 FlatRight => _player.CameraBasis?.FlatRight ?? Vector3.Right;
    private Vector3 FlatVel => new(_player.Velocity.X, 0f, _player.Velocity.Z);

    private void Finish()
    {
        _done = true;
        ReleaseAll();
        GD.Print($"[SELFTEST] ---- {_checks - _failures.Count}/{_checks} checks passed ----");
        foreach (var f in _failures) GD.Print("[SELFTEST] FAILED: " + f);
        GD.Print(_failures.Count == 0 ? "[SELFTEST] RESULT: PASS" : $"[SELFTEST] RESULT: FAIL ({_failures.Count})");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    // ---------------- the script ----------------
    private IEnumerator Run()
    {
        var t = _debug.Tuning;
        var m = t.Movement;
        var js = t.JumpSlam;

        // ---- body configuration invariants ----
        Check("CCD enabled", _player.ContinuousCd);
        Check("player cannot sleep", !_player.CanSleep);
        Check("contact monitoring on", _player.ContactMonitor);
        Check("contact budget > 0", _player.MaxContactsReported >= 4, $"reported={_player.MaxContactsReported}");
        Check("CustomIntegrator off", !_player.CustomIntegrator);
        Check("standard gravity active", _player.GravityScale > 0f, $"scale={_player.GravityScale}");

        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Check("ground contact produces usable data",
            _player.IsRawGrounded && _player.GroundNormal.Dot(Vector3.Up) > 0.9f,
            $"grounded={_player.IsRawGrounded} n={_player.GroundNormal}");

        // ---- accelerates from rest ----
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(1.5f)) yield return null;
        Check("accelerates from rest on flat", _player.LocomotionSpeed > 12f, $"speed={_player.LocomotionSpeed:0.0}");

        // ---- hard cap, including under boost ----
        Input.ActionPress(InputBootstrap.Boost, 1f);
        float maxSeen = 0f;
        foreach (var _ in Seconds(6f))
        {
            maxSeen = Mathf.Max(maxSeen, _player.LocomotionSpeed);
            yield return null;
        }
        Check("hard locomotion cap is never exceeded", maxSeen <= m.HardMaxLocomotionSpeed + 0.5f,
            $"max={maxSeen:0.00} cap={m.HardMaxLocomotionSpeed}");
        Check("boost cannot bypass the cap", _player.LocomotionSpeed <= m.HardMaxLocomotionSpeed + 0.5f,
            $"speed={_player.LocomotionSpeed:0.00}");
        Check("cap is actually reachable", maxSeen > m.HardMaxLocomotionSpeed - 2f, $"max={maxSeen:0.00}");
        ReleaseAll();

        // ---- downhill acceleration with no input ----
        foreach (var _ in Settle(RampSurfacePoint(120f) + Vector3.Up * 3f, 2.0f)) yield return null;
        Check("player is resting on the test ramp",
            _player.IsGrounded && Mathf.Abs(_player.GroundNormal.Dot(Vector3.Up) - Mathf.Cos(Mathf.DegToRad(RampAngleDegrees))) < 0.05f,
            $"grounded={_player.IsGrounded} n={_player.GroundNormal}");
        float v0 = _player.LocomotionSpeed;
        foreach (var _ in Seconds(1.5f)) yield return null;
        float measured = (_player.LocomotionSpeed - v0) / 1.5f;
        float expected = m.Gravity * Mathf.Sin(Mathf.DegToRad(RampAngleDegrees));
        Check("downhill terrain materially accelerates the player",
            measured > expected * 0.55f && measured < expected * 1.15f,
            $"measured={measured:0.00} expected~{expected:0.00} m/s^2 ({v0:0.0} -> {_player.LocomotionSpeed:0.0})");

        // ---- the same must hold on the real generated terrain, not just the rig ----
        foreach (var e in RunRealTerrainSlopeCase()) yield return e;
        foreach (var e in RunBoostPickupCase()) yield return e;

        // ---- uphill propulsion still works without boost ----
        foreach (var _ in Settle(RampSurfacePoint(-60f) + Vector3.Up * 3f, 1.0f)) yield return null;
        _worldDrive = Vector3.Right;             // +X is uphill on the test ramp
        foreach (var _ in Seconds(1.2f)) yield return null;
        float uphillY = _player.GlobalPosition.Y;
        foreach (var _ in Seconds(2.0f)) yield return null;
        Check("base propulsion climbs uphill without boost", _player.GlobalPosition.Y > uphillY + 5f,
            $"{uphillY:0.0} -> {_player.GlobalPosition.Y:0.0} m");
        ReleaseAll();

        // ---- charging applies no artificial slowdown, and locks steering ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        float speedBeforeCharge = _player.LocomotionSpeed;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Check("Space while grounded begins charge", _player.IsCharging);
        Check("steering authority is zero while charging", Mathf.IsZeroApprox(_player.SteeringAuthority),
            $"authority={_player.SteeringAuthority:0.###}");

        Vector3 headingAtChargeStart = FlatVel.Normalized();
        Input.ActionPress(InputBootstrap.MoveLeft, 1f);
        foreach (var _ in Seconds(0.55f)) yield return null;
        Input.ActionRelease(InputBootstrap.MoveLeft);
        float headingDrift = Mathf.RadToDeg(headingAtChargeStart.AngleTo(FlatVel.Normalized()));
        Check("no steering while charging", headingDrift < 1.0f, $"drift={headingDrift:0.00} deg");
        Check("charging applies no artificial slowdown", _player.LocomotionSpeed >= speedBeforeCharge - 0.5f,
            $"{speedBeforeCharge:0.0} -> {_player.LocomotionSpeed:0.0}");

        // ---- charge clamps but never auto-jumps ----
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds * 2.5f)) yield return null;
        Check("charge clamps at maximum", Mathf.IsEqualApprox(_player.Charge01, 1f), $"charge01={_player.Charge01:0.###}");
        Check("full charge does not auto-jump", _player.IsCharging && _player.IsGrounded);

        // ---- full-charge release ----
        float flatBefore = FlatVel.Length();
        int jumpsBefore = _jumpedCount;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        Check("release performs the jump", _jumpedCount == jumpsBefore + 1);
        CheckNear("full hold produces max takeoff", _player.LastTakeoffVerticalSpeed, js.MaxJumpTakeoffVerticalSpeed, 0.05f);
        CheckNear("jump preserves horizontal momentum", FlatVel.Length(), flatBefore, 1.0f);
        Check("jump opens perfect-apex eligibility", _player.JumpArcEligible);

        // ---- a fresh airborne press is slam, never a new charge ----
        foreach (var _ in Frames(6)) yield return null;
        int slamsBefore = _slammedCount;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Check("airborne press does not begin a new charge", !_player.IsCharging);
        Check("airborne press triggers slam", _slammedCount == slamsBefore + 1 && _player.SlamActive);
        Check("early slam is not a perfect apex", !_lastSlamPerfect,
            $"vy at slam={_player.LastSlamVerticalSpeed:0.##} threshold={_player.PerfectApexVerticalSpeedThreshold:0.##}");
        Input.ActionRelease(InputBootstrap.Jump);

        // ---- no double jump ----
        foreach (var _ in Act()) yield return null;
        jumpsBefore = _jumpedCount;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        Check("no double jump", _jumpedCount == jumpsBefore);
        ReleaseAll();
        foreach (var _ in Seconds(2.5f)) yield return null;

        // ---- quick tap produces the minimum takeoff ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Frames(1)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        CheckNear("quick tap produces min takeoff", _player.LastTakeoffVerticalSpeed, js.MinJumpTakeoffVerticalSpeed, 0.6f);
        Check("quick tap charge was near zero", _lastJumpCharge < 0.2f, $"charge01={_lastJumpCharge:0.###}");

        // ---- a release arriving between physics ticks must still jump (held-state fallback) ----
        foreach (var _ in Seconds(2.0f)) yield return null;
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(0.3f)) yield return null;
        Check("charge is active before the off-tick release", _player.IsCharging);
        jumpsBefore = _jumpedCount;
        _releaseJumpFromProcess = true;
        foreach (var _ in Frames(5)) yield return null;
        Check("a release arriving between physics ticks still jumps", _jumpedCount == jumpsBefore + 1 && !_player.IsCharging,
            $"jumps={_jumpedCount - jumpsBefore} charging={_player.IsCharging}");
        ReleaseAll();
        foreach (var _ in Seconds(2.5f)) yield return null;

        // ---- quick tap produces the minimum takeoff ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Frames(1)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Act()) yield return null;
        CheckNear("quick tap produces min takeoff (again, after off-tick case)", _player.LastTakeoffVerticalSpeed, js.MinJumpTakeoffVerticalSpeed, 0.6f);

        // ---- perfect apex ----
        int apexFrames = 0;
        while (_player.VerticalSpeed > _player.PerfectApexVerticalSpeedThreshold * 0.4f && apexFrames++ < 300) yield return null;
        Check("apex window reached", apexFrames < 300, $"frames={apexFrames}");
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        Check("slam inside the apex window is a perfect apex", _lastSlamPerfect,
            $"vy at slam={_player.LastSlamVerticalSpeed:0.###} threshold={_player.PerfectApexVerticalSpeedThreshold:0.##}");
        ReleaseAll();
        foreach (var _ in Seconds(2.0f)) yield return null;

        // ---- slam preserves lateral momentum and commits downward ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.1f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(10)) yield return null;
        ReleaseAll();
        float lateralBefore = FlatVel.Length();
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Check("slam preserves lateral momentum",
            FlatVel.Length() >= lateralBefore * js.SlamLateralRetention - 0.6f,
            $"{lateralBefore:0.0} -> {FlatVel.Length():0.0}");
        Check("slam commits downward", _player.VerticalSpeed <= -js.SlamInitialDownwardSpeed + 0.5f,
            $"vy={_player.VerticalSpeed:0.0}");
        ReleaseAll();
        foreach (var _ in Seconds(2.5f)) yield return null;

        // ---- a fall that was not a player jump can never be a perfect apex (D-070) ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 80f, 0.05f)) yield return null;
        int guard = 0;
        while (_player.IsGrounded && guard++ < 60) yield return null;
        Check("arc from a fall is not apex-eligible", !_player.JumpArcEligible);
        // Widen the window absurdly for this case so the ONLY thing that can deny the
        // bonus is the missing jump arc (D-070), rather than the timing.
        float savedWindow = js.PerfectApexWindowSeconds;
        js.PerfectApexWindowSeconds = 100f;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        Check("the fall slam was well inside the (widened) apex window",
            Mathf.Abs(_player.LastSlamVerticalSpeed) <= _player.PerfectApexVerticalSpeedThreshold,
            $"vy at slam={_player.LastSlamVerticalSpeed:0.###}");
        Check("fall without a jump cannot earn a perfect apex even inside the window", !_lastSlamPerfect);
        js.PerfectApexWindowSeconds = savedWindow;
        ReleaseAll();
        foreach (var _ in Seconds(3.0f)) yield return null;

        // ---- charge release grace after losing ground ----
        foreach (var e in RunChargeGraceCase(true)) yield return e;
        foreach (var e in RunChargeGraceCase(false)) yield return e;

        // ---- air steering is bounded relative to ground steering ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Vector3 groundHeading = FlatVel.Normalized();
        Input.ActionRelease(InputBootstrap.MoveForward);
        Input.ActionPress(InputBootstrap.MoveLeft, 1f);
        foreach (var _ in Seconds(0.5f)) yield return null;
        float groundTurn = Mathf.RadToDeg(groundHeading.AngleTo(FlatVel.Normalized()));
        ReleaseAll();

        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.1f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(8)) yield return null;
        Vector3 airHeading = FlatVel.Normalized();
        Input.ActionRelease(InputBootstrap.MoveForward);
        Input.ActionPress(InputBootstrap.MoveLeft, 1f);
        foreach (var _ in Seconds(0.5f)) yield return null;
        float airTurn = Mathf.RadToDeg(airHeading.AngleTo(FlatVel.Normalized()));
        Check("air steering is bounded and weaker than ground steering",
            airTurn > 0.2f && airTurn < groundTurn * 0.75f, $"air={airTurn:0.0} ground={groundTurn:0.0} deg");
        ReleaseAll();
        foreach (var _ in Seconds(3.0f)) yield return null;

        // ---- cap decomposition across the grounded/airborne transition ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(5f)) yield return null;
        Input.ActionRelease(InputBootstrap.Boost);
        Vector3 preJumpHeading = FlatVel.Normalized();
        float preJumpSpeed = FlatVel.Length();
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Frames(1)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        Input.ActionRelease(InputBootstrap.MoveForward);
        foreach (var _ in Act()) yield return null;
        float airborneSpeed = FlatVel.Length();
        float headingRotation = Mathf.RadToDeg(preJumpHeading.AngleTo(FlatVel.Normalized()));
        Check("grounded->airborne transition does not rotate velocity", headingRotation < 1.0f,
            $"rotation={headingRotation:0.00} deg");
        Check("grounded->airborne transition does not manufacture energy", airborneSpeed <= preJumpSpeed + 0.3f,
            $"{preJumpSpeed:0.00} -> {airborneSpeed:0.00}");
        Check("locomotion stays within the cap while airborne", airborneSpeed <= m.HardMaxLocomotionSpeed + 0.5f,
            $"speed={airborneSpeed:0.00}");
        ReleaseAll();
        foreach (var _ in Seconds(3f)) yield return null;

        // ---- boost works in the air ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(1.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Seconds(js.MaxJumpChargeSeconds + 0.1f)) yield return null;
        Input.ActionRelease(InputBootstrap.Jump);
        foreach (var _ in Frames(6)) yield return null;
        float airSpeedBefore = FlatVel.Length();
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(0.5f)) yield return null;
        Check("boost works in the air", FlatVel.Length() > airSpeedBefore + 2f,
            $"{airSpeedBefore:0.0} -> {FlatVel.Length():0.0}");
        ReleaseAll();
        foreach (var _ in Seconds(3f)) yield return null;

        // ---- boost drain and slow passive regeneration ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        float boostFull = _player.BoostAmount;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(1.0f)) yield return null;
        float drained = boostFull - _player.BoostAmount;
        CheckNear("boost drains at the tuned rate", drained, t.Boost.BoostDrainRate, t.Boost.BoostDrainRate * 0.2f);
        Input.ActionRelease(InputBootstrap.Boost);
        float boostAfterDrain = _player.BoostAmount;
        foreach (var _ in Seconds(1.0f)) yield return null;
        CheckNear("passive boost regeneration follows tuning",
            _player.BoostAmount - boostAfterDrain, t.Boost.PassiveBoostRegen, t.Boost.PassiveBoostRegen * 0.3f);
        ReleaseAll();

        // ---- carve A/B toggle ----
        Input.ActionPress(InputBootstrap.Carve, 1f);
        foreach (var _ in Act()) yield return null;
        Check("carve engages when enabled", _player.CarveActive);
        t.Carve.Enabled = false;
        foreach (var _ in Act()) yield return null;
        Check("carve can be toggled off for A/B testing", !_player.CarveActive);
        t.Carve.Enabled = true;
        ReleaseAll();

        // ---- CCD: a max-speed run into a 0.3 m plate must stop, not tunnel ----
        foreach (var _ in Settle(_wallLaneStart)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        _worldDrive = _laneFwd;
        Input.ActionPress(InputBootstrap.Boost, 1f);
        float maxProgress = float.MinValue, maxSpeedSeen = 0f;
        foreach (var _ in Seconds(6f))
        {
            maxProgress = Mathf.Max(maxProgress, (_player.GlobalPosition - _wallCentre).Dot(_laneFwd));
            maxSpeedSeen = Mathf.Max(maxSpeedSeen, _player.LocomotionSpeed);
            yield return null;
        }
        float finalProgress = (_player.GlobalPosition - _wallCentre).Dot(_laneFwd);
        Check("reached the thin wall at playable max speed",
            maxSpeedSeen > m.HardMaxLocomotionSpeed - 3f && maxProgress > -3f,
            $"maxSpeed={maxSpeedSeen:0.0} closest={maxProgress:0.00} m");
        Check("did not tunnel through a 0.3 m plate at max speed (CCD)",
            finalProgress < 0f && maxProgress < 1.5f,
            $"final={finalProgress:0.00} m maxPast={maxProgress:0.00} m");
        Check("velocity finite after the high-speed impact", _player.Velocity.IsFinite());
        ReleaseAll();
        foreach (var _ in Seconds(0.5f)) yield return null;

        // ---- landing at the cap on a slope bleeds the tangent excess instead of clipping it ----
        foreach (var _ in Settle(_landingStart)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        _worldDrive = _laneFwd;
        Input.ActionPress(InputBootstrap.Boost, 1f);
        int landGuard = 0;
        while (_player.IsGrounded && landGuard++ < 900) yield return null;       // reach the crest and leave it
        Input.ActionRelease(InputBootstrap.Boost);
        _worldDrive = null;
        bool leftCrest = landGuard < 900;
        float speedAtCrest = _player.LocomotionSpeed;
        while (!_player.IsGrounded && landGuard++ < 1200) yield return null;     // land on the slope
        bool landed = landGuard < 1200;
        float prev = _player.LocomotionSpeed, worstDrop = 0f, peakOverCap = 0f;
        foreach (var _ in Seconds(0.6f))
        {
            float now = _player.LocomotionSpeed;
            worstDrop = Mathf.Max(worstDrop, prev - now);
            peakOverCap = Mathf.Max(peakOverCap, now - m.HardMaxLocomotionSpeed);
            prev = now;
            yield return null;
        }
        Check("reached the cap and flew off the landing crest", leftCrest && speedAtCrest > m.HardMaxLocomotionSpeed - 3f,
            $"leftCrest={leftCrest} speed={speedAtCrest:0.0}");
        Check("landed on the 25 deg slope", landed);
        Check("landing at the cap on a slope produces a tangent excess (allowance engaged)", peakOverCap > 1.0f,
            $"peak over cap={peakOverCap:0.00} m/s (expect ~{m.HardMaxLocomotionSpeed * (1f / Mathf.Cos(Mathf.DegToRad(LandingSlopeDegrees)) - 1f):0.0})");
        float maxTickDrop = (m.LandingCapBleed + 5f) / Engine.PhysicsTicksPerSecond + 0.5f;
        Check("the excess bleeds instead of clipping in one tick", worstDrop < maxTickDrop,
            $"worst single-tick drop={worstDrop:0.00} m/s allowed<{maxTickDrop:0.00}");
        Check("allowance decays back to the cap", _player.LocomotionSpeed <= m.HardMaxLocomotionSpeed + 0.5f,
            $"speed={_player.LocomotionSpeed:0.0} cap={m.HardMaxLocomotionSpeed}");
        ReleaseAll();

        // ---- camera occlusion probe pulls the camera in when terrain blocks the focus ----
        var rig = (Rushcore.Camera.CameraRig)_player.CameraBasis!;
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        Check("camera is unobstructed in the open", rig.OcclusionFraction > 0.95f, $"fraction={rig.OcclusionFraction:0.00}");
        foreach (var _ in Settle(_wallCentre - Vector3.Up * 6f + _laneFwd * 3f + Vector3.Up * 3f)) yield return null;
        rig.SnapYawToward(_laneFwd);
        foreach (var _ in Frames(4)) yield return null;
        Check("camera pulls in when a wall blocks the line of sight", rig.OcclusionFraction < 0.5f,
            $"fraction={rig.OcclusionFraction:0.00}");
        t.Camera.OcclusionProbe = false;
        foreach (var _ in Seconds(1.5f)) yield return null;
        Check("occlusion probe can be disabled", rig.OcclusionFraction > 0.95f, $"fraction={rig.OcclusionFraction:0.00}");
        t.Camera.OcclusionProbe = true;

        // ---- chase camera: yaw follows the trajectory, never flips on reverse ----
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f)) yield return null;
        t.Camera.FollowTrajectoryYaw = true;
        rig.SnapYawToward(Vector3.Forward);
        _worldDrive = Vector3.Right;
        foreach (var _ in Seconds(3.0f)) yield return null;
        Check("chase yaw converges on the travel heading",
            rig.FlatForward.Dot(Vector3.Right) > 0.96f,
            $"camFwd={rig.FlatForward} yaw={rig.YawDegreesCurrent:0.0}");
        _worldDrive = Vector3.Left;                      // brake through zero and reverse
        foreach (var _ in Seconds(2.5f)) yield return null;
        Check("reversing does not flip the chase camera",
            rig.FlatForward.Dot(Vector3.Right) > 0.7f && _player.Velocity.Dot(Vector3.Right) < -2f,
            $"camFwd={rig.FlatForward} vel.x={_player.Velocity.X:0.0}");
        ReleaseAll();
        t.Camera.FollowTrajectoryYaw = false;
        foreach (var _ in Seconds(2.5f)) yield return null;
        float fixedErr = Mathf.Abs(Mathf.Wrap(rig.YawDegreesCurrent - t.Camera.YawDegrees, -180f, 180f));
        Check("fixed-yaw A/B mode returns to the configured yaw", fixedErr < 3f, $"err={fixedErr:0.0} deg");
        t.Camera.FollowTrajectoryYaw = true;

        // ---- real terrain crest run: climb, mesa lip, ramp, drop, chasms at speed ----
        foreach (var _ in Settle(_debug.World.SurfacePoint(TerrainHeightField.Ramp1X, 210f, m.BallRadius + 1.5f), 0.8f)) yield return null;
        rig.SnapYawToward(Vector3.Forward);
        foreach (var _ in Frames(3)) yield return null;
        _player.RefillBoost(t.Boost.BoostCapacity);
        _worldDrive = Vector3.Forward;                   // -Z through the whole central column
        Input.ActionPress(InputBootstrap.Boost, 1f);
        _visFrames = 0; _visBlocked = 0; _visCheck = true;
        int floored = 0;
        foreach (var _ in Seconds(11f))
        {
            if (rig.FlooredThisFrame) floored++;
            if (_player.GlobalPosition.Z < -430f) break;
            yield return null;
        }
        _visCheck = false;
        ReleaseAll();
        Check("crest run covered the central column", _player.GlobalPosition.Z < -150f,
            $"z={_player.GlobalPosition.Z:0}");
        Check("camera->ball line stays clear over crests (<=2% of ticks)",
            _visFrames > 0 && _visBlocked <= Mathf.CeilToInt(_visFrames * 0.02f),
            $"blocked {_visBlocked}/{_visFrames} ticks, floored {floored} frames");

        // ---- tuning persistence: diff-only JSON round-trip on a scratch path ----
        {
            const string scratch = "user://tuning_selftest.json";
            float driveDefault = m.GroundDriveAcceleration;
            bool carveDefault = t.Carve.Enabled;
            Check("harness starts on compiled defaults", t.OverrideCount == 0, $"overrides={t.OverrideCount}");
            m.GroundDriveAcceleration = driveDefault + 7f;
            t.Carve.Enabled = !carveDefault;
            Check("modified values are counted", t.OverrideCount == 2, $"overrides={t.OverrideCount}");
            Check("override saves", t.SaveOverride(scratch));

            string text = Godot.FileAccess.GetFileAsString(scratch);
            var parsed = Json.ParseString(text).Obj as Godot.Collections.Dictionary;
            var values = parsed?["values"].Obj as Godot.Collections.Dictionary;
            Check("override file is a versioned diff of only the changed values",
                parsed is not null && (int)parsed["version"] == 1 && values is { Count: 2 },
                $"keys={values?.Count}");

            t.ResetAll();
            Check("reset returns to compiled defaults", t.OverrideCount == 0 && Mathf.IsEqualApprox(m.GroundDriveAcceleration, driveDefault));
            int applied = t.LoadOverride(scratch);
            Check("override loads on top of defaults",
                applied == 2 && Mathf.IsEqualApprox(m.GroundDriveAcceleration, driveDefault + 7f) && t.Carve.Enabled == !carveDefault,
                $"applied={applied} drive={m.GroundDriveAcceleration}");

            t.ResetAll();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(scratch));
            Check("scratch override removed and defaults restored", !Godot.FileAccess.FileExists(scratch) && t.OverrideCount == 0);
        }

        // ---- named presets: save, list, load, delete ----
        {
            var bundled = GameplayTuning.ListPresets();
            Check("bundled starter presets were installed", bundled.Contains("baseline") && bundled.Contains("iso-classic"),
                $"presets={string.Join(",", bundled)}");
            // Every bundled preset must load with every key recognised: a typo in a key
            // would otherwise be dropped silently by the loader.
            var unrecognised = new List<string>();
            foreach (string preset in bundled)
            {
                var doc = Json.ParseString(Godot.FileAccess.GetFileAsString(GameplayTuning.PresetPath(preset))).Obj as Godot.Collections.Dictionary;
                int keyCount = doc?["values"].Obj is Godot.Collections.Dictionary dv ? dv.Count : -1;
                int applied = t.LoadPreset(preset);
                if (applied != keyCount) unrecognised.Add($"{preset}({applied}/{keyCount})");
            }
            Check("every bundled preset loads with all keys recognised", unrecognised.Count == 0,
                string.Join(" ", unrecognised));
            t.ResetAll();
            string name = GameplayTuning.SanitizePresetName(" selftest/variant #A ");
            Check("preset names are made file-safe", name == "selftest_variant__A", $"name='{name}'");
            float driveDefault = m.GroundDriveAcceleration;
            m.GroundDriveAcceleration = driveDefault + 3f;
            Check("preset saves", t.SavePreset(name));
            Check("preset appears in the list", GameplayTuning.ListPresets().Contains(name));
            t.ResetAll();
            Check("preset loads on top of defaults",
                t.LoadPreset(name) == 1 && Mathf.IsEqualApprox(m.GroundDriveAcceleration, driveDefault + 3f));
            t.ResetAll();
            Check("preset deletes", GameplayTuning.DeletePreset(name) && !GameplayTuning.PresetExists(name));
        }

        // ---- fall recovery ----
        foreach (var _ in Seconds(0.5f)) yield return null;
        _player.SetCheckpoint(PlatformCenter + Vector3.Up * 3f);
        int recoveredBefore = _recoveredCount;
        _player.TeleportTo(new Vector3(0f, -5000f, 0f));
        foreach (var _ in Frames(4)) yield return null;
        _player.RequestRecovery();
        foreach (var _ in Frames(8)) yield return null;
        Check("recovery restores a valid pose",
            _player.GlobalPosition.DistanceTo(PlatformCenter + Vector3.Up * 3f) < 4f,
            $"pos={_player.GlobalPosition}");
        Check("recovery clears transient movement state",
            !_player.IsCharging && !_player.SlamActive && !_player.JumpArcEligible);
        Check("recovery resets physics interpolation", _recoveredCount > recoveredBefore);

        foreach (var _ in Seconds(0.5f)) yield return null;
        Check("velocity is finite at the end of the run", _player.Velocity.IsFinite());
        Check("camera lens never went below the heightfield during the whole run", _groundViolations == 0,
            $"violations={_groundViolations}");
    }

    /// <summary>
    /// Validates the calibration terrain itself and then confirms gravity still
    /// materially accelerates the player on it (D-005). The engineered grade fan exists
    /// precisely so this can be measured against known geometry, and it also feeds the
    /// scale-calibration gate M1.
    /// </summary>
    private IEnumerable RunRealTerrainSlopeCase()
    {
        var world = _debug.World;
        var m = _debug.Tuning.Movement;

        // --- geometry: the instruments must have the gradients they advertise ---
        CheckGrade("8 deg", TerrainHeightField.Grade8X, 8f);
        CheckGrade("15 deg", TerrainHeightField.Grade15X, 15f);
        CheckGrade("25 deg", TerrainHeightField.Grade25X, 25f);

        float laneMin = float.MaxValue, laneMax = float.MinValue;
        for (float d = 0f; d <= TerrainHeightField.LaneLength; d += 10f)
        {
            float h = world.SampleHeight(TerrainHeightField.LaneStartX - d, TerrainHeightField.LaneZ);
            laneMin = Mathf.Min(laneMin, h);
            laneMax = Mathf.Max(laneMax, h);
        }
        Check("measured calibration lane is flat over its full 500 m",
            laneMax - laneMin < 0.5f, $"height varies {laneMax - laneMin:0.###} m");

        // --- behaviour: steeper grades must accelerate the player harder ---
        float a15 = 0f, a25 = 0f;
        foreach (var e in MeasureGradeAcceleration(TerrainHeightField.Grade15X, 15f, v => a15 = v)) yield return e;
        foreach (var e in MeasureGradeAcceleration(TerrainHeightField.Grade25X, 25f, v => a25 = v)) yield return e;

        float ideal15 = m.Gravity * Mathf.Sin(Mathf.DegToRad(15f));
        float ideal25 = m.Gravity * Mathf.Sin(Mathf.DegToRad(25f));
        Check("15 deg grade materially accelerates the player", a15 > ideal15 * 0.7f,
            $"measured={a15:0.00} ideal~{ideal15:0.00} m/s^2");
        Check("25 deg grade materially accelerates the player", a25 > ideal25 * 0.7f,
            $"measured={a25:0.00} ideal~{ideal25:0.00} m/s^2");
        Check("steeper terrain is strategically faster", a25 > a15 + 1.0f,
            $"15deg={a15:0.00} vs 25deg={a25:0.00} m/s^2");
    }

    /// <summary>
    /// The toy's active-refill hook: rolling the 15 deg reward line must collect rings
    /// and refill the meter by the tuned amount (03 §10, D-020).
    /// </summary>
    private IEnumerable RunBoostPickupCase()
    {
        var t = _debug.Tuning;

        // Drain the meter while parked on the rig (boost persists across teleports), so
        // the first ring collected on the reward line produces a measurable refill.
        foreach (var _ in Settle(PlatformCenter + Vector3.Up * 3f, 0.6f)) yield return null;
        Input.ActionPress(InputBootstrap.Boost, 1f);
        foreach (var _ in Seconds(t.Boost.BoostCapacity / Mathf.Max(1f, t.Boost.BoostDrainRate) + 0.2f)) yield return null;
        Input.ActionRelease(InputBootstrap.Boost);

        Vector3 start = _debug.World.SurfacePoint(TerrainHeightField.Grade15X, TerrainHeightField.FanCrestZ - 4f,
            t.Movement.BallRadius + 1.5f);
        foreach (var _ in Settle(start, 0.3f)) yield return null;
        float before = _player.BoostAmount;
        int pickupsBefore = _pickupCount;

        int guard = 0;
        while (_pickupCount == pickupsBefore && guard++ < 600) yield return null;
        foreach (var _ in Act()) yield return null;

        Check("boost pickup line is collectable on the 15 deg grade", _pickupCount > pickupsBefore,
            $"pickups={_pickupCount - pickupsBefore} after {guard} frames");
        Check("pickup refills boost by the tuned amount",
            _player.BoostAmount >= before + t.Boost.PickupRefillAmount - 2f,
            $"{before:0.0} -> {_player.BoostAmount:0.0} (expected +{t.Boost.PickupRefillAmount})");
        ReleaseAll();
    }

    private void CheckGrade(string label, float x, float expectedDegrees)
    {
        var world = _debug.World;
        // Sample inside the linear section, clear of the eased crest and runout.
        float hi = world.SampleHeight(x, 120f);
        float lo = world.SampleHeight(x, 80f);
        float measured = Mathf.RadToDeg(Mathf.Atan((hi - lo) / 40f));
        CheckNear($"grade fan {label} lane has its stated gradient", measured, expectedDegrees, 1.0f);
    }

    private IEnumerable MeasureGradeAcceleration(float x, float degrees, Action<float> report)
    {
        Vector3 start = _debug.World.SurfacePoint(x, 115f, _debug.Tuning.Movement.BallRadius + 1.5f);
        foreach (var _ in Settle(start, 0.9f)) yield return null;
        float v0 = _player.LocomotionSpeed;
        foreach (var _ in Seconds(1.5f)) yield return null;
        report((_player.LocomotionSpeed - v0) / 1.5f);
        ReleaseAll();
    }

    /// <summary>
    /// Drives off the plate edge while charging. releaseInsideGrace=true must still
    /// jump; false must cancel the charge instead (03 §7).
    /// </summary>
    private IEnumerable RunChargeGraceCase(bool releaseInsideGrace)
    {
        var js = _debug.Tuning.JumpSlam;
        string label = releaseInsideGrace ? "inside grace" : "after grace expiry";

        foreach (var _ in Settle(PlatformCenter - Forward * 150f + Vector3.Up * 3f)) yield return null;
        Input.ActionPress(InputBootstrap.MoveForward, 1f);
        foreach (var _ in Seconds(2.0f)) yield return null;
        Input.ActionPress(InputBootstrap.Jump, 1f);
        foreach (var _ in Act()) yield return null;

        int guard = 0;
        while (_player.IsRawGrounded && guard++ < 1200) yield return null;
        bool leftGround = guard < 1200;

        int jumpsBefore = _jumpedCount;
        int cancelsBefore = _chargeCanceledCount;

        if (releaseInsideGrace)
        {
            Input.ActionRelease(InputBootstrap.Jump);
            foreach (var _ in Act()) yield return null;
            Check($"charge survives ground loss and releasing {label} still jumps",
                leftGround && _jumpedCount == jumpsBefore + 1,
                $"leftGround={leftGround} jumps={_jumpedCount - jumpsBefore}");
        }
        else
        {
            foreach (var _ in Seconds(js.ChargeReleaseGraceSeconds + 0.25f)) yield return null;
            bool canceled = _chargeCanceledCount == cancelsBefore + 1 && !_player.IsCharging;
            Input.ActionRelease(InputBootstrap.Jump);
            foreach (var _ in Act()) yield return null;
            Check($"charge cancels {label} and release does not jump",
                leftGround && canceled && _jumpedCount == jumpsBefore,
                $"leftGround={leftGround} canceled={canceled} jumps={_jumpedCount - jumpsBefore}");
        }

        ReleaseAll();
        foreach (var _ in Seconds(0.4f)) yield return null;
    }
}
