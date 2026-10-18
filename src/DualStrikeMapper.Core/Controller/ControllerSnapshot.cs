namespace DualStrikeMapper.Core.Controller;

/// <summary>D-pad (hat) bits, same values SDL uses.</summary>
[Flags]
public enum HatDirection : byte
{
    Centered = 0,
    Up = 1,
    Right = 2,
    Down = 4,
    Left = 8,
}

/// <summary>Every physical input that can be mapped to an action.</summary>
public enum ControlId
{
    Button1, Button2, Button3, Button4, Button5, Button6, Button7, Button8,
    Button9, Button10, Button11, Button12, Button13, Button14, Button15, Button16,
    DpadUp, DpadRight, DpadDown, DpadLeft,
}

/// <summary>One reading of the controller. Axes are normalised to -1..1.</summary>
public sealed record ControllerSnapshot(bool Connected, string Name, float X, float Y, bool[] Buttons, HatDirection Hat)
{
    public const int MaxButtons = 16;

    public static ControllerSnapshot Disconnected { get; } = new(false, "", 0, 0, [], HatDirection.Centered);

    public bool IsPressed(ControlId id) => id switch
    {
        ControlId.DpadUp => Hat.HasFlag(HatDirection.Up),
        ControlId.DpadRight => Hat.HasFlag(HatDirection.Right),
        ControlId.DpadDown => Hat.HasFlag(HatDirection.Down),
        ControlId.DpadLeft => Hat.HasFlag(HatDirection.Left),
        _ => (int)id < Buttons.Length && Buttons[(int)id],
    };
}
