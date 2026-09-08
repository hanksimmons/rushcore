using Godot;
using Rushcore.Camera;
using Rushcore.DebugUi;
using Rushcore.Generation;
using Rushcore.Pickups;
using Rushcore.Player;
using Rushcore.Run;
using Rushcore.Testing;
using Rushcore.UI;
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
    private ColorRect _fade = null!;
    private PlayerVfx _playerVfx = null!;
    private readonly RandomNumberGenerator _burstRng = new();
    private PlayerHud _hud = null!;
    private readonly PlayerHealth _health = new();
    private readonly RunDirector _director = new(DefaultSeed);
    private int _sampleSeen;
    private float _cellSizeSeen, _cellSizeDwell;

    /// <summary>The completion sequence (T1): exit feedback, fade out, rebuild, fade in.</summary>
    private enum OutroPhase { None, Feedback, FadeOut, FadeIn }
    private OutroPhase _outro;
    private float _outroT;
    private int _outroExit = -1;

    public GameplayTuning Tuning => _tuning;
    public PlayerPhysics Player => _player;
    public MovementToyWorld World => _world;
    public RunDirector Run => _director;
    public PlayerHealth Health => _health;
    public PlayerHud Hud => _hud;
    public bool StageOutroActive => _outro != OutroPhase.None;
    /// <summary>Run seed and stage index (T1). The clipboard copies the run seed alone, for `--seed N`.</summary>
    public string SeedText => $"{_director.RunSeed}/{_director.StageIndex}";

    public override void _Ready()
    {
        Name = "GameRoot";
        // The tuning panel can pause the tree while editing; the debug hotkeys and
        // the panel toggle must keep working in that state.
        ProcessMode = ProcessModeEnum.Always;
        GD.Print("[RUSHCORE] Bootstrapping Movement Toy on Godot ", Engine.GetVersionInfo()["string"]);
        if (SeedFromArgs() is { } seed) { _director.StartRun(seed); GD.Print("[RUSHCORE] Seed from command line ", seed); }
        // `-- --stage N` launches on stage N of that run (T1): the same seed chain, a later stage.
        if (IntFromArgs("--stage") is { } stageIndex)
        {
            _director.StartRun(_director.RunSeed, stageIndex);
            GD.Print($"[RUSHCORE] Stage index from command line {_director.StageIndex}");
        }

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
        // `-- --canyon` launches a Canyon Run stage (D-098), over whatever the override file says about the world.
        if (HasFlag("--canyon")) { _tuning.World.GeneratedStage = true; _tuning.World.Archetype = (int)Rushcore.Generation.TerrainArchetype.CanyonRun; GD.Print("[RUSHCORE] Canyon Run from command line"); }
        if (HasFlag("--dunes")) { _tuning.World.GeneratedStage = true; _tuning.World.Archetype = (int)Rushcore.Generation.TerrainArchetype.DuneSea; GD.Print("[RUSHCORE] Dune Sea from command line"); }
        if (HasFlag("--sky")) { _tuning.World.GeneratedStage = true; _tuning.World.Archetype = (int)Rushcore.Generation.TerrainArchetype.SkyTerraces; GD.Print("[RUSHCORE] Sky Terraces from command line"); }

        _world = new MovementToyWorld(_tuning, _director.RunSeed);
        AddChild(_world);
        _world.BoostPickupCollected += amount => _player.RefillBoost(amount);
        _world.StageCompleted += OnStageCompleted;
        // The showcase row's pad throws the coins; the wallet they land in is the run's (T2, T3).
        _world.RewardPadTriggered += at => SpawnRewardBurst(at, 12);

        _player = new PlayerPhysics(_tuning);
        AddChild(_player);
        _player.GlobalPosition = _world.SpawnPoint;
        _player.SetCheckpoint(_world.SpawnPoint);
        _player.AddChild(new PlayerVisual(_tuning, _player));
        _playerVfx = new PlayerVfx(_tuning, _player);
        _player.AddChild(_playerVfx);
        // Health has no damage source yet (T2, P-004): reaching zero recovers to the checkpoint, and the value
        // comes back when the ball arrives rather than in the same instant.
        _health.Died += () => GD.Print("[RUSHCORE] Player down; recovering to the checkpoint.");
        _health.Died += _player.RequestRecovery;
        _player.Recovered += () => { if (_health.IsDead) _health.Refill(); };

        _camera = new CameraRig(_tuning, _player);
        AddChild(_camera);
        _player.CameraBasis = _camera;
        _player.Ground = _world;
        _camera.GroundHeight = _world.SampleHeight;
        _camera.Tubes = () => _world.Tubes;
        _camera.LidOver = _world.LidOver;
        _player.Structure = _world;
        _camera.SnapYawToward(_world.SpawnFacing);
        _camera.SnapToPlayer();

        var ui = new CanvasLayer { Name = "UiRoot", Layer = 10 };
        AddChild(ui);
        _telemetry = new TelemetryOverlay(this);
        ui.AddChild(_telemetry);
        // Stage transition fade (T1): under the tuning panel, over everything else, never eats a click.
        _fade = new ColorRect { Name = "StageFade", Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ui.AddChild(_fade);
        // The player HUD sits under the developer overlays: the panel and the telemetry plate stay legible over it.
        _hud = new PlayerHud(this, _health);
        ui.AddChild(_hud);
        ui.MoveChild(_hud, 0);
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

    /// <summary>`-- --seed N` launches on a named world seed (regression seeds, the G0 manual sample).</summary>
    private static int? SeedFromArgs() => IntFromArgs("--seed");

    private static int? IntFromArgs(string flag)
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == flag && int.TryParse(args[i + 1], out int value)) return value;
        return null;
    }

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

        if (_outro != OutroPhase.None) StepStageOutro((float)delta);

        // World › Sample Stage picks a named archetype and seed (docs/10) and rebuilds; the seed row shows the seed.
        int sample = Mathf.RoundToInt(_tuning.World.SampleStage);
        if (sample != _sampleSeen)
        {
            _sampleSeen = sample;
            if (Rushcore.Generation.SampleStages.At(sample) is { } e)
            {
                _tuning.World.GeneratedStage = true;
                _tuning.World.Archetype = (int)e.Archetype;
                GD.Print($"[RUSHCORE] Sample stage {sample}: {e.Name} ({e.What}), seed {e.Seed}");
                StartRun(e.Seed, 0);
            }
        }

        // World › Calibration Strip and Cell Size rebuild the whole terrain (Gate M1). The
        // toggle applies at once; the slider waits until it has stopped moving. A transition
        // sets the archetype and rebuilds in one step, so the watchdog stands down while it runs.
        if (_outro != OutroPhase.None) { }
        else if (!_world.MatchesTuning()) RestartSameSeed();
        else if (!Mathf.IsEqualApprox(_tuning.World.CellSize, _world.CellSize))
        {
            if (!Mathf.IsEqualApprox(_tuning.World.CellSize, _cellSizeSeen)) { _cellSizeSeen = _tuning.World.CellSize; _cellSizeDwell = 0f; }
            _cellSizeDwell += (float)delta;
            if (_cellSizeDwell > 0.5f) RestartSameSeed();
        }

        // Generated stage: progression anchors replace the toy's rolling auto-checkpoint (04 §13).
        _player.AutoCheckpoint = !_world.IsStage;
        if (_world.IsStage && !GetTree().Paused && _world.UpdateStageProgress(_player.GlobalPosition, (float)delta) is { } anchor)
            _player.SetCheckpoint(anchor);

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
        if (Input.IsActionJustPressed(InputBootstrap.DebugTeleportNearExit)) TeleportNearExit();
        if (Input.IsActionJustPressed(InputBootstrap.DebugKillPlayer)) KillPlayer();
        if (Input.IsActionJustPressed(InputBootstrap.DebugHealPlayer)) HealPlayer();
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
            case 380:
                Capture("rushcore_08_after.png");
                // One frame of the T3 showcase row (06 §8, §9), so the silhouettes can be reviewed from
                // another device. The row is lab-only, so the lab is what gets built whatever the saved
                // override says; the toggle rebuilds the world and the teleport waits for that to land.
                _tuning.World.GeneratedStage = false;
                _tuning.World.CalibrationStrip = false;
                _tuning.World.SampleStage = 0f;
                _tuning.World.EnemyShowcase = true;
                break;
            case 420: TeleportToShowcase(); break;
            case 445: Capture("rushcore_09_showcase.png", checkGroundVisible: true); GetTree().Quit(); break;
        }
    }

    /// <summary>
    /// Parks the ball on the lane opposite the middle of the showcase row and turns it to face across at the row,
    /// which is the angle the row is meant to be read from while driving past it. The telemetry plate stands down
    /// for the frame so it does not cover a third of what the capture exists to show.
    /// </summary>
    private void TeleportToShowcase()
    {
        float midX = WorldDressing.ShowcaseStartX - WorldDressing.ShowcaseSpacing * 5.5f;
        // Far enough back that most of a 286 m row is in frame; the lane itself is only 40 m off the row.
        Vector3 p = _world.SurfacePoint(midX, TerrainHeightField.LaneZ - 70f, _tuning.Movement.BallRadius + 0.4f);
        _player.SetCheckpoint(p);
        _camera.SnapYawToward(Vector3.Back);        // +Z: the row stands 40 m that way
        _player.TeleportTo(p);
        _camera.SnapToPlayer();
        _telemetry.Visible = false;
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

    // ---------------- stage completion (T1) ----------------

    /// <summary>
    /// The ball reached an exit pad (02 §4, D-105). Controls lock at once and the outro runs: exit
    /// feedback while the ball rolls free, a fade, the next stage of the same run, a fade back in.
    /// With <see cref="RunDirector.AutoAdvance"/> off the completion is still raised and printed but
    /// nothing is built, so one stage can be driven again and again.
    /// </summary>
    private void OnStageCompleted(int exitIndex)
    {
        if (!_director.AutoAdvance || _outro != OutroPhase.None) return;
        _outroExit = exitIndex;
        _outro = OutroPhase.Feedback;
        _outroT = 0f;
        _player.ControlsLocked = true;
    }

    private void StepStageOutro(float dt)
    {
        var r = _tuning.Run;
        _outroT += dt;
        switch (_outro)
        {
            case OutroPhase.Feedback:
                if (_outroT < r.OutroSeconds) break;
                _outro = OutroPhase.FadeOut;
                _outroT = 0f;
                break;

            case OutroPhase.FadeOut:
                SetFade(r.FadeOutSeconds <= 0f ? 1f : _outroT / r.FadeOutSeconds);
                if (_outroT < r.FadeOutSeconds) break;
                SetFade(1f);
                BuildNextStage();
                _outro = OutroPhase.FadeIn;
                _outroT = 0f;
                break;

            case OutroPhase.FadeIn:
                SetFade(r.FadeInSeconds <= 0f ? 0f : 1f - _outroT / r.FadeInSeconds);
                if (_outroT < r.FadeInSeconds) break;
                SetFade(0f);
                _outro = OutroPhase.None;
                _player.ControlsLocked = false;
                break;
        }
    }

    private void SetFade(float alpha) => _fade.Color = _fade.Color with { A = Mathf.Clamp(alpha, 0f, 1f) };

    /// <summary>
    /// The next stage of the same run: same run seed, index + 1, and the archetype the exit taken picks
    /// (P-011). Flow and boost carry (P-010): the transition teleport keeps the chain. Synchronous, as the
    /// rebuild has always been; the wall time is printed so the budget stays visible (08 §10).
    /// </summary>
    private void BuildNextStage()
    {
        ulong stageSeed = _world.Stage?.Request.StageSeed ?? SeedChain.Derive(_director.RunSeed, "stage", _director.StageIndex);
        var next = RunDirector.NextArchetype(stageSeed, _world.Archetype, _outroExit);
        string label = _world.StageExitLabel;
        float clock = _world.StageExitTime, model = _world.Stage?.SpeedProfile.TotalTime ?? 0f;
        int from = _director.StageIndex;

        ulong start = Time.GetTicksMsec();
        bool wrapped = _director.Advance(_outroExit);
        _tuning.World.Archetype = (int)next;
        _world.Regenerate(_director.Request(next));
        TeleportToStart(keepChain: true);
        ulong millis = Time.GetTicksMsec() - start;

        GD.Print($"[RUSHCORE] Stage {_director.RunSeed}/{from} complete by exit {label} in {clock:0.0} s (model {model:0.0} s); " +
                 $"stage {_director.RunSeed}/{_director.StageIndex} {ArchetypeRules.Label(next)}, next build {millis} ms");
        if (wrapped) GD.Print("[RUSHCORE] run complete (placeholder)");
    }

    // ---------------- IDebugActions ----------------
    public void RestartSameSeed()
    {
        _world.Regenerate(_director.Request(_world.WantedArchetype));
        TeleportToStart();
    }

    public void RestartNewSeed()
    {
        StartRun((int)(Time.GetTicksUsec() & 0x7FFFFFFF), 0);
        GD.Print("[RUSHCORE] New world seed ", _director.RunSeed);
    }

    public void StartRun(int runSeed, int stageIndex)
    {
        CancelOutro();
        _director.StartRun(runSeed, stageIndex);
        _world.Regenerate(_director.Request(_world.WantedArchetype));
        TeleportToStart();
    }

    /// <summary>A manual restart never leaves a half-played outro behind: the fade clears and controls return.</summary>
    private void CancelOutro()
    {
        if (_outro == OutroPhase.None) return;
        _outro = OutroPhase.None;
        _outroT = 0f;
        SetFade(0f);
        _player.ControlsLocked = false;
    }

    public void RecoverPlayer() => _player.RequestRecovery();

    public void TeleportToStart() => TeleportToStart(keepChain: false);

    private void TeleportToStart(bool keepChain)
    {
        _world.ResetStageProgress();
        _player.SetCheckpoint(_world.SpawnPoint);
        _camera.SnapYawToward(_world.SpawnFacing);   // face down the lane, not the old heading
        _player.TeleportTo(_world.SpawnPoint, keepChain);
    }

    /// <summary>
    /// Debug (07 §12): drops the ball on the primary 200 m short of the exit pad, facing down the route,
    /// so the completion sequence can be reached without driving the whole stage. Progress and the armed
    /// anchor move with it; the stage clock is left alone.
    /// </summary>
    public void TeleportNearExit()
    {
        if (_world.Stage is not { } stage) { TeleportToStart(); return; }
        var route = stage.PrimaryRoute;
        int i = route.IndexAtDistance(Mathf.Max(0f, route.Length - 200f));
        var v = route.Vertices[Mathf.Clamp(i, 0, route.Vertices.Count - 1)];
        Vector3 p = _world.SurfacePoint(v.Position.X, v.Position.Z, _tuning.Movement.BallRadius + 0.6f);
        _world.SkipStageProgressTo(i);
        _player.SetCheckpoint(p);
        _camera.SnapYawToward(new Vector3(Mathf.Cos(v.Heading), 0f, Mathf.Sin(v.Heading)));
        _player.TeleportTo(p);
        GD.Print($"[RUSHCORE] Teleported to {v.Distance:0} m of {route.Length:0} m on the primary ({route.Length - v.Distance:0} m short of exit A)");
    }

    public void RefillBoost() => _player.RefillBoost(_tuning.Boost.BoostCapacity);

    public void KillPlayer() => _health.Kill();

    public void HealPlayer() => _health.Refill();

    // ---------------- T3: the combat one-shots and the reward burst ----------------

    public void PlayCrush()
    {
        if (_world.Showcase is { } row) row.Pylon.PlayCrush();
        else _world.Vfx.Play(Rushcore.Vfx.WorldVfxKind.Crush, _player.GlobalPosition);
    }

    public void PlayFailedImpact()
    {
        if (_world.Showcase is { } row) row.Bulwark.PlayFail();
        else _world.Vfx.Play(Rushcore.Vfx.WorldVfxKind.FailedImpact, _player.GlobalPosition);
    }

    public void PlayDamage() => _playerVfx.PlayDamage();

    public void BurstCoins() => SpawnRewardBurst(_player.GlobalPosition + Vector3.Up * 2f, 12);

    /// <summary>
    /// Throws a reward burst and points its collected coins at the run's wallet (P-007). The burst is kinematic
    /// and frees itself; nothing here can touch the ball's velocity, which is the whole reason auto-collect
    /// exists (02 §12).
    /// </summary>
    private void SpawnRewardBurst(Vector3 origin, int coins)
    {
        var burst = RewardBurst.Spawn(_world, origin, PickupKind.Currency, coins, _burstRng,
                                      () => _player.GlobalPosition, _world.Vfx);
        burst.Collected += (_, amount) => _director.AddCurrency(amount);
    }

    public void CopySeedToClipboard() => DisplayServer.ClipboardSet(_director.RunSeed.ToString());
}
