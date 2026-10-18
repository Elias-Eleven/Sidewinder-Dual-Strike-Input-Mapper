namespace DualStrikeMapper.Core.Controller;

/// <summary>
/// Human names for controls, as printed on the Dual Strike. Checked live on the real pad:
/// 1-4 = A B C D (right-hand diamond), 5-6 = X Y, 7-8 = left/right trigger, 9 = arrow up.
/// </summary>
public static class ControlNames
{
    private static readonly Dictionary<ControlId, string> DualStrike = new()
    {
        [ControlId.Button1] = "A",
        [ControlId.Button2] = "B",
        [ControlId.Button3] = "C",
        [ControlId.Button4] = "D",
        [ControlId.Button5] = "X",
        [ControlId.Button6] = "Y",
        [ControlId.Button7] = "Left trigger",
        [ControlId.Button8] = "Right trigger",
        [ControlId.Button9] = "Arrow up",
    };

    public static string Get(ControlId id, bool dualStrike = true) => id switch
    {
        ControlId.DpadUp => "D-pad up",
        ControlId.DpadRight => "D-pad right",
        ControlId.DpadDown => "D-pad down",
        ControlId.DpadLeft => "D-pad left",
        _ when dualStrike && DualStrike.TryGetValue(id, out var name) => name,
        _ => $"Button {(int)id + 1}",
    };

    /// <summary>Short text for a button chip ("LT", "A", "▲").</summary>
    public static string Short(ControlId id) => id switch
    {
        ControlId.Button7 => "LT",
        ControlId.Button8 => "RT",
        ControlId.Button9 => "↑",
        ControlId.DpadUp => "▲",
        ControlId.DpadRight => "▶",
        ControlId.DpadDown => "▼",
        ControlId.DpadLeft => "◀",
        _ when DualStrike.TryGetValue(id, out var name) => name,
        _ => ((int)id + 1).ToString(),
    };

    /// <summary>The controls the Dual Strike actually has, in display order.</summary>
    public static IReadOnlyList<ControlId> DualStrikeControls { get; } =
    [
        ControlId.Button7,
        ControlId.DpadUp, ControlId.DpadRight, ControlId.DpadDown, ControlId.DpadLeft,
        ControlId.Button9, ControlId.Button5, ControlId.Button6,
        ControlId.Button8,
        ControlId.Button1, ControlId.Button2, ControlId.Button3, ControlId.Button4,
    ];
}
