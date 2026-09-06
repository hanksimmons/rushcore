using Godot;
using Rushcore.Camera;
using Rushcore.DebugUi;
using Rushcore.Player;
using Rushcore.Testing;
using Rushcore.Tuning;
using Rushcore.World;

namespace Rushcore.Core;

/// <summary>
/// Sole hand-authored runtime entry point and composition root. Everything below
/// this node is constructed programmatically (05 §2, D-064).
/// </summary>
public partial class GameBootstrap : Node3D, IDebugActions
{
    private const int DefaultSeed = 20260905;

    private GameplayTuning _tuning = null!;
    private MovementToyWorld _world = null!;
    private PlayerPhysics _player = null!;
    private CameraRig _camera = null!;
    private TuningPanel _tuningPanel = null!;
    private TelemetryOverlay _telemetry = null!;
    private int _seed = DefaultSeed;
    private float _cellSizeSeen, _cellSizeDwell;

    public GameplayTuning Tuning => _tuning;
    public PlayerPhysics Player => _player;
    public MovementToyWorld World => _world;
    public string SeedText => _seed.ToString();

    public override void _Ready()
    {
        Name = "GameRoot";
        // The tuning panel can pause the tree while editing; the debug hotkeys and
        // the panel toggle must keep working in that state.
        ProcessMode = ProcessModeEnum.Always;
        GD.Print("[RUSHCORE] Bootstrapping Movement Toy on Godot ", Engine.GetVersionInfo()["string"]);

        InputBootstrap.Register();

        _tuning = new GameplayTuning();
        GameplayTuning.InstallBundledPresets();
        // Compiled defaults remain the clean-build authority (07 §8); the saved override is
        // applied on launch so feel work persists between sessions, and the panel/telemetry
        // show how many values differ so the baseline is never drifted from unknowingly.
        // The self-test always runs on compiled defaults.
        if (!WantsSelfTest())
        {
            int applied = _tuning.LoadOverride();
            if (applied < 0) GD.Print("[RUSHCORE] No tuning override; running compiled defaults.");
        }

        _world = new MovementToyWorld(_tuning, _seed);
        AddChild(_world);
        _world.BoostPickupCollected += amount => _player.RefillBoost(amount);

        _player = new PlayerPhysics(_tuning);
        AddChild(_player);
        _player.GlobalPosition = _world.SpawnPoint;
        _player.SetCheckpoint(_world.SpawnPoint);
        _player.AddChild(new PlayerVisual(_tuning, _player));
        _player.AddChild(new PlayerVfx(_tuning, _player));

        _camera = new CameraRig(_tuning, _player);
        AddChild(_camera);
        _player.CameraBasis = _camera;
        _camera.GroundHeight = _world.SampleHeight;
        _camera.SnapYawToward(_world.SpawnFacing);
        _camera.SnapToPlayer();

        var ui = new CanvasLayer { Name = "UiRoot", Layer = 10 };
        AddChild(ui);
        _telemetry = new TelemetryOverlay(this);
        ui.AddChild(_telemetry);
        _tuningPanel = new TuningPanel(this) { Visible = false };
        ui.AddChild(_tuningPanel);

        GD.Print($"[RUSHCORE] Ready. Spawn {_world.SpawnPoint}. F1 tuning, F2 telemetry, R recover.");

        if (HasFlag(ScreenshotFlag))
        {
            _screenshotFrame = 1;
        }

        if (WantsSelfTest())
        {
            var selfTest = new MovementToySelfTest(this);
            AddChild(selfTest);
            MoveChild(selfTest, 0);   // must inject input before the player samples it
        }
    }

    private static bool WantsSelfTest() => HasFlag("--rushcore-selftest");

    private const string ScreenshotFlag = "--rushcore-screenshot";
    private int _screenshotFrame;

    private static bool HasFlag(string flag)
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a == flag) return true;
        foreach (var a in OS.GetCmdlineArgs()) if (a == flag) return true;
        return false;
    }

    public override void _Process(double delta)
    {
        if (!InputBootstrap.IsTextEntryFocused(GetViewport())) HandleDebugHotkeys();

        if (_screenshotFrame > 0) StepScreenshotCapture();

        // World › Calibration Strip and Cell Size rebuild the whole terrain (Gate M1). The
        // toggle applies at once; the slider waits until it has stopped moving.
        if (_tuning.World.CalibrationStrip != _world.IsStrip) RestartSameSeed();
        else if (!Mathf.IsEqualApprox(_tuning.World.CellSize, _world.CellSize))
        {
            if (!Mathf.IsEqualApprox(_tuning.World.CellSize, _cellSizeSeen)) { _cellSizeSeen = _tuning.World.CellSize; _cellSizeDwell = 0f; }
            _cellSizeDwell += (float)delta;
            if (_cellSizeDwell > 0.5f) RestartSameSeed();
        }

        // Fall recovery: the toy must be hard to permanently break.
        if (!GetTree().Paused && _player.GlobalPosition.Y < _world.KillPlaneY) RecoverPlayer();
    }

    private void HandleDebugHotkeys()
    {
        if (Input.IsActionJustPressed(InputBootstrap.ToggleTuning)) _tuningPanel.Visible = !_tuningPanel.Visible;
        if (Input.IsActionJustPressed(InputBootstrap.ToggleTelemetry)) _telemetry.Visible = !_telemetry.Visible;
        if (Input.IsActionJustPressed(InputBootstrap.Recover)) RecoverPlayer();
        if (Input.IsActionJustPressed(InputBootstrap.DebugRefillBoost)) RefillBoost();
        if (Input.IsActionJustPressed(InputBootstrap.DebugRegenerateWorld)) RestartNewSeed();
        if (Input.IsActionJustPressed(InputBootstrap.DebugTeleportStart)) TeleportToStart();
        if (Input.IsActionJustPressed(InputBootstrap.DebugTogglePhysicsHz))
        {
            // V-007: 60 Hz is the baseline; 120 is only to be tried if high-speed
            // behaviour demands it. This lets that be judged live without a rebuild.
            Engine.PhysicsTicksPerSecond = Engine.PhysicsTicksPerSecond == 60 ? 120 : 60;
            GD.Print("[RUSHCORE] Physics tick rate now ", Engine.PhysicsTicksPerSecond, " Hz");
        }
    }

    /// <summary>
    /// Verification aid: drives the ball with synthetic input through drive, charge,
    /// release and slam, saving a PNG at each state so presentation cues can be seen
    /// rather than assumed. Exits when done. Not a feel judgement.
    /// </summary>
    private void StepScreenshotCapture()
    {
        _screenshotFrame++;
        switch (_screenshotFrame)
        {
            case 60: Capture("rushcore_01_spawn.png"); break;
            case 62:
                _tuning.Movement.DragCoefficient += 0.02f;      // show the override state in the capture
                _tuningPanel.Visible = true;
                break;
            case 75:
                Capture("rushcore_02_panel.png");
                _tuningPanel.Visible = false;
                _tuning.Movement.DragCoefficient -= 0.02f;
                break;
            case 80: Input.ActionPress(InputBootstrap.MoveForward, 1f); break;   // straight down the lane
            case 200: Capture("rushcore_03_rolling.png", checkGroundVisible: true); break;
            case 202: Input.ActionPress(InputBootstrap.Boost, 1f); break;
            case 260: Capture("rushcore_04_boost.png"); Input.ActionRelease(InputBootstrap.Boost); break;
            case 270: Input.ActionPress(InputBootstrap.Jump, 1f); break;
            case 305: Capture("rushcore_05_charge.png"); break;
            case 306: Input.ActionRelease(InputBootstrap.Jump); break;
            case 312: Capture("rushcore_06_release.png"); break;
            case 330: Input.ActionPress(InputBootstrap.Jump, 1f); break;
            case 336: Capture("rushcore_07_slam.png"); Input.ActionRelease(InputBootstrap.Jump); break;
            case 380: Capture("rushcore_08_after.png"); GetTree().Quit(); break;
        }
    }

    private void Capture(string fileName, bool checkGroundVisible = false)
    {
        Image image = GetViewport().GetTexture().GetImage();
        string path = "user://" + fileName;
        Error err = image.SavePng(path);
        GD.Print($"[RUSHCORE] screenshot {ProjectSettings.GlobalizePath(path)} -> {err}");
        if (checkGroundVisible) CheckGroundVisible(image);
    }

    /// <summary>
    /// Render guard: the flat-shaded terrain under the player must show per-facet
    /// variation. A perfectly uniform patch means the sky is showing through, i.e. the
    /// terrain was culled or missing. Caught a winding-order bug once; keep it.
    /// </summary>
    private static void CheckGroundVisible(Image image)
    {
        int w = image.GetWidth(), h = image.GetHeight();
        int x0 = (int)(w * 0.35f), x1 = (int)(w * 0.65f);
        int y0 = (int)(h * 0.62f), y1 = (int)(h * 0.95f);
        double sum = 0, sumSq = 0;
        int n = 0;
        for (int y = y0; y < y1; y += 6)
        {
            for (int x = x0; x < x1; x += 6)
            {
                float l = image.GetPixel(x, y).Luminance;
                sum += l;
                sumSq += l * l;
                n++;
            }
        }
        double mean = sum / n;
        double std = Math.Sqrt(Math.Max(0, sumSq / n - mean * mean));
        bool ok = std > 0.004;
        GD.Print($"[RUSHCORE] render check: ground-under-player luminance std={std:0.0000} -> {(ok ? "PASS" : "FAIL (uniform: terrain not rendered?)")}");
    }

    // ---------------- IDebugActions ----------------
    public void RestartSameSeed()
    {
        _world.Regenerate(_seed);
        TeleportToStart();
    }

    public void RestartNewSeed()
    {
        _seed = (int)(Time.GetTicksUsec() & 0x7FFFFFFF);
        _world.Regenerate(_seed);
        TeleportToStart();
        GD.Print("[RUSHCORE] New world seed ", _seed);
    }

    public void RecoverPlayer() => _player.RequestRecovery();

    public void TeleportToStart()
    {
        _player.SetCheckpoint(_world.SpawnPoint);
        _camera.SnapYawToward(_world.SpawnFacing);   // face down the lane, not the old heading
        _player.TeleportTo(_world.SpawnPoint);
    }

    public void RefillBoost() => _player.RefillBoost(_tuning.Boost.BoostCapacity);

    public void CopySeedToClipboard() => DisplayServer.ClipboardSet(SeedText);
}
