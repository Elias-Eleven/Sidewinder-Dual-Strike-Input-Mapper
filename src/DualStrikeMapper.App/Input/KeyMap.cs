using System;
using System.Collections.Generic;
using Avalonia.Input;
using SharpHook.Data;

namespace DualStrikeMapper.App.Input;

/// <summary>Translates keys pressed in the GUI (Avalonia) into keys we can simulate (SharpHook).</summary>
public static class KeyMap
{
    private static readonly Dictionary<Key, KeyCode> Map = Build();

    public static bool TryGet(Key key, out KeyCode code) => Map.TryGetValue(key, out code);

    public static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    public static bool IsModifier(KeyCode code) => code is KeyCode.VcLeftControl or KeyCode.VcRightControl
        or KeyCode.VcLeftShift or KeyCode.VcRightShift or KeyCode.VcLeftAlt or KeyCode.VcRightAlt
        or KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    private static Dictionary<Key, KeyCode> Build()
    {
        var m = new Dictionary<Key, KeyCode>();
        // Avalonia's letter/digit/F keys are sequential. SharpHook's codes are scan codes (not sequential),
        // so match them by name.
        for (var i = 0; i < 26; i++)
            m[Key.A + i] = Enum.Parse<KeyCode>("Vc" + (char)('A' + i));
        for (var i = 0; i < 10; i++)
        {
            m[Key.D0 + i] = Enum.Parse<KeyCode>("Vc" + i);
            m[Key.NumPad0 + i] = Enum.Parse<KeyCode>("VcNumPad" + i);
        }
        for (var i = 1; i <= 24; i++)
            m[Key.F1 + (i - 1)] = Enum.Parse<KeyCode>("VcF" + i);

        m[Key.LeftCtrl] = KeyCode.VcLeftControl;
        m[Key.RightCtrl] = KeyCode.VcRightControl;
        m[Key.LeftShift] = KeyCode.VcLeftShift;
        m[Key.RightShift] = KeyCode.VcRightShift;
        m[Key.LeftAlt] = KeyCode.VcLeftAlt;
        m[Key.RightAlt] = KeyCode.VcRightAlt;
        m[Key.LWin] = KeyCode.VcLeftMeta;
        m[Key.RWin] = KeyCode.VcRightMeta;
        m[Key.Apps] = KeyCode.VcContextMenu;

        m[Key.Enter] = KeyCode.VcEnter;
        m[Key.Escape] = KeyCode.VcEscape;
        m[Key.Space] = KeyCode.VcSpace;
        m[Key.Tab] = KeyCode.VcTab;
        m[Key.Back] = KeyCode.VcBackspace;
        m[Key.Delete] = KeyCode.VcDelete;
        m[Key.Insert] = KeyCode.VcInsert;
        m[Key.Home] = KeyCode.VcHome;
        m[Key.End] = KeyCode.VcEnd;
        m[Key.PageUp] = KeyCode.VcPageUp;
        m[Key.PageDown] = KeyCode.VcPageDown;
        m[Key.Up] = KeyCode.VcUp;
        m[Key.Down] = KeyCode.VcDown;
        m[Key.Left] = KeyCode.VcLeft;
        m[Key.Right] = KeyCode.VcRight;
        m[Key.CapsLock] = KeyCode.VcCapsLock;
        m[Key.NumLock] = KeyCode.VcNumLock;
        m[Key.Scroll] = KeyCode.VcScrollLock;
        m[Key.PrintScreen] = KeyCode.VcPrintScreen;
        m[Key.Pause] = KeyCode.VcPause;

        m[Key.OemTilde] = KeyCode.VcBackQuote;
        m[Key.OemMinus] = KeyCode.VcMinus;
        m[Key.OemPlus] = KeyCode.VcEquals;
        m[Key.OemOpenBrackets] = KeyCode.VcOpenBracket;
        m[Key.OemCloseBrackets] = KeyCode.VcCloseBracket;
        m[Key.OemPipe] = KeyCode.VcBackslash;
        m[Key.OemSemicolon] = KeyCode.VcSemicolon;
        m[Key.OemQuotes] = KeyCode.VcQuote;
        m[Key.OemComma] = KeyCode.VcComma;
        m[Key.OemPeriod] = KeyCode.VcPeriod;
        m[Key.OemQuestion] = KeyCode.VcSlash;

        m[Key.Multiply] = KeyCode.VcNumPadMultiply;
        m[Key.Add] = KeyCode.VcNumPadAdd;
        m[Key.Subtract] = KeyCode.VcNumPadSubtract;
        m[Key.Divide] = KeyCode.VcNumPadDivide;
        m[Key.Decimal] = KeyCode.VcNumPadDecimal;

        m[Key.MediaPlayPause] = KeyCode.VcMediaPlay;
        m[Key.MediaStop] = KeyCode.VcMediaStop;
        m[Key.MediaNextTrack] = KeyCode.VcMediaNext;
        m[Key.MediaPreviousTrack] = KeyCode.VcMediaPrevious;
        m[Key.VolumeMute] = KeyCode.VcVolumeMute;
        m[Key.VolumeDown] = KeyCode.VcVolumeDown;
        m[Key.VolumeUp] = KeyCode.VcVolumeUp;
        m[Key.BrowserBack] = KeyCode.VcBrowserBack;
        m[Key.BrowserForward] = KeyCode.VcBrowserForward;
        m[Key.BrowserRefresh] = KeyCode.VcBrowserRefresh;
        m[Key.BrowserHome] = KeyCode.VcBrowserHome;
        m[Key.BrowserSearch] = KeyCode.VcBrowserSearch;
        return m;
    }
}
