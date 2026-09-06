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
    /// <summary>cos(150°): desired input closer than 30° to straight against the heading brakes
    /// instead of turning, so plain W/S can never spin the ball (D-076).</summary>
    private const float BrakeConeDot = -0.866f;
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
    private bool _slamActive;
    // ---- landing burst (D-077): timing only; no combo/rhythm state ----
    private float _sinceSlamLanding = float.PositiveInfinity;   // seconds since the last slam landing
    private float _sinceSlamPress = float.PositiveInfinity;     // seconds since a Space press during a slam
    private bool _burstArmed;                                     // slam landed; burst not yet fired or expired
    private bool _pendingBurst;
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
    /// <summary>True while a fresh Space press would fire the landing burst (D-077).</summary>
    public bool BurstWindowOpen => _burstArmed && _sinceSlamLanding <= _t.JumpSlam.LandingBurstWindowSeconds;
    /// <summary>Seconds left in the post-landing half of the burst window (0 when closed).</summary>
    public float BurstWindowRemaining => BurstWindowOpen ? _t.JumpSlam.LandingBurstWindowSeconds - _sinceSlamLanding : 0f;
    /// <summary>Locomotion speed established by the most recent landing burst.</summary>
    public float LastBurstSpeed { get; private set; }
    public int BurstCount { get; private set; }
    /// <summary>Cap currently enforced: base cap plus any decaying landing allowance.</summary>
    public float EffectiveLocomotionCap => _t.Movement.HardMaxLocomotionSpeed + _capAllowance;
    public bool SlamActive => _slamActive;
    /// <summary>Vertical speed established by the most recent jump release.</summary>
    public float LastTakeoffVerticalSpeed { get; private set; }
    /// <summary>Vertical speed observed at the instant the most recent slam began.</summary>
    public float LastSlamVerticalSpeed { get; private set; }
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
    public event Action? Slammed;
    /// <summary>(impact speed, was a slam). Every slam landing is a power impact (D-077).</summary>
    public event Action<float, bool>? Landed;
    /// <summary>Landing burst fired; argument is the locomotion speed established.</summary>
    public event Action<float>? LandingBurst;
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
        _sinceSlamLanding += dt;
        _sinceSlamPress += dt;
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
        // S / stick-back is a brake, never a reverse drive (D-076): it has no direction of
        // its own, so the travel heading, and therefore the chase camera, cannot flip from it.
        Vector2 driveInput = new(_moveInput.X, Mathf.Max(0f, _moveInput.Y));
        float brake01 = Mathf.Clamp(-_moveInput.Y, 0f, 1f);
        Vector3 desiredDir = ComputeDesiredDirection(planeNormal, curDir, driveInput);
        float inputMag = Mathf.Min(driveInput.Length(), 1f);

        // ---- landing burst (D-077): a speed floor along the current heading, nothing else ----
        // Direction is never rewritten and a faster ball is never slowed, so the burst
        // reads as a seamless surge out of the landing rather than a launch.
        if (_pendingBurst)
        {
            _pendingBurst = false;
            float burst = Mathf.Clamp(_t.JumpSlam.LandingBurstSpeedFraction, 0f, 1f) * cap;
            Vector3 dir = curDir != Vector3.Zero ? curDir : desiredDir;
            if (dir == Vector3.Zero && CameraBasis is not null)
            {
                dir = CameraBasis.FlatForward - planeNormal * CameraBasis.FlatForward.Dot(planeNormal);
                dir = dir.LengthSquared() > 1e-6f ? dir.Normalized() : Vector3.Zero;
            }
            if (dir != Vector3.Zero && speed < burst)
            {
                vT = dir * burst;
                speed = burst;
                speed01 = Mathf.Clamp(speed / cap, 0f, 1f);
                curDir = dir;
            }
            LastBurstSpeed = vT.Length();
            BurstCount++;
            LandingBurst?.Invoke(LastBurstSpeed);
        }

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
                // Input within the brake cone of straight against the heading has no turn
                // side: it only brakes (negative-alignment drive below), it is never a U-turn
                // whose direction float noise would pick. Clear lateral intent (W+A/D) makes
                // the side unambiguous and the normal hairpin applies.
                if (curDir.Dot(desiredDir) > BrakeConeDot && axis.LengthSquared() >= 1e-6f)
                {
                    curDir = curDir.Rotated(axis.Normalized(), Mathf.Min(maxAngle, angle));
                    vT = curDir * speed;
                }
            }
        }

        // ---- longitudinal drive (negative alignment still brakes on sharp turns) ----
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

        // ---- brake: shed speed along the current heading, never through zero ----
        if (brake01 > 0.01f && speed > 0.5f)
        {
            float shed = Mathf.Min(speed, drive * brake01 * dt);
            vT -= curDir * shed;
        }

        ApplyBoost(ref vT, planeNormal, curDir, desiredDir, speed01, dt);

        // ---- drag ----
        float dragK = m.DragCoefficient;
        vT *= Mathf.Max(0f, 1f - dragK * dt);

        // ---- slam commits downward; vertical is never touched by the locomotion cap ----
        if (_slamActive && !IsGrounded)
        {
            vN -= _t.JumpSlam.SlamDownwardAcceleration * dt;
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
        ImpactPowerEstimate = v.Length() * (_slamActive ? _t.JumpSlam.SlamImpactMultiplier : 1f);
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
        _burstArmed = false;
        _pendingBurst = false;
        _sinceSlamLanding = float.PositiveInfinity;
        _sinceSlamPress = float.PositiveInfinity;
        _isCharging = false;
        _chargeSeconds = 0f;
        _chargeGrace = 0f;
        _slamActive = false;
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
        _slamActive = false;

        // Landing burst (D-077): every slam landing opens a short window. A press buffered
        // during the slam counts if it was inside the same window before touchdown.
        _burstArmed = wasSlam;
        _sinceSlamLanding = wasSlam ? 0f : float.PositiveInfinity;
        if (wasSlam && _sinceSlamPress <= _t.JumpSlam.LandingBurstWindowSeconds) FireBurst();
        _sinceSlamPress = float.PositiveInfinity;

        Landed?.Invoke(Mathf.Abs(preVerticalSpeed), wasSlam);
    }

    private void FireBurst()
    {
        _burstArmed = false;
        _pendingBurst = true;
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

        if (_burstArmed && _sinceSlamLanding > js.LandingBurstWindowSeconds) _burstArmed = false;

        if (_jumpPressedEdge)
        {
            if (_burstArmed && _sinceSlamLanding <= js.LandingBurstWindowSeconds)
            {
                // Landing burst (D-077): the press is consumed; it never starts a charge.
                FireBurst();
            }
            else if (_rawGrounded && !_isCharging)
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
            else if (_slamActive && !IsGrounded)
            {
                // Early burst press: remembered so a press just before touchdown still counts.
                _sinceSlamPress = 0f;
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
            _slamActive = false;
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
        LastSlamVerticalSpeed = v.Y;
        _slamActive = true;

        v.X *= js.SlamLateralRetention;
        v.Z *= js.SlamLateralRetention;
        v.Y = Mathf.Min(v.Y, -js.SlamInitialDownwardSpeed);

        Slammed?.Invoke();
    }

    private Vector3 ComputeDesiredDirection(Vector3 planeNormal, Vector3 curDir, Vector2 driveInput)
    {
        // While charging, propulsion follows the existing travel heading instead of
        // letting WASD redirect the ball (03 §4).
        if (_isCharging) return curDir;

        Vector3 fwd = CameraBasis?.FlatForward ?? Vector3.Forward;
        Vector3 right = CameraBasis?.FlatRight ?? Vector3.Right;
        Vector3 desired = right * driveInput.X + fwd * driveInput.Y;
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
