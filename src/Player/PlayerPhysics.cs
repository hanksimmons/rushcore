using Godot;
using Rushcore.Core;
using Rushcore.Tuning;

namespace Rushcore.Player;

public enum MovementState { Grounded, ChargingJump, Airborne, Slam }

public enum SpeedBand { Roll, Rush, Crush, Overdrive }

/// <summary>
/// Arcade-physics rolling controller (03). Real Jolt gravity/collision stays active;
/// this class shapes the body state inside <see cref="_IntegrateForces"/> only.
/// CustomIntegrator is deliberately left off (D-071).
/// </summary>
public partial class PlayerPhysics : RigidBody3D
{
    private readonly GameplayTuning _t;
    private readonly SphereShape3D _shape = new();

    // ---- input snapshot, sampled on the main thread in _PhysicsProcess ----
    private Vector2 _moveInput;
    private bool _boostHeld;
    private bool _jumpPressedEdge, _jumpReleasedEdge, _jumpHeld;

    // ---- movement state (03 §3: track only what behavior needs) ----
    private bool _rawGrounded;
    private float _groundStick;
    private Vector3 _groundNormal = Vector3.Up;
    private float _jumpLockout;
    private bool _isCharging;
    private float _chargeSeconds;
    private float _chargeGrace;
    private bool _jumpArcEligible;
    private bool _slamUsedThisArc;
    private bool _slamActive;
    private bool _lastSlamPerfect;
    private bool _boostActive;
    private float _boost;
    private float _defaultGravity = 9.8f;
    private float _capAllowance;
    private bool _landedThisTick;

    // ---- recovery ----
    private Vector3 _checkpoint;
    private float _checkpointTimer;
    private bool _pendingTeleport;
    private Vector3 _teleportPos;
    private bool _needsInterpolationReset;

    /// <summary>
    /// Solver friction for the player/terrain pair. The arcade controller owns traction
    /// and resistance (03 §1), so this is kept near zero and <c>DragCoefficient</c> is the
    /// single tunable resistance. Higher values silently tax slope acceleration (D-005)
    /// without appearing in the tuning panel.
    /// </summary>
    public const float ArcadeSurfaceFriction = 0.02f;

    /// <summary>Ground grace used for locomotion only. Charge start still requires a raw contact.</summary>
    private const float GroundStickSeconds = 0.05f;
    private const float JumpLockoutSeconds = 0.08f;
    private const float CheckpointIntervalSeconds = 0.75f;

    public PlayerPhysics(GameplayTuning tuning)
    {
        _t = tuning;
        _boost = tuning.Boost.BoostCapacity;
    }

    // ---------------- public / debug state contract ----------------
    public MovementState State { get; private set; } = MovementState.Airborne;
    public bool IsGrounded { get; private set; }
    public bool IsRawGrounded => _rawGrounded;
    public Vector3 GroundNormal => _groundNormal;
    public Vector3 Velocity { get; private set; }
    /// <summary>Grounded: ground-tangent speed. Airborne: world-XZ speed (03 §5).</summary>
    public float LocomotionSpeed { get; private set; }
    public float VerticalSpeed => Velocity.Y;
    public float LocomotionCap => _t.Movement.HardMaxLocomotionSpeed;
    public SpeedBand Band { get; private set; } = SpeedBand.Roll;
    public bool IsCharging => _isCharging;
    public float ChargeSeconds => _chargeSeconds;
    public float Charge01 => Mathf.Clamp(_chargeSeconds / Mathf.Max(0.001f, _t.JumpSlam.MaxJumpChargeSeconds), 0f, 1f);
    public float ComputedTakeoffSpeed =>
        Mathf.Lerp(_t.JumpSlam.MinJumpTakeoffVerticalSpeed, _t.JumpSlam.MaxJumpTakeoffVerticalSpeed, Charge01);
    public bool JumpArcEligible => _jumpArcEligible && !_slamUsedThisArc;
    /// <summary>|vY| threshold for a perfect apex: gravity * window / 2 (D-017).</summary>
    public float PerfectApexVerticalSpeedThreshold =>
        _t.Movement.Gravity * Mathf.Max(0f, _t.JumpSlam.PerfectApexWindowSeconds) * 0.5f;
    /// <summary>Cap currently enforced: base cap plus any decaying landing allowance.</summary>
    public float EffectiveLocomotionCap => _t.Movement.HardMaxLocomotionSpeed + _capAllowance;
    public bool SlamActive => _slamActive;
    /// <summary>Vertical speed established by the most recent jump release.</summary>
    public float LastTakeoffVerticalSpeed { get; private set; }
    /// <summary>Vertical speed observed at the instant the most recent slam began.</summary>
    public float LastSlamVerticalSpeed { get; private set; }
    public bool LastSlamWasPerfect => _lastSlamPerfect;
    public bool BoostActive => _boostActive;
    public float BoostAmount => _boost;
    public float Boost01 => Mathf.Clamp(_boost / Mathf.Max(0.001f, _t.Boost.BoostCapacity), 0f, 1f);
    public Vector2 InputVector => _moveInput;
    /// <summary>Effective lateral steering acceleration applied this physics step (m/s^2).</summary>
    public float SteeringAuthority { get; private set; }
    public float ImpactPowerEstimate { get; private set; }
    public Vector3 CheckpointPosition => _checkpoint;

    public ICameraBasis? CameraBasis { get; set; }

    // ---------------- semantic events (05 §19) ----------------
    public event Action? JumpChargeStarted;
    public event Action? JumpChargeCanceled;
    public event Action<float>? Jumped;
    public event Action<bool>? Slammed;
    public event Action<float, bool, bool>? Landed;
    public event Action<bool>? BoostActiveChanged;
    public event Action<SpeedBand>? SpeedBandChanged;
    public event Action? Recovered;

    public override void _Ready()
    {
        Name = "Player";
        _shape.Radius = _t.Movement.BallRadius;
        AddChild(new CollisionShape3D { Shape = _shape, Name = "Collider" });

        Mass = 1.0f;
        ContinuousCd = true;                 // D-066: tunneling is a first-order risk
        CanSleep = false;                    // D-071
        ContactMonitor = true;               // D-067: grounded state reads direct contacts
        MaxContactsReported = 8;
        CustomIntegrator = false;            // keep standard Jolt gravity/damping
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0f;                     // drag is applied explicitly below
        AngularDampMode = DampMode.Replace;
        AngularDamp = 0f;
        // The collider is a true sphere but visual roll is presentation-driven (03 §13),
        // so the simulated body itself never needs to spin.
        AxisLockAngularX = AxisLockAngularY = AxisLockAngularZ = true;
        PhysicsMaterialOverride = new PhysicsMaterial { Friction = ArcadeSurfaceFriction, Bounce = 0f };

        _defaultGravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8f);
        // Semantic events are raised from inside _IntegrateForces and subscribers touch
        // scene state directly; that is only safe while physics runs on the main thread.
        if ((bool)ProjectSettings.GetSetting("physics/3d/run_on_separate_thread", false))
            GD.PushError("[RUSHCORE] physics/3d/run_on_separate_thread is enabled; player events assume main-thread physics.");
        _checkpoint = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        _moveInput = InputBootstrap.ReadMoveVector();
        _boostHeld = Input.IsActionPressed(InputBootstrap.Boost);
        _jumpHeld = Input.IsActionPressed(InputBootstrap.Jump);
        if (Input.IsActionJustPressed(InputBootstrap.Jump)) _jumpPressedEdge = true;
        if (Input.IsActionJustReleased(InputBootstrap.Jump)) _jumpReleasedEdge = true;

        // Live tunables that live on engine objects rather than in our integration math.
        GravityScale = _t.Movement.Gravity / Mathf.Max(0.001f, _defaultGravity);
        if (!Mathf.IsEqualApprox(_shape.Radius, _t.Movement.BallRadius))
            _shape.Radius = _t.Movement.BallRadius;
    }

    public override void _Process(double delta)
    {
        if (_needsInterpolationReset)
        {
            _needsInterpolationReset = false;
            ResetPhysicsInterpolation();   // D-068: no old->new streak after a teleport
            Recovered?.Invoke();
        }
    }

    // ---------------- commands ----------------
    public void RefillBoost(float amount) =>
        _boost = Mathf.Clamp(_boost + amount, 0f, _t.Boost.BoostCapacity);

    public void SetCheckpoint(Vector3 position) => _checkpoint = position;

    public void RequestRecovery() => TeleportTo(_checkpoint);

    public void TeleportTo(Vector3 position)
    {
        _teleportPos = position;
        _pendingTeleport = true;
    }

    // ---------------- physics ----------------
    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float dt = (float)state.Step;
        if (dt <= 0f) return;

        if (_pendingTeleport)
        {
            ApplyTeleport(state);
            return;
        }

        Vector3 v = state.LinearVelocity;
        if (!v.IsFinite())
        {
            GD.PushError("[RUSHCORE] Non-finite player velocity; recovering.");
            _pendingTeleport = true;
            _teleportPos = _checkpoint;
            ApplyTeleport(state);
            return;
        }

        float preVerticalSpeed = v.Y;
        bool wasGrounded = IsGrounded;

        _landedThisTick = false;
        UpdateGroundState(state, dt);
        HandleLanding(wasGrounded, preVerticalSpeed);
        UpdateJumpInput(dt, ref v);

        // ---- locomotion plane: ground tangent when grounded, world XZ when airborne ----
        Vector3 planeNormal = IsGrounded ? _groundNormal : Vector3.Up;
        float vN = v.Dot(planeNormal);
        Vector3 vT = v - planeNormal * vN;
        float speed = vT.Length();

        var m = _t.Movement;
        float cap = Mathf.Max(0.001f, m.HardMaxLocomotionSpeed);
        float speed01 = Mathf.Clamp(speed / cap, 0f, 1f);

        // Landing converts world-horizontal speed into ground-tangent speed, which is
        // larger by 1/cos(slope). Clipping that in one tick reads as hitting a wall, so
        // the excess becomes a short-lived allowance that bleeds away (03 §5, D-069).
        if (_landedThisTick && speed > cap) _capAllowance = Mathf.Max(_capAllowance, speed - cap);
        _capAllowance = Mathf.Max(0f, _capAllowance - m.LandingCapBleed * dt);
        float effectiveCap = cap + _capAllowance;

        Vector3 curDir = speed > 0.5f ? vT / speed : Vector3.Zero;
        Vector3 desiredDir = ComputeDesiredDirection(planeNormal, curDir);
        float inputMag = Mathf.Min(_moveInput.Length(), 1f);

        // ---- authority ----
        float lateral = m.GroundSteeringLateralAccel * Mathf.Lerp(1f, m.HighSpeedSteeringMultiplier, speed01);
        float drive = m.GroundDriveAcceleration;
        if (!IsGrounded)
        {
            lateral *= m.AirControlMultiplier;
            drive *= m.AirControlMultiplier;
        }
        if (_slamActive) lateral *= _t.JumpSlam.SlamSteeringMultiplier;
        // Charge locks line-control authority only; drive/gravity/boost are untouched (03 §4).
        if (_isCharging) lateral = 0f;
        SteeringAuthority = lateral;

        // ---- steer: rotate the travel direction, never rewrite it ----
        if (speed > 0.5f && desiredDir != Vector3.Zero && lateral > 0f)
        {
            float angle = curDir.AngleTo(desiredDir);
            if (angle > 1e-5f)
            {
                // a_lat = v * omega  =>  turnRadius = v^2 / a_lat
                float maxAngle = lateral * dt / speed;
                Vector3 axis = curDir.Cross(desiredDir);
                axis = axis.LengthSquared() < 1e-8f ? planeNormal : axis.Normalized();
                curDir = curDir.Rotated(axis, Mathf.Min(maxAngle, angle));
                vT = curDir * speed;
            }
        }

        // ---- longitudinal drive (negative alignment brakes) ----
        if (inputMag > 0.01f && desiredDir != Vector3.Zero)
        {
            if (speed > 0.5f)
            {
                float align = curDir.Dot(desiredDir);
                vT += curDir * (drive * inputMag * align * dt);
            }
            else
            {
                vT += desiredDir * (drive * inputMag * dt);
            }
        }

        ApplyBoost(ref vT, planeNormal, curDir, desiredDir, speed01, dt);

        // ---- drag ----
        float dragK = m.DragCoefficient;
        vT *= Mathf.Max(0f, 1f - dragK * dt);

        // ---- slam commits downward; vertical is never touched by the locomotion cap ----
        if (_slamActive && !IsGrounded)
        {
            float strength = _lastSlamPerfect ? _t.JumpSlam.PerfectApexSlamStrengthMultiplier : 1f;
            vN -= _t.JumpSlam.SlamDownwardAcceleration * strength * dt;
        }

        // ---- hard locomotion cap: clamp magnitude only, never rotate (03 §5, D-069) ----
        float locSpeed = vT.Length();
        if (locSpeed > effectiveCap)
        {
            vT *= effectiveCap / locSpeed;
            locSpeed = effectiveCap;
        }

        v = vT + planeNormal * vN;
        state.LinearVelocity = v;

        // ---- published state ----
        Velocity = v;
        LocomotionSpeed = locSpeed;
        State = _isCharging ? MovementState.ChargingJump
              : _slamActive ? MovementState.Slam
              : IsGrounded ? MovementState.Grounded
              : MovementState.Airborne;
        ImpactPowerEstimate = v.Length() * (_lastSlamPerfect && _slamActive ? _t.JumpSlam.PerfectApexImpactMultiplier : 1f);
        UpdateSpeedBand(locSpeed);
        UpdateBoostMeter(dt);
        UpdateCheckpoint(state, dt);

        _jumpPressedEdge = false;
        _jumpReleasedEdge = false;
    }

    private void ApplyTeleport(PhysicsDirectBodyState3D state)
    {
        _pendingTeleport = false;
        Transform3D tr = state.Transform;
        tr.Origin = _teleportPos;
        tr.Basis = Basis.Identity;
        state.Transform = tr;
        state.LinearVelocity = Vector3.Zero;
        state.AngularVelocity = Vector3.Zero;

        Velocity = Vector3.Zero;
        LocomotionSpeed = 0f;
        _isCharging = false;
        _chargeSeconds = 0f;
        _chargeGrace = 0f;
        _slamActive = false;
        _lastSlamPerfect = false;
        _jumpArcEligible = false;
        _slamUsedThisArc = false;
        _jumpLockout = 0f;
        _groundStick = 0f;
        _capAllowance = 0f;
        _rawGrounded = false;
        IsGrounded = false;
        _groundNormal = Vector3.Up;
        State = MovementState.Airborne;
        _needsInterpolationReset = true;
    }

    private void UpdateGroundState(PhysicsDirectBodyState3D state, float dt)
    {
        if (_jumpLockout > 0f) _jumpLockout -= dt;

        Vector3 sum = Vector3.Zero;
        int hits = 0;
        int contacts = state.GetContactCount();
        for (int i = 0; i < contacts; i++)
        {
            Vector3 n = state.GetContactLocalNormal(i);
            if (n.Dot(Vector3.Up) >= _t.Movement.MinGroundNormalDot)
            {
                sum += n;
                hits++;
            }
        }

        _rawGrounded = hits > 0 && _jumpLockout <= 0f;
        if (_rawGrounded)
        {
            _groundNormal = sum.Normalized();     // stable representative normal (03 §3)
            _groundStick = GroundStickSeconds;
        }
        else
        {
            if (_jumpLockout > 0f) _groundStick = 0f;
            else _groundStick -= dt;
            if (_groundStick <= 0f) _groundNormal = Vector3.Up;
        }

        IsGrounded = _rawGrounded || _groundStick > 0f;
    }

    private void HandleLanding(bool wasGrounded, float preVerticalSpeed)
    {
        if (wasGrounded || !IsGrounded) return;

        _landedThisTick = true;
        bool wasSlam = _slamActive;
        bool wasPerfect = _slamActive && _lastSlamPerfect;
        _slamActive = false;
        _jumpArcEligible = false;
        _slamUsedThisArc = false;

        if (wasPerfect) RefillBoost(_t.Boost.PerfectApexRefillAmount);   // active refill hook
        Landed?.Invoke(Mathf.Abs(preVerticalSpeed), wasSlam, wasPerfect);
    }

    private void UpdateJumpInput(float dt, ref Vector3 v)
    {
        var js = _t.JumpSlam;

        if (_isCharging)
        {
            _chargeSeconds = Mathf.Min(_chargeSeconds + dt, js.MaxJumpChargeSeconds);   // clamps, never auto-jumps

            if (!_rawGrounded)
            {
                _chargeGrace += dt;
                if (_chargeGrace > js.ChargeReleaseGraceSeconds)
                {
                    _isCharging = false;
                    _chargeSeconds = 0f;
                    _chargeGrace = 0f;
                    JumpChargeCanceled?.Invoke();
                }
            }
            else
            {
                _chargeGrace = 0f;
            }
        }

        if (_jumpPressedEdge)
        {
            if (_rawGrounded && !_isCharging)
            {
                // Charge begins only from a real ground contact; there is deliberately no
                // airborne coyote charge, which keeps the airborne press unambiguously slam.
                _isCharging = true;
                _chargeSeconds = 0f;
                _chargeGrace = 0f;
                JumpChargeStarted?.Invoke();
            }
            else if (!IsGrounded && !_isCharging && !_slamActive)
            {
                StartSlam(ref v);
            }
        }

        // Release is edge-driven, but the held-state fallback means a jump can never be
        // stranded if press and release land inside the same physics tick.
        if (_isCharging && (_jumpReleasedEdge || !_jumpHeld))
        {
            float charge01 = Charge01;
            float takeoff = Mathf.Lerp(js.MinJumpTakeoffVerticalSpeed, js.MaxJumpTakeoffVerticalSpeed, charge01);
            // Preserve all useful horizontal momentum; only establish upward velocity (D-012).
            v.Y = Mathf.Max(v.Y, takeoff);
            LastTakeoffVerticalSpeed = v.Y;

            _isCharging = false;
            _chargeSeconds = 0f;
            _chargeGrace = 0f;
            _jumpArcEligible = true;      // only a released jump opens perfect-apex eligibility (D-070)
            _slamUsedThisArc = false;
            _slamActive = false;
            _lastSlamPerfect = false;
            _jumpLockout = JumpLockoutSeconds;
            _rawGrounded = false;
            IsGrounded = false;
            _groundStick = 0f;
            _groundNormal = Vector3.Up;
            Jumped?.Invoke(charge01);
        }
    }

    private void StartSlam(ref Vector3 v)
    {
        var js = _t.JumpSlam;
        bool perfect = _jumpArcEligible
                       && !_slamUsedThisArc
                       && Mathf.Abs(v.Y) <= PerfectApexVerticalSpeedThreshold;

        LastSlamVerticalSpeed = v.Y;
        _slamActive = true;
        _slamUsedThisArc = true;
        _lastSlamPerfect = perfect;

        float strength = perfect ? js.PerfectApexSlamStrengthMultiplier : 1f;
        v.X *= js.SlamLateralRetention;
        v.Z *= js.SlamLateralRetention;
        v.Y = Mathf.Min(v.Y, -js.SlamInitialDownwardSpeed * strength);

        Slammed?.Invoke(perfect);
    }

    private Vector3 ComputeDesiredDirection(Vector3 planeNormal, Vector3 curDir)
    {
        // While charging, propulsion follows the existing travel heading instead of
        // letting WASD redirect the ball (03 §4).
        if (_isCharging) return curDir;

        Vector3 fwd = CameraBasis?.FlatForward ?? Vector3.Forward;
        Vector3 right = CameraBasis?.FlatRight ?? Vector3.Right;
        Vector3 desired = right * _moveInput.X + fwd * _moveInput.Y;
        desired -= planeNormal * desired.Dot(planeNormal);
        return desired.LengthSquared() > 1e-6f ? desired.Normalized() : Vector3.Zero;
    }

    private void ApplyBoost(ref Vector3 vT, Vector3 planeNormal, Vector3 curDir, Vector3 desiredDir, float speed01, float dt)
    {
        var b = _t.Boost;
        bool wantBoost = _boostHeld && _boost > 0f;
        if (wantBoost != _boostActive)
        {
            _boostActive = wantBoost;
            BoostActiveChanged?.Invoke(wantBoost);
        }
        if (!wantBoost) return;

        Vector3 travelDir = curDir != Vector3.Zero ? curDir : desiredDir;
        if (travelDir == Vector3.Zero) return;

        // Current trajectory dominates increasingly with speed (D-019).
        float blend = b.BoostDirectionBlend * (1f - speed01);
        if (!IsGrounded) blend *= _t.Movement.AirControlMultiplier;
        if (_isCharging) blend = 0f;

        Vector3 dir = desiredDir != Vector3.Zero ? travelDir.Lerp(desiredDir, blend) : travelDir;
        dir -= planeNormal * dir.Dot(planeNormal);
        if (dir.LengthSquared() < 1e-6f) return;

        vT += dir.Normalized() * (b.BoostAcceleration * dt);
    }

    private void UpdateBoostMeter(float dt)
    {
        var b = _t.Boost;
        if (_boostActive) _boost = Mathf.Max(0f, _boost - b.BoostDrainRate * dt);
        else _boost = Mathf.Min(b.BoostCapacity, _boost + b.PassiveBoostRegen * dt);
    }

    private void UpdateSpeedBand(float locomotionSpeed)
    {
        var m = _t.Movement;
        SpeedBand band = locomotionSpeed >= m.OverdriveThreshold ? SpeedBand.Overdrive
                       : locomotionSpeed >= m.CrushThreshold ? SpeedBand.Crush
                       : locomotionSpeed >= m.RushThreshold ? SpeedBand.Rush
                       : SpeedBand.Roll;
        if (band == Band) return;
        Band = band;
        SpeedBandChanged?.Invoke(band);
    }

    private void UpdateCheckpoint(PhysicsDirectBodyState3D state, float dt)
    {
        _checkpointTimer -= dt;
        if (_checkpointTimer > 0f) return;
        _checkpointTimer = CheckpointIntervalSeconds;
        if (_rawGrounded && !_slamActive)
            _checkpoint = state.Transform.Origin + Vector3.Up * (_t.Movement.BallRadius + 0.5f);
    }
}

/// <summary>Minimal camera-basis contract consumed by camera-relative steering.</summary>
public interface ICameraBasis
{
    Vector3 FlatForward { get; }
    Vector3 FlatRight { get; }
}
