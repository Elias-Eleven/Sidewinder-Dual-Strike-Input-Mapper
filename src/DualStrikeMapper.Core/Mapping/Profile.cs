using SharpHook.Data;
using DualStrikeMapper.Core.Controller;

namespace DualStrikeMapper.Core.Mapping;

public enum ActionKind
{
    None,
    MouseButton,
    Scroll,
    Keys,
    /// <summary>While held the cursor ignores the handle, so it can be re-centred (like lifting a mouse).</summary>
    Lift,
    /// <summary>Jumps the cursor to the middle of the screen. While held it also acts like Lift,
    /// so the handle can be put back in the middle to match.</summary>
    Recenter,
    /// <summary>While held the cursor moves slower for precise aiming.</summary>
    Precision,
    /// <summary>Turns handle-to-mouse movement on or off.</summary>
    ToggleMouse,
}

public enum MouseButtonKind { Left, Right, Middle, Back, Forward }

public enum ScrollDirection { Up, Down, Left, Right }

public sealed class ControlAction
{
    public ActionKind Kind { get; set; }
    public MouseButtonKind Button { get; set; }
    public ScrollDirection Scroll { get; set; }
    /// <summary>Keys pressed together, in order (modifiers first). Released in reverse order.</summary>
    public List<KeyCode> Keys { get; set; } = [];
    /// <summary>Optional custom label shown in the GUI instead of the generated description.</summary>
    public string? Label { get; set; }

    public static ControlAction None() => new();
    public static ControlAction Mouse(MouseButtonKind button, string? label = null) => new() { Kind = ActionKind.MouseButton, Button = button, Label = label };
    public static ControlAction Wheel(ScrollDirection direction, string? label = null) => new() { Kind = ActionKind.Scroll, Scroll = direction, Label = label };
    public static ControlAction Combo(string? label, params KeyCode[] keys) => new() { Kind = ActionKind.Keys, Keys = [.. keys], Label = label };
    public static ControlAction Special(ActionKind kind, string? label = null) => new() { Kind = kind, Label = label };

    public ControlAction Clone() => new() { Kind = Kind, Button = Button, Scroll = Scroll, Keys = [.. Keys], Label = Label };
}

public enum MouseMode
{
    /// <summary>Handle position tracks the cursor in the aim zone; pushing past it keeps it moving.</summary>
    Hybrid,
    /// <summary>Classic joystick: tilt sets cursor speed. Best for sticks that spring back to centre.</summary>
    Joystick,
}

public sealed class MouseSettings
{
    public MouseMode Mode { get; set; } = MouseMode.Hybrid;
    /// <summary>Hybrid: pixels moved per full unit of handle travel (centre to edge = 1 unit).</summary>
    public double AimSensitivity { get; set; } = 660;
    /// <summary>Size of the aim zone as a fraction of the handle travel (0.3..0.98).</summary>
    public double AimZone { get; set; } = 0.85;
    /// <summary>Pixels per second when the handle is pushed all the way (spin zone / joystick mode).</summary>
    public double SpinSpeed { get; set; } = 1400;
    /// <summary>Joystick mode: ignore tilt below this fraction.</summary>
    public double Deadzone { get; set; } = 0.12;
    /// <summary>Joystick mode: response curve exponent (1 = linear).</summary>
    public double Curve { get; set; } = 1.8;
    /// <summary>0 = raw, 0.9 = very smooth (adds a little lag). Hides sensor jitter.</summary>
    public double Smoothing { get; set; } = 0.35;
    /// <summary>0..1, how much speed the cursor keeps when Lift is pressed (trackball-style fling).</summary>
    public double Momentum { get; set; } = 0.5;
    /// <summary>Speed multiplier while Precision is held.</summary>
    public double PrecisionFactor { get; set; } = 0.35;
    public bool InvertX { get; set; }
    /// <summary>On by default: the handle reports up/down opposite to the cursor.</summary>
    public bool InvertY { get; set; } = true;

    public MouseSettings Clone() => (MouseSettings)MemberwiseClone();
}

public sealed class Profile
{
    public string Name { get; set; } = "Profile";
    public string Description { get; set; } = "";
    /// <summary>For copies: the built-in layout this one started from. "Reset" goes back to it.</summary>
    public string? BasedOn { get; set; }
    public Dictionary<ControlId, ControlAction> Mappings { get; set; } = [];
    public MouseSettings Mouse { get; set; } = new();

    public ControlAction Get(ControlId id) => Mappings.TryGetValue(id, out var action) ? action : ControlAction.None();

    public Profile Clone() => new()
    {
        Name = Name,
        Description = Description,
        BasedOn = BasedOn,
        Mappings = Mappings.ToDictionary(p => p.Key, p => p.Value.Clone()),
        Mouse = Mouse.Clone(),
    };
}
