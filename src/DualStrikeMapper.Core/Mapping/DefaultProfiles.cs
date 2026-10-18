using SharpHook.Data;
using DualStrikeMapper.Core.Controller;

namespace DualStrikeMapper.Core.Mapping;

/// <summary>
/// Built-in layouts. Dual Strike buttons: 1-4 = A B C D, 5-6 = X Y, 7-8 = triggers, 9 = arrow up.
/// Like other controllers, the right trigger is the main (left-click) button.
/// No labels: the GUI shows the PC input each button sends ("Space", "Left click").
/// </summary>
public static class DefaultProfiles
{
    public static List<Profile> Create() => [Desktop(), Gaming()];

    /// <summary>Navigate the desktop: triggers click, D-pad scrolls, face buttons send common Windows keys.</summary>
    public static Profile Desktop() => new()
    {
        Name = "Desktop",
        Description = "Navigate Windows with the handle and triggers.",
        Mappings = new()
        {
            [ControlId.Button8] = ControlAction.Mouse(MouseButtonKind.Left),
            [ControlId.Button7] = ControlAction.Mouse(MouseButtonKind.Right),
            [ControlId.Button1] = ControlAction.Combo(null, KeyCode.VcEnter),
            [ControlId.Button2] = ControlAction.Combo(null, KeyCode.VcEscape),
            [ControlId.Button3] = ControlAction.Mouse(MouseButtonKind.Middle),
            [ControlId.Button4] = ControlAction.Combo(null, KeyCode.VcLeftMeta, KeyCode.VcTab),
            [ControlId.Button5] = ControlAction.Combo(null, KeyCode.VcLeftMeta),
            [ControlId.Button6] = ControlAction.Combo(null, KeyCode.VcLeftMeta, KeyCode.VcLeftControl, KeyCode.VcO),
            [ControlId.Button9] = ControlAction.Special(ActionKind.Recenter),
            [ControlId.DpadUp] = ControlAction.Wheel(ScrollDirection.Up),
            [ControlId.DpadDown] = ControlAction.Wheel(ScrollDirection.Down),
            [ControlId.DpadLeft] = ControlAction.Mouse(MouseButtonKind.Back),
            [ControlId.DpadRight] = ControlAction.Mouse(MouseButtonKind.Forward),
        },
    };

    /// <summary>
    /// Standard PC shooter keys, placed like a console layout. The D-pad (left thumb) is WASD,
    /// so keys you press while moving sit on the right thumb's diamond (Space at the bottom,
    /// R on the left). The left thumb's extra buttons get keys you press while standing still.
    /// </summary>
    public static Profile Gaming() => new()
    {
        Name = "Shooter",
        Description = "Standard PC shooter keys, WASD on the D-pad.",
        Mappings = new()
        {
            [ControlId.Button8] = ControlAction.Mouse(MouseButtonKind.Left),
            [ControlId.Button7] = ControlAction.Mouse(MouseButtonKind.Right),
            [ControlId.Button4] = ControlAction.Combo(null, KeyCode.VcSpace),
            [ControlId.Button2] = ControlAction.Combo(null, KeyCode.VcR),
            [ControlId.Button3] = ControlAction.Combo(null, KeyCode.VcLeftControl),
            [ControlId.Button1] = ControlAction.Combo(null, KeyCode.VcLeftShift),
            [ControlId.Button5] = ControlAction.Combo(null, KeyCode.VcE),
            [ControlId.Button6] = ControlAction.Wheel(ScrollDirection.Up),
            [ControlId.Button9] = ControlAction.Special(ActionKind.Lift),
            [ControlId.DpadUp] = ControlAction.Combo(null, KeyCode.VcW),
            [ControlId.DpadLeft] = ControlAction.Combo(null, KeyCode.VcA),
            [ControlId.DpadDown] = ControlAction.Combo(null, KeyCode.VcS),
            [ControlId.DpadRight] = ControlAction.Combo(null, KeyCode.VcD),
        },
    };
}
