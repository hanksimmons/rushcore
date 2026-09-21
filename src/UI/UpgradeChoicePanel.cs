using Godot;
using Rushcore.Core;
using Rushcore.Run;

namespace Rushcore.UI;

/// <summary>
/// The level-up choice panel (docs/16 §3, D-119; 02 §10's safe moment): opened by the stage outro over the fade while a
/// level-up is queued, it lists the eight stats with their rank pips and what the next rank does in words; one choice
/// per queued level, a stat at rank 3 greyed. Keys 1–8, the mouse, or focus and accept. It writes nothing but the
/// choice, through <see cref="RunDirector.ChooseUpgrade"/>; the outro closes it once nothing is queued.
/// </summary>
public partial class UpgradeChoicePanel : Control
{
    private static readonly Color Ink = new(0.86f, 0.91f, 0.97f);
    private static readonly Color Dim = new(0.50f, 0.54f, 0.60f);
    private static readonly Color Plate = new(0.04f, 0.05f, 0.07f, 0.92f);

    private readonly IDebugActions _debug;
    private Label _title = null!, _subtitle = null!;
    private readonly Button[] _buttons = new Button[UpgradeState.StatCount];
    private PanelContainer _plate = null!;
    private Font? _font;

    public UpgradeChoicePanel(IDebugActions debug) => _debug = debug;

    public bool IsOpen => Visible;
    /// <summary>Where the plate's middle is drawn, in viewport pixels (the harness checks it is the screen's middle).</summary>
    public Vector2 PlateCentre => _plate.Position + _plate.Size * 0.5f;

    public override void _Ready()
    {
        Name = "UpgradeChoicePanel";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        _font = new SystemFont { FontNames = new[] { "Menlo", "Monaco", "SF Mono", "Consolas", "DejaVu Sans Mono", "monospace" } };

        var plate = _plate = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        var style = new StyleBoxFlat { BgColor = Plate, ContentMarginLeft = 22f, ContentMarginRight = 22f, ContentMarginTop = 16f, ContentMarginBottom = 16f };
        style.SetCornerRadiusAll(6);
        plate.AddThemeStyleboxOverride("panel", style);
        // Centred on the viewport by hand (below): a Control under a CanvasLayer has no parent rect to anchor to, and
        // anchoring the plate's centre to it put the plate's middle at the screen's origin.
        plate.CustomMinimumSize = new Vector2(620f, 0f);
        AddChild(plate);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        plate.AddChild(column);

        _title = Text("LEVEL UP", 20, Ink);
        _subtitle = Text("choose an upgrade", 14, Dim);
        column.AddChild(_title);
        column.AddChild(_subtitle);
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 6f) });

        for (int i = 0; i < UpgradeState.StatCount; i++)
        {
            var stat = (UpgradeStat)i;
            var button = new Button { Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0f, 30f) };
            if (_font is not null) button.AddThemeFontOverride("font", _font);
            button.AddThemeFontSizeOverride("font_size", 14);
            button.Pressed += () => Choose(stat);
            column.AddChild(button);
            _buttons[i] = button;
        }
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 4f) });
        column.AddChild(Text("1–8, click, or arrows and Enter", 12, Dim));
    }

    private Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        if (_font is not null) label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public void Open()
    {
        Visible = true;
        Refresh();
        Centre();
        foreach (var b in _buttons) if (!b.Disabled) { b.GrabFocus(); break; }
    }

    public override void _Process(double delta)
    {
        if (Visible) Centre();
    }

    /// <summary>The plate sits in the middle of the viewport whatever its size and the window's.</summary>
    private void Centre()
    {
        Vector2 screen = GetViewportRect().Size;
        Vector2 size = _plate.GetCombinedMinimumSize();
        if (_plate.Size != size) _plate.Size = size;
        _plate.Position = ((screen - size) * 0.5f).Round();
    }

    public void Close() => Visible = false;

    /// <summary>Rebuilds every line from the run: the level, how many choices remain, each stat's pips and next rank.</summary>
    public void Refresh()
    {
        var run = _debug.Run;
        var up = run.Upgrades;
        float burst = _debug.Tuning.JumpSlam.LandingBurstMultiplier;
        _title.Text = $"LEVEL {run.Level}";
        _subtitle.Text = run.PendingLevelUps == 1 ? "choose an upgrade" : $"choose an upgrade  ({run.PendingLevelUps} to choose)";
        for (int i = 0; i < UpgradeState.StatCount; i++)
        {
            var stat = (UpgradeStat)i;
            int rank = up.Rank(stat);
            string pips = new string('●', rank) + new string('○', UpgradeState.MaxRank - rank);
            string next = rank >= UpgradeState.MaxRank ? "max" : up.Describe(stat, rank + 1, burst);
            _buttons[i].Text = $"{i + 1}  {UpgradeState.Label(stat),-18} {pips}   {next}";
            _buttons[i].Disabled = !up.CanRaise(stat) || run.PendingLevelUps <= 0;
        }
    }

    private void Choose(UpgradeStat stat)
    {
        if (!_debug.Run.ChooseUpgrade(stat)) return;
        Refresh();
        foreach (var b in _buttons) if (!b.Disabled) { b.GrabFocus(); break; }
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible || e is not InputEventKey { Pressed: true, Echo: false } key) return;
        int index = key.Keycode switch
        {
            Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3,
            Key.Key5 => 4, Key.Key6 => 5, Key.Key7 => 6, Key.Key8 => 7,
            _ => -1,
        };
        if (index < 0) return;
        Choose((UpgradeStat)index);
        GetViewport().SetInputAsHandled();
    }
}
