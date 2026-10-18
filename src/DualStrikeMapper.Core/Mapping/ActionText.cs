using SharpHook.Data;

namespace DualStrikeMapper.Core.Mapping;

/// <summary>Readable descriptions of actions and keys for the GUI.</summary>
public static class ActionText
{
    public static string Describe(ControlAction action)
    {
        if (!string.IsNullOrWhiteSpace(action.Label))
            return action.Label!;

        return action.Kind switch
        {
            ActionKind.None => "Not set",
            ActionKind.MouseButton => action.Button switch
            {
                MouseButtonKind.Left => "Left click",
                MouseButtonKind.Right => "Right click",
                MouseButtonKind.Middle => "Middle click",
                MouseButtonKind.Back => "Mouse back",
                _ => "Mouse forward",
            },
            ActionKind.Scroll => $"Scroll {action.Scroll.ToString().ToLowerInvariant()}",
            ActionKind.Keys => action.Keys.Count == 0 ? "No keys" : string.Join(" + ", action.Keys.Select(KeyName)),
            ActionKind.Lift => "Lift handle",
            ActionKind.Recenter => "Re-centre cursor",
            ActionKind.Precision => "Precision aim",
            ActionKind.ToggleMouse => "Mouse on/off",
            _ => action.Kind.ToString(),
        };
    }

    public static string KeyName(KeyCode key) => key switch
    {
        KeyCode.VcLeftControl or KeyCode.VcRightControl => "Ctrl",
        KeyCode.VcLeftShift or KeyCode.VcRightShift => "Shift",
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt => "Alt",
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta => OperatingSystem.IsMacOS() ? "Cmd" : "Win",
        KeyCode.VcEnter => "Enter",
        KeyCode.VcEscape => "Esc",
        KeyCode.VcSpace => "Space",
        KeyCode.VcBackspace => "Backspace",
        KeyCode.VcPageUp => "Page Up",
        KeyCode.VcPageDown => "Page Down",
        KeyCode.VcPrintScreen => "Print Screen",
        KeyCode.VcBackQuote => "`",
        KeyCode.VcMinus => "-",
        KeyCode.VcEquals => "=",
        KeyCode.VcOpenBracket => "[",
        KeyCode.VcCloseBracket => "]",
        KeyCode.VcBackslash => "\\",
        KeyCode.VcSemicolon => ";",
        KeyCode.VcQuote => "'",
        KeyCode.VcComma => ",",
        KeyCode.VcPeriod => ".",
        KeyCode.VcSlash => "/",
        _ => Pretty(key.ToString()),
    };

    private static string Pretty(string name)
    {
        // VcA -> A, VcNumPad1 -> Num Pad 1, VcMediaPlay -> Media Play
        var s = name.StartsWith("Vc", StringComparison.Ordinal) ? name[2..] : name;
        var chars = new List<char>(s.Length + 4);
        for (var i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]))
                chars.Add(' ');
            else if (i > 0 && char.IsDigit(s[i]) && char.IsLetter(s[i - 1]) && !(s[0] == 'F' && s.Length <= 3))
                chars.Add(' ');
            chars.Add(s[i]);
        }
        return new string([.. chars]);
    }
}
