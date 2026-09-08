using Godot;
using Rushcore.Core;
using Rushcore.Player;

namespace Rushcore.UI;

/// <summary>
/// The persistent player HUD (06 §12): health, boost, Flow, stage progress and currency, built in code like
/// everything else (05 §2). It reads public player and run state and writes nothing. Retro-restrained (06 §17):
/// flat unshaded colours, one font, no gradients, no icons that need explaining, and no raw m/s — speed is the
/// camera's and the VFX's job, and the numbers stay in the F2 telemetry (07 §10).
/// </summary>
public partial class PlayerHud : Control
{
    // Base geometry at scale 1, in pixels. Layout() multiplies by Hud › Scale.
    private const float Margin = 18f, BarWidth = 220f, BarHeight = 14f, FlowWidth = 200f, LabelGap = 4f;
    private const int FontSize = 14;   // 06 §18: nothing smaller than this at scale 1
    /// <summary>Seconds a Flow gain stays lit (06 §7: the escalation must read, and it must not be a number).</summary>
    private const float PulseSeconds = 0.3f;

    private static readonly Color Plate = new(0.04f, 0.05f, 0.07f, 0.55f);
    private static readonly Color Ink = new(0.86f, 0.91f, 0.97f);
    private static readonly Color HealthFill = new(0.90f, 0.35f, 0.34f);
    private static readonly Color BoostFill = new(0.36f, 0.72f, 0.98f);
    private static readonly Color BoostFiring = new(0.72f, 0.93f, 1.00f);
    private static readonly Color FlowFill = new(1.00f, 0.78f, 0.30f);
    private static readonly Color FlowPulse = new(1.00f, 0.96f, 0.72f);
    private static readonly Color FlowDim = new(0.42f, 0.38f, 0.30f);
    private static readonly Color ProgressFill = new(0.55f, 0.86f, 0.92f);
    private static readonly Color[] BandColors =
    {
        new(0.72f, 0.80f, 0.90f),   // Roll
        new(0.45f, 0.90f, 0.70f),   // Rush
        new(1.00f, 0.78f, 0.30f),   // Crush
        new(1.00f, 0.45f, 0.35f),   // Overdrive
    };

    private readonly IDebugActions _debug;
    private readonly PlayerHealth _health;

    private ColorRect _healthBack = null!, _healthFill = null!;
    private ColorRect _boostBack = null!, _boostFill = null!;
    private ColorRect _flowBack = null!, _flowFill = null!;
    private ColorRect _progressBack = null!, _progressFill = null!;
    private Label _stageLabel = null!, _currencyLabel = null!, _bandLabel = null!;
    private Label _healthCaption = null!, _boostCaption = null!, _flowCaption = null!;
    private Font? _font;

    // Cached values: nothing is written to a node, and no string is built, unless what it shows changed.
    private float _shownHealth = -1f, _shownBoost = -1f, _shownFlow = -1f, _shownProgress = -1f;
    private float _shownScale = -1f, _pulse;
    private int _shownStage = -1, _shownStageCount = -1, _shownCurrency = -1;
    private bool _shownBoosting, _shownBandWord;
    private SpeedBand _shownBand = (SpeedBand)(-1);

    public PlayerHud(IDebugActions debug, PlayerHealth health)
    {
        _debug = debug;
        _health = health;
    }

    /// <summary>The Flow gain pulse is lit; the harness reads it rather than looking at a colour.</summary>
    public bool FlowPulseActive => _pulse > 0f;

    // What each bar is actually drawing, as a fraction of its track: the harness measures the rendered
    // widths rather than the values the HUD was handed, so a layout bug cannot pass unnoticed.
    public float HealthShown => Drawn(_healthFill, _healthBack);
    public float BoostShown => Drawn(_boostFill, _boostBack);
    public float FlowShown => Drawn(_flowFill, _flowBack);
    public float ProgressShown => Drawn(_progressFill, _progressBack);
    /// <summary>The stage number the label reads (1-based), and the currency it shows.</summary>
    public int StageShown => _shownStage;
    public int CurrencyShown => _shownCurrency;
    public bool BandWordVisible => _bandLabel.Visible;

    private static float Drawn(ColorRect fill, ColorRect back) => back.Size.X <= 0f ? 0f : fill.Size.X / back.Size.X;

    public override void _Ready()
    {
        Name = "PlayerHud";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;   // the tuning panel pauses the tree; the HUD keeps reading

        _font = new SystemFont { FontNames = new[] { "Menlo", "Monaco", "SF Mono", "Consolas", "DejaVu Sans Mono", "monospace" } };

        (_healthBack, _healthFill) = AddBar(HealthFill);
        (_boostBack, _boostFill) = AddBar(BoostFill);
        (_flowBack, _flowFill) = AddBar(FlowFill);
        (_progressBack, _progressFill) = AddBar(ProgressFill);

        _healthCaption = AddLabel("HEALTH", HorizontalAlignment.Left);
        _boostCaption = AddLabel("BOOST", HorizontalAlignment.Center);
        _flowCaption = AddLabel("FLOW", HorizontalAlignment.Right);
        _stageLabel = AddLabel("STAGE 1 / 9", HorizontalAlignment.Center);
        _currencyLabel = AddLabel("0", HorizontalAlignment.Right);
        _bandLabel = AddLabel("ROLL", HorizontalAlignment.Right);
        _bandLabel.Visible = false;   // off by default (P-005); the toggle turns it on

        Layout(1f);
    }

    private (ColorRect Back, ColorRect Fill) AddBar(Color fill)
    {
        var back = new ColorRect { Color = Plate, MouseFilter = MouseFilterEnum.Ignore };
        var front = new ColorRect { Color = fill, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(back);
        AddChild(front);
        return (back, front);
    }

    private Label AddLabel(string text, HorizontalAlignment align)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = align };
        if (_font is not null) label.AddThemeFontOverride("font", _font);
        label.AddThemeColorOverride("font_color", Ink);
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeColorOverride("font_outline_color", new Color(0.03f, 0.04f, 0.06f, 0.9f));
        AddChild(label);
        return label;
    }

    /// <summary>
    /// Places everything for a HUD scale. Called only when the scale changes, so the layout is not per-frame
    /// work, and no Control transform is involved: sizes and offsets are computed, which keeps the corners
    /// anchored properly at every scale (06 §18 HUD scale).
    /// </summary>
    private void Layout(float scale)
    {
        Vector2 screen = GetViewportRect().Size;
        float m = Margin * scale, bw = BarWidth * scale, bh = BarHeight * scale, fw = FlowWidth * scale;
        int fs = Mathf.Max(12, Mathf.RoundToInt(FontSize * scale));   // never below 12 px, even at scale 0.6
        float gap = LabelGap * scale;

        foreach (var l in new[] { _healthCaption, _boostCaption, _flowCaption, _stageLabel, _currencyLabel, _bandLabel })
            l.AddThemeFontSizeOverride("font_size", fs);
        float lineHeight = fs * 1.4f;

        // Health, bottom-left.
        Place(_healthBack, new Vector2(m, screen.Y - m - bh), new Vector2(bw, bh));
        Place(_healthFill, _healthBack.Position, new Vector2(bw, bh));
        Place(_healthCaption, new Vector2(m, screen.Y - m - bh - lineHeight - gap), new Vector2(bw, lineHeight));

        // Boost, bottom-centre.
        float cx = (screen.X - bw) * 0.5f;
        Place(_boostBack, new Vector2(cx, screen.Y - m - bh), new Vector2(bw, bh));
        Place(_boostFill, _boostBack.Position, new Vector2(bw, bh));
        Place(_boostCaption, new Vector2(cx, screen.Y - m - bh - lineHeight - gap), new Vector2(bw, lineHeight));

        // Flow, bottom-right.
        float fx = screen.X - m - fw;
        Place(_flowBack, new Vector2(fx, screen.Y - m - bh), new Vector2(fw, bh));
        Place(_flowFill, _flowBack.Position, new Vector2(fw, bh));
        Place(_flowCaption, new Vector2(fx, screen.Y - m - bh - lineHeight - gap), new Vector2(fw, lineHeight));
        Place(_bandLabel, new Vector2(fx, screen.Y - m - bh - lineHeight * 2f - gap * 2f), new Vector2(fw, lineHeight));

        // Stage and its progress line, top-centre; currency, top-right.
        float pw = 260f * scale, px = (screen.X - pw) * 0.5f;
        Place(_stageLabel, new Vector2(px, m), new Vector2(pw, lineHeight));
        Place(_progressBack, new Vector2(px, m + lineHeight + gap), new Vector2(pw, Mathf.Max(2f, 3f * scale)));
        Place(_progressFill, _progressBack.Position, new Vector2(pw, Mathf.Max(2f, 3f * scale)));
        Place(_currencyLabel, new Vector2(screen.X - m - 140f * scale, m), new Vector2(140f * scale, lineHeight));

        _shownScale = scale;
        // Every fill is re-sized against the new bar width on the next update.
        _shownHealth = _shownBoost = _shownFlow = _shownProgress = -1f;
    }

    private static void Place(Control control, Vector2 position, Vector2 size)
    {
        control.Position = position;
        control.Size = size;
    }

    public override void _Process(double delta)
    {
        var t = _debug.Tuning.Hud;
        bool visible = t.Visible;
        if (Visible != visible) Visible = visible;
        if (!visible) return;

        float scale = Mathf.Clamp(t.Scale, 0.6f, 1.6f);
        if (!Mathf.IsEqualApprox(scale, _shownScale)) Layout(scale);

        var p = _debug.Player;
        _pulse = Mathf.Max(0f, _pulse - (float)delta);

        SetFill(_healthFill, _healthBack, _health.Fraction, ref _shownHealth);

        SetFill(_boostFill, _boostBack, p.Boost01, ref _shownBoost);
        if (p.BoostActive != _shownBoosting)
        {
            _shownBoosting = p.BoostActive;
            _boostFill.Color = _shownBoosting ? BoostFiring : BoostFill;
        }

        float flow = p.Flow;
        if (flow > _shownFlow && _shownFlow >= 0f) _pulse = PulseSeconds;
        SetFill(_flowFill, _flowBack, flow, ref _shownFlow);
        var wanted = _pulse > 0f ? FlowPulse : flow > 0.001f ? FlowFill : FlowDim;
        if (_flowFill.Color != wanted) _flowFill.Color = wanted;

        var world = _debug.World;
        float progress = world.IsStage && world.Stage is { } st && st.PrimaryRoute.Vertices.Count > 1
            ? world.StageProgressIndex / (float)(st.PrimaryRoute.Vertices.Count - 1)
            : 0f;
        SetFill(_progressFill, _progressBack, progress, ref _shownProgress);

        int stage = _debug.Run.StageIndex + 1, stages = Rushcore.Run.RunDirector.StageCount;
        if (stage != _shownStage || stages != _shownStageCount)
        {
            _shownStage = stage;
            _shownStageCount = stages;
            _stageLabel.Text = $"STAGE {stage} / {stages}";
        }

        int currency = _debug.Run.Currency;
        if (currency != _shownCurrency)
        {
            _shownCurrency = currency;
            _currencyLabel.Text = currency.ToString();
        }

        bool bandWord = t.BandWord;
        if (bandWord != _shownBandWord)
        {
            _shownBandWord = bandWord;
            _bandLabel.Visible = bandWord;
        }
        if (bandWord && p.Band != _shownBand)
        {
            _shownBand = p.Band;
            _bandLabel.Text = _shownBand.ToString().ToUpperInvariant();
            _bandLabel.AddThemeColorOverride("font_color", BandColors[Mathf.Clamp((int)_shownBand, 0, BandColors.Length - 1)]);
        }
    }

    /// <summary>Sets a bar's fill width, and only when the value moved enough to be a different pixel.</summary>
    private static void SetFill(ColorRect fill, ColorRect back, float value01, ref float shown)
    {
        float v = Mathf.Clamp(value01, 0f, 1f);
        if (shown >= 0f && Mathf.Abs(v - shown) * back.Size.X < 0.5f) return;
        shown = v;
        fill.Size = new Vector2(back.Size.X * v, back.Size.Y);
    }
}
