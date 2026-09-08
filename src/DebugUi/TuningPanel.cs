using System.Globalization;
using Godot;
using Rushcore.Core;
using Rushcore.Tuning;

namespace Rushcore.DebugUi;

/// <summary>
/// Runtime tuning panel (07). Edits the single <see cref="GameplayTuning"/> instance
/// through its explicit parameter descriptors; it must not redefine the schema.
///
/// Layout: a pass-through full-rect root with one opaque column docked to the right
/// edge, so the play area stays visible and drivable while tuning (07 §2, §14).
/// </summary>
public partial class TuningPanel : Control
{
    private const float PanelWidth = 540f;
    private const float NameWidth = 150f;
    private const float FieldWidth = 78f;

    /// <summary>Keys the player drives with; a focused text field must never eat these.</summary>
    private static readonly Key[] DriveKeys = { Key.W, Key.A, Key.S, Key.D, Key.Space };

    private static readonly Color TextDim = new(0.62f, 0.68f, 0.78f);
    private static readonly Color TextAccent = new(1f, 0.78f, 0.35f);
    private static readonly Color TextModified = new(1f, 0.62f, 0.30f);

    private sealed class ParamRow
    {
        public required TuningParameter Param { get; init; }
        public required Label Name { get; init; }
        public required HSlider Slider { get; init; }
        public required LineEdit Field { get; init; }
        public required string Format { get; init; }
        public bool Suppress;

        public string Render(float v) => v.ToString(Format, CultureInfo.InvariantCulture);

        public void Refresh()
        {
            var v = Mathf.Clamp(Param.Get(), Param.Min, Param.Max);
            Suppress = true;
            Slider.Value = v;
            Field.Text = Render(v);
            Suppress = false;
            Tint();
        }

        /// <summary>Modified values are highlighted so drift from the baseline is always visible.</summary>
        public void Tint() =>
            Name.AddThemeColorOverride("font_color", GameplayTuning.IsModified(Param) ? TextModified : TextDim);
    }

    private sealed class ToggleRow
    {
        public required TuningToggle Toggle { get; init; }
        public required CheckBox Box { get; init; }
        public bool Suppress;

        public void Refresh()
        {
            var v = Toggle.Get();
            if (Box.ButtonPressed != v)
            {
                Suppress = true;
                Box.ButtonPressed = v;
                Suppress = false;
            }
            Tint();
        }

        public void Tint() =>
            Box.AddThemeColorOverride("font_color", GameplayTuning.IsModified(Toggle) ? TextModified : TextDim);
    }

    private readonly IDebugActions _debug;
    private readonly List<ParamRow> _paramRows = new();
    private readonly List<ToggleRow> _toggleRows = new();

    private Label _seedLabel = null!;
    private LineEdit _presetName = null!;
    private OptionButton _presetList = null!;
    private bool _pausedForTyping;
    private Label _overrideLabel = null!;
    private int _overrideShown = -1;
    private Label _statusLabel = null!;
    private CheckBox _pauseBox = null!;
    private string _seedShown = string.Empty;
    private double _statusTimer;
    private bool _pausedByPanel;

    public TuningPanel(IDebugActions debug) => _debug = debug;

    protected IDebugActions Debug => _debug;
    protected GameplayTuning Tuning => _debug.Tuning;

    public override void _Ready()
    {
        Name = "TuningPanel";
        // SetAnchorsPreset (keepOffsets=false) rewrites the offsets so the control's
        // CURRENT rect is preserved. On a freshly-constructed Control that rect is
        // (0,0), so FullRect collapses to a zero-size root and every anchored child
        // lands off-screen. The "AndOffsets" variant resets offsets to 0 instead,
        // which is what actually fills the viewport.
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;      // root never blocks the game
        ProcessMode = ProcessModeEnum.Always;      // stays usable when it pauses the tree

        BuildUi();
        RefreshAll();

        Tuning.BulkChanged += RefreshAll;
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _ExitTree()
    {
        Tuning.BulkChanged -= RefreshAll;
        VisibilityChanged -= OnVisibilityChanged;
        if (_pausedByPanel && GetTree() is { } tree) tree.Paused = false;
        _pausedByPanel = false;
    }

    // ---------------- construction ----------------

    private void BuildUi()
    {
        var column = new PanelContainer
        {
            Name = "PanelColumn",
            MouseFilter = MouseFilterEnum.Stop,    // the column itself is opaque to the mouse
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 1f,
            OffsetLeft = -PanelWidth,
            OffsetRight = 0f,
            OffsetTop = 0f,
            OffsetBottom = 0f,
        };
        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.065f, 0.085f, 0.94f),
            BorderColor = new Color(0.30f, 0.62f, 0.85f, 0.75f),
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        bg.BorderWidthLeft = 2;
        column.AddThemeStyleboxOverride("panel", bg);
        AddChild(column);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);
        column.AddChild(root);

        root.AddChild(Header("RUSHCORE — RUNTIME TUNING   (F1)", 15, TextAccent));

        _seedLabel = new Label { Text = "seed —" };
        _seedLabel.AddThemeColorOverride("font_color", TextDim);
        root.AddChild(_seedLabel);

        _overrideLabel = new Label { Text = "compiled defaults" };
        _overrideLabel.AddThemeColorOverride("font_color", TextDim);
        root.AddChild(_overrideLabel);

        _pauseBox = new CheckBox
        {
            Text = "Pause game while editing",
            FocusMode = FocusModeEnum.None,
            TooltipText = "Pauses the scene tree. F1 cannot resume while paused — uncheck here.",
        };
        _pauseBox.Toggled += OnPauseToggled;
        root.AddChild(_pauseBox);

        root.AddChild(new HSeparator());
        root.AddChild(ButtonGrid(new (string, Action)[]
        {
            ("Reset All", Tuning.ResetAll),
            ("Save Override", () => Status(Tuning.SaveOverride() ? "Saved override; it will load on next launch." : "Save failed.")),
            ("Load Override", () => { int n = Tuning.LoadOverride(); Status(n < 0 ? "No override file." : $"Loaded override ({n} values)."); }),
        }));

        root.AddChild(Header("PRESETS  (stash and compare variations)", 12, TextDim));
        var saveRow = new HBoxContainer();
        saveRow.AddThemeConstantOverride("separation", 6);
        _presetName = new LineEdit
        {
            PlaceholderText = "preset name",
            FocusMode = FocusModeEnum.Click,
            ContextMenuEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Letters, digits, - and _. Enter or Save As writes the current values as a named preset.",
        };
        _presetName.TextSubmitted += _ => SavePresetFromField();
        _presetName.FocusEntered += OnTextEntryFocus;
        _presetName.FocusExited += OnTextEntryBlur;
        saveRow.AddChild(_presetName);
        saveRow.AddChild(MakeButton("Save As", SavePresetFromField));
        root.AddChild(saveRow);

        var loadRow = new HBoxContainer();
        loadRow.AddThemeConstantOverride("separation", 6);
        _presetList = new OptionButton
        {
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Load applies the preset live. Save Override afterwards to make it the startup state.",
        };
        loadRow.AddChild(_presetList);
        loadRow.AddChild(MakeButton("Load", LoadSelectedPreset));
        loadRow.AddChild(MakeButton("Delete", DeleteSelectedPreset));
        root.AddChild(loadRow);
        RefreshPresetList(null);

        root.AddChild(Header("DEBUG ACTIONS", 12, TextDim));
        root.AddChild(ButtonGrid(new (string, Action)[]
        {
            ("Restart Same Seed", () => { _debug.RestartSameSeed(); Status("Regenerated same seed."); }),
            ("New Seed", () => { _debug.RestartNewSeed(); Status("Regenerated new seed."); }),
            ("Copy Seed", () => { _debug.CopySeedToClipboard(); Status("Seed copied."); }),
            ("Recover Player", () => { _debug.RecoverPlayer(); Status("Recovered to checkpoint."); }),
            ("Teleport To Start", () => { _debug.TeleportToStart(); Status("Teleported to start."); }),
            ("Teleport Near Exit", () => { _debug.TeleportNearExit(); Status("Teleported 200 m short of the exit."); }),
            ("Refill Boost", () => { _debug.RefillBoost(); Status("Boost refilled."); }),
            ("Kill Player", () => { _debug.KillPlayer(); Status("Player down; recovering."); }),
            ("Heal Player", () => { _debug.HealPlayer(); Status("Health full."); }),
            ("Play Crush", () => { _debug.PlayCrush(); Status("Crush one-shot."); }),
            ("Play Fail", () => { _debug.PlayFailedImpact(); Status("Failed-impact one-shot."); }),
            ("Play Damage", () => { _debug.PlayDamage(); Status("Damage pulse."); }),
            ("Burst 12 Coins", () => { _debug.BurstCoins(); Status("Twelve coins thrown."); }),
        }));

        _statusLabel = new Label { Text = string.Empty };
        _statusLabel.AddThemeColorOverride("font_color", TextAccent);
        root.AddChild(_statusLabel);

        root.AddChild(new HSeparator());

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipTabs = true,
        };
        root.AddChild(tabs);

        for (var i = 0; i < GameplayTuning.Categories.Length; i++)
        {
            var category = GameplayTuning.Categories[i];
            tabs.AddChild(BuildCategory(category, i));
            tabs.SetTabTitle(i, ShortTabTitle(category));
        }
    }

    private ScrollContainer BuildCategory(string category, int index)
    {
        var scroll = new ScrollContainer
        {
            Name = $"Cat{index}",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(list);

        var reset = MakeButton($"Reset {category}", () => Tuning.ResetCategory(category));
        reset.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        list.AddChild(reset);

        foreach (var toggle in Tuning.Toggles)
            if (toggle.Category == category)
                list.AddChild(BuildToggleRow(toggle));

        foreach (var param in Tuning.Parameters)
            if (param.Category == category)
                list.AddChild(BuildParamRow(param));

        return scroll;
    }

    private Control BuildParamRow(TuningParameter param)
    {
        var range = Mathf.Max(0.0001f, param.Max - param.Min);
        var (step, format) = range <= 2f ? (0.001f, "0.####")
            : range <= 20f ? (0.005f, "0.###")
            : (0.01f, "0.##");

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);

        var name = new Label
        {
            Text = param.Name,
            CustomMinimumSize = new Vector2(NameWidth, 0),
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            TooltipText = $"{param.Key}\nrange {param.Min}..{param.Max}\ndefault {param.DefaultValue}",
            MouseFilter = MouseFilterEnum.Stop,     // so the tooltip works
        };
        name.AddThemeColorOverride("font_color", TextDim);
        row.AddChild(name);

        var slider = new HSlider
        {
            MinValue = param.Min,
            MaxValue = param.Max,
            Step = step,
            FocusMode = FocusModeEnum.None,         // never steals arrow keys from the game
            CustomMinimumSize = new Vector2(120f, 18f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        row.AddChild(slider);

        var field = new LineEdit
        {
            CustomMinimumSize = new Vector2(FieldWidth, 0),
            Alignment = HorizontalAlignment.Right,
            FocusMode = FocusModeEnum.Click,        // only a deliberate click can focus it
            SelectAllOnFocus = true,
            ContextMenuEnabled = false,
            TooltipText = "Type an exact value, press Enter.",
        };
        row.AddChild(field);

        var entry = new ParamRow { Param = param, Name = name, Slider = slider, Field = field, Format = format };
        _paramRows.Add(entry);
        entry.Tint();

        slider.ValueChanged += value =>
        {
            if (entry.Suppress) return;
            var v = (float)value;
            param.Set(v);                            // applied immediately, same frame
            field.Text = entry.Render(v);
            entry.Tint();
        };

        field.TextSubmitted += text =>
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                param.Set(Mathf.Clamp(parsed, param.Min, param.Max));
            entry.Refresh();
            field.ReleaseFocus();                    // hand the keyboard straight back to the game
        };
        field.FocusExited += entry.Refresh;

        return row;
    }

    private Control BuildToggleRow(TuningToggle toggle)
    {
        var box = new CheckBox
        {
            Text = toggle.Name,
            FocusMode = FocusModeEnum.None,          // Space must not re-trigger it
            TooltipText = $"{toggle.Key}\ndefault {toggle.DefaultValue}",
        };
        var entry = new ToggleRow { Toggle = toggle, Box = box };
        _toggleRows.Add(entry);

        box.Toggled += pressed =>
        {
            if (entry.Suppress) return;
            toggle.Set(pressed);
        };
        return box;
    }

    private static Label Header(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = FocusModeEnum.None,          // buttons must not swallow Space
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        button.Pressed += action;
        return button;
    }

    private static GridContainer ButtonGrid((string Text, Action Action)[] items)
    {
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var (text, action) in items) grid.AddChild(MakeButton(text, action));
        return grid;
    }

    // ---------------- live behavior ----------------

    public override void _Process(double delta)
    {
        if (!Visible) return;

        // Cheap resync of the handful of toggles in case one was flipped outside the panel.
        foreach (var entry in _toggleRows) entry.Refresh();

        if (_debug.SeedText != _seedShown)
        {
            _seedShown = _debug.SeedText;
            _seedLabel.Text = $"seed {_seedShown}";
        }
        int overrides = Tuning.OverrideCount;
        if (overrides != _overrideShown)
        {
            _overrideShown = overrides;
            _overrideLabel.Text = overrides == 0
                ? "compiled defaults"
                : $"OVERRIDE ACTIVE — {overrides} {(overrides == 1 ? "value differs" : "values differ")} from compiled defaults";
            _overrideLabel.AddThemeColorOverride("font_color", overrides == 0 ? TextDim : TextModified);
        }

        if (_statusTimer > 0d)
        {
            _statusTimer -= delta;
            if (_statusTimer <= 0d) _statusLabel.Text = string.Empty;
        }
    }

    /// <summary>
    /// A focused value field must never hold on to the drive keys. Runs before GUI
    /// input, so releasing focus here also stops the character being typed.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (Array.IndexOf(DriveKeys, key.PhysicalKeycode) < 0) return;
        if (GetViewport()?.GuiGetFocusOwner() == _presetName) return;   // typing a name
        ReleaseOwnedFocus();
    }

    /// <summary>All seven categories must fit the strip without scroll arrows.</summary>
    private static string ShortTabTitle(string category) => category switch
    {
        GameplayTuning.CatMovement => "Move",
        GameplayTuning.CatJumpSlam => "Jump",
        GameplayTuning.CatBoost => "Boost",
        GameplayTuning.CatCamera => "Camera",
        GameplayTuning.CatVfx => "VFX",
        GameplayTuning.CatWorld => "World",
        _ => category,
    };

    // ---------------- presets ----------------

    private void RefreshPresetList(string? select)
    {
        _presetList.Clear();
        var names = GameplayTuning.ListPresets();
        if (names.Count == 0)
        {
            _presetList.AddItem("(no presets saved)");
            _presetList.Disabled = true;
            return;
        }
        _presetList.Disabled = false;
        for (int i = 0; i < names.Count; i++)
        {
            _presetList.AddItem(names[i]);
            if (names[i] == select) _presetList.Select(i);
        }
    }

    private string? SelectedPreset()
    {
        if (_presetList.Disabled || _presetList.Selected < 0) return null;
        return _presetList.GetItemText(_presetList.Selected);
    }

    private void SavePresetFromField()
    {
        string name = GameplayTuning.SanitizePresetName(_presetName.Text);
        _presetName.ReleaseFocus();
        if (name.Length == 0)
        {
            Status("Enter a preset name first.");
            return;
        }
        bool existed = GameplayTuning.PresetExists(name);
        if (!Tuning.SavePreset(name))
        {
            Status($"Could not save preset '{name}'.");
            return;
        }
        _presetName.Text = name;
        RefreshPresetList(name);
        Status($"{(existed ? "Overwrote" : "Saved")} preset '{name}' ({Tuning.OverrideCount} values differ from defaults).");
    }

    private void LoadSelectedPreset()
    {
        if (SelectedPreset() is not { } name) { Status("No preset selected."); return; }
        int n = Tuning.LoadPreset(name);
        Status(n < 0 ? $"Preset '{name}' is missing." : $"Loaded '{name}' ({n} values). Save Override to make it the startup state.");
        _presetName.Text = name;
    }

    private void DeleteSelectedPreset()
    {
        if (SelectedPreset() is not { } name) { Status("No preset selected."); return; }
        Status(GameplayTuning.DeletePreset(name) ? $"Deleted preset '{name}'." : $"Could not delete '{name}'.");
        RefreshPresetList(null);
    }

    /// <summary>Typing a name must not drive the ball or trigger hotkeys, so the tree
    /// pauses for the duration of the edit unless it was already paused.</summary>
    private void OnTextEntryFocus()
    {
        if (GetTree() is { Paused: false } tree)
        {
            tree.Paused = true;
            _pausedForTyping = true;
        }
    }

    private void OnTextEntryBlur()
    {
        if (!_pausedForTyping) return;
        _pausedForTyping = false;
        if (GetTree() is { } tree && !_pausedByPanel) tree.Paused = false;
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
        {
            RefreshAll();
            RefreshPresetList(SelectedPreset());
            return;
        }
        ReleaseOwnedFocus();
        if (_pausedByPanel) _pauseBox.ButtonPressed = false;   // never leave the game paused and hidden
    }

    private void OnPauseToggled(bool pressed)
    {
        _pausedByPanel = pressed;
        if (GetTree() is { } tree) tree.Paused = pressed;
    }

    private void RefreshAll()
    {
        foreach (var entry in _paramRows) entry.Refresh();
        foreach (var entry in _toggleRows) entry.Refresh();
    }

    private void ReleaseOwnedFocus()
    {
        if (GetViewport()?.GuiGetFocusOwner() is { } owner && IsAncestorOf(owner)) owner.ReleaseFocus();
    }

    private void Status(string message)
    {
        _statusLabel.Text = message;
        _statusTimer = 3d;
    }
}
