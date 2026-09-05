using Godot;

namespace Rushcore.Core;

/// <summary>
/// Semantic action registration (05 §18). Only Space is contractual; every other
/// physical binding is an implementation default.
/// </summary>
public static class InputBootstrap
{
    public const string MoveForward = "rc_move_forward";
    public const string MoveBack = "rc_move_back";
    public const string MoveLeft = "rc_move_left";
    public const string MoveRight = "rc_move_right";
    public const string Jump = "rc_jump";
    public const string Boost = "rc_boost";
    public const string ZoomIn = "rc_zoom_in";
    public const string ZoomOut = "rc_zoom_out";
    public const string ToggleTuning = "rc_toggle_tuning";
    public const string ToggleTelemetry = "rc_toggle_telemetry";
    public const string Recover = "rc_recover";
    public const string DebugRefillBoost = "rc_debug_refill_boost";
    public const string DebugRegenerateWorld = "rc_debug_regen_world";
    public const string DebugTeleportStart = "rc_debug_teleport_start";
    public const string DebugTogglePhysicsHz = "rc_debug_toggle_physics_hz";

    public static void Register()
    {
        Key(MoveForward, Godot.Key.W, Godot.Key.Up);
        Key(MoveBack, Godot.Key.S, Godot.Key.Down);
        Key(MoveLeft, Godot.Key.A, Godot.Key.Left);
        Key(MoveRight, Godot.Key.D, Godot.Key.Right);
        Key(Jump, Godot.Key.Space);
        Key(Boost, Godot.Key.Shift);
        Key(ZoomIn, Godot.Key.Equal, Godot.Key.KpAdd);
        Key(ZoomOut, Godot.Key.Minus, Godot.Key.KpSubtract);
        Key(ToggleTuning, Godot.Key.F1);
        Key(ToggleTelemetry, Godot.Key.F2);
        Key(Recover, Godot.Key.R);
        Key(DebugRefillBoost, Godot.Key.B);
        Key(DebugRegenerateWorld, Godot.Key.F5);
        Key(DebugTeleportStart, Godot.Key.T);
        Key(DebugTogglePhysicsHz, Godot.Key.F4);

        // Gamepad parity for the movement verbs.
        JoyButton(Jump, Godot.JoyButton.A);
        JoyButton(Boost, Godot.JoyButton.X);
        JoyButton(ZoomIn, Godot.JoyButton.DpadUp);
        JoyButton(ZoomOut, Godot.JoyButton.DpadDown);
        JoyAxis(MoveLeft, Godot.JoyAxis.LeftX, -1f);
        JoyAxis(MoveRight, Godot.JoyAxis.LeftX, 1f);
        JoyAxis(MoveForward, Godot.JoyAxis.LeftY, -1f);
        JoyAxis(MoveBack, Godot.JoyAxis.LeftY, 1f);

        MouseWheel(ZoomIn, MouseButton.WheelUp);
        MouseWheel(ZoomOut, MouseButton.WheelDown);
    }

    private static void Ensure(string action)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action, 0.25f);
    }

    private static void Key(string action, params Key[] keys)
    {
        Ensure(action);
        foreach (var k in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
    }

    private static void JoyButton(string action, JoyButton button)
    {
        Ensure(action);
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
    }

    private static void JoyAxis(string action, JoyAxis axis, float dir)
    {
        Ensure(action);
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = dir });
    }

    private static void MouseWheel(string action, MouseButton button)
    {
        Ensure(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }

    /// <summary>True while a text field owns keyboard focus; hotkeys must stand down
    /// so typing "r" or "-" does not recover the player or zoom the camera.</summary>
    public static bool IsTextEntryFocused(Viewport? viewport) =>
        viewport?.GuiGetFocusOwner() is LineEdit or TextEdit;

    /// <summary>Camera-relative stick/WASD vector; X = right, Y = forward.</summary>
    public static Vector2 ReadMoveVector()
        => Input.GetVector(MoveLeft, MoveRight, MoveBack, MoveForward);
}
