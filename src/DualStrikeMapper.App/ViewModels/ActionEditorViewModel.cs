using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpHook.Data;
using DualStrikeMapper.App.Input;
using DualStrikeMapper.Core.Mapping;

namespace DualStrikeMapper.App.ViewModels;

/// <summary>Edits the action of the selected control. Every change is applied immediately.</summary>
public partial class ActionEditorViewModel : ViewModelBase
{
    private static readonly ActionKind[] Kinds =
        [ActionKind.None, ActionKind.MouseButton, ActionKind.Scroll, ActionKind.Keys, ActionKind.Recenter, ActionKind.Lift, ActionKind.Precision, ActionKind.ToggleMouse];

    private readonly Action<ControlAction> _apply;
    private bool _loading;

    public ActionEditorViewModel(Action<ControlAction> apply) => _apply = apply;

    public IReadOnlyList<string> KindNames { get; } =
        ["Nothing", "Mouse button", "Scroll wheel", "Keyboard keys", "Re-centre cursor", "Lift handle (cursor stays put)", "Precision aim (slow)", "Mouse on/off"];

    public IReadOnlyList<string> MouseButtonNames { get; } = ["Left click", "Right click", "Middle click", "Mouse back", "Mouse forward"];

    public IReadOnlyList<string> ScrollNames { get; } = ["Up", "Down", "Left", "Right"];

    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Chip { get; set; } = "";
    [ObservableProperty] public partial int KindIndex { get; set; }
    [ObservableProperty] public partial int MouseButtonIndex { get; set; }
    [ObservableProperty] public partial int ScrollIndex { get; set; }
    [ObservableProperty] public partial bool Ctrl { get; set; }
    [ObservableProperty] public partial bool Shift { get; set; }
    [ObservableProperty] public partial bool Alt { get; set; }
    [ObservableProperty] public partial bool Win { get; set; }
    [ObservableProperty] public partial KeyCode? MainKey { get; set; }
    [ObservableProperty] public partial string Label { get; set; } = "";

    public bool IsMouseButton => Kind == ActionKind.MouseButton;
    public bool IsScroll => Kind == ActionKind.Scroll;
    public bool IsKeys => Kind == ActionKind.Keys;
    public string MainKeyText => MainKey is { } k ? ActionText.KeyName(k) : "none yet";
    public string MetaName => OperatingSystem.IsMacOS() ? "Cmd" : "Win";
    public string Preview => ActionText.Describe(Build() is { } a ? WithoutLabel(a) : ControlAction.None());

    public string Hint => Kind switch
    {
        ActionKind.Recenter => "Jumps the cursor to the middle of the screen. Keep holding while you move the handle back to the middle, then let go.",
        ActionKind.Lift => "Hold to move the handle without moving the cursor, like lifting a mouse off the desk. Let go to keep aiming.",
        ActionKind.Precision => "Hold to slow the cursor down for small, precise movements.",
        ActionKind.ToggleMouse => "Press to switch handle-to-mouse control off and on.",
        ActionKind.Scroll => "Hold to keep scrolling.",
        ActionKind.Keys => "Keys are held down for as long as you hold the button.",
        _ => "",
    };

    private ActionKind Kind => Kinds[Math.Clamp(KindIndex, 0, Kinds.Length - 1)];

    public void Load(string title, string chip, ControlAction action)
    {
        _loading = true;
        Title = title;
        Chip = chip;
        KindIndex = Math.Max(0, Array.IndexOf(Kinds, action.Kind));
        MouseButtonIndex = (int)action.Button;
        ScrollIndex = (int)action.Scroll;
        Ctrl = action.Keys.Any(k => k is KeyCode.VcLeftControl or KeyCode.VcRightControl);
        Shift = action.Keys.Any(k => k is KeyCode.VcLeftShift or KeyCode.VcRightShift);
        Alt = action.Keys.Any(k => k is KeyCode.VcLeftAlt or KeyCode.VcRightAlt);
        Win = action.Keys.Any(k => k is KeyCode.VcLeftMeta or KeyCode.VcRightMeta);
        MainKey = action.Keys.Where(k => !KeyMap.IsModifier(k)).Select(k => (KeyCode?)k).LastOrDefault();
        Label = action.Label ?? "";
        _loading = false;
        RefreshDerived();
    }

    /// <summary>True while the capture box has focus and is listening for an input.</summary>
    [ObservableProperty] public partial bool IsCapturing { get; set; }

    /// <summary>Something was recorded since the box started listening.</summary>
    [ObservableProperty] public partial bool HasCaptured { get; set; }

    /// <summary>While listening the box shows the latest input and keeps listening until Done.</summary>
    public string CaptureText => IsCapturing
        ? HasCaptured ? Preview : "Listening… press a key, click or scroll"
        : Kind is ActionKind.None or ActionKind.MouseButton or ActionKind.Scroll or ActionKind.Keys
            ? $"{Preview}  ·  click here to change"
            : "Click here, then press a key, click or scroll";

    public bool ShowCaptureHint => IsCapturing && HasCaptured;

    partial void OnIsCapturingChanged(bool value)
    {
        HasCaptured = false;
        OnPropertyChanged(nameof(CaptureText));
        OnPropertyChanged(nameof(ShowCaptureHint));
    }

    partial void OnHasCapturedChanged(bool value)
    {
        OnPropertyChanged(nameof(CaptureText));
        OnPropertyChanged(nameof(ShowCaptureHint));
    }

    /// <summary>A key was pressed in the capture box.</summary>
    public void CaptureKey(KeyCode code) => Batch(() =>
    {
        if (Kind != ActionKind.Keys)
        {
            // Coming from a mouse/scroll action: start a fresh key combo.
            KindIndex = Array.IndexOf(Kinds, ActionKind.Keys);
            Ctrl = Shift = Alt = Win = false;
            MainKey = null;
            Label = "";
        }
        // A lone modifier press adds that modifier. Anything else becomes the main key.
        switch (code)
        {
            case KeyCode.VcLeftControl or KeyCode.VcRightControl: Ctrl = true; break;
            case KeyCode.VcLeftShift or KeyCode.VcRightShift: Shift = true; break;
            case KeyCode.VcLeftAlt or KeyCode.VcRightAlt: Alt = true; break;
            case KeyCode.VcLeftMeta or KeyCode.VcRightMeta: Win = true; break;
            default: MainKey = code; break;
        }
    });

    /// <summary>A mouse button was clicked on the capture box.</summary>
    public void CaptureMouse(MouseButtonKind button) => Batch(() =>
    {
        KindIndex = Array.IndexOf(Kinds, ActionKind.MouseButton);
        MouseButtonIndex = (int)button;
        Label = "";
    });

    /// <summary>The mouse wheel was turned over the capture box.</summary>
    public void CaptureScroll(ScrollDirection direction) => Batch(() =>
    {
        KindIndex = Array.IndexOf(Kinds, ActionKind.Scroll);
        ScrollIndex = (int)direction;
        Label = "";
    });

    /// <summary>Apply several property changes as one edit.</summary>
    private void Batch(Action change)
    {
        _loading = true;
        change();
        _loading = false;
        if (IsCapturing)
            HasCaptured = true;
        Changed();
    }

    [RelayCommand]
    private void ClearKeys() => Batch(() =>
    {
        Ctrl = Shift = Alt = Win = false;
        MainKey = null;
    });

    partial void OnKindIndexChanged(int value)
    {
        // Labels describe the old action, so drop them when the action type changes.
        if (!_loading)
            Label = "";
        Changed();
    }

    partial void OnMouseButtonIndexChanged(int value) => Changed();
    partial void OnScrollIndexChanged(int value) => Changed();
    partial void OnCtrlChanged(bool value) => Changed();
    partial void OnShiftChanged(bool value) => Changed();
    partial void OnAltChanged(bool value) => Changed();
    partial void OnWinChanged(bool value) => Changed();
    partial void OnMainKeyChanged(KeyCode? value) => Changed();
    partial void OnLabelChanged(string value) => Changed();

    private void Changed()
    {
        if (_loading)
            return;
        RefreshDerived();
        if (Build() is { } action)
            _apply(action);
    }

    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(IsMouseButton));
        OnPropertyChanged(nameof(IsScroll));
        OnPropertyChanged(nameof(IsKeys));
        OnPropertyChanged(nameof(MainKeyText));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(CaptureText));
    }

    private ControlAction? Build()
    {
        var action = new ControlAction
        {
            Kind = Kind,
            Button = (MouseButtonKind)Math.Clamp(MouseButtonIndex, 0, 4),
            Scroll = (ScrollDirection)Math.Clamp(ScrollIndex, 0, 3),
            Label = string.IsNullOrWhiteSpace(Label) ? null : Label.Trim(),
        };
        if (action.Kind == ActionKind.Keys)
        {
            if (Ctrl) action.Keys.Add(KeyCode.VcLeftControl);
            if (Shift) action.Keys.Add(KeyCode.VcLeftShift);
            if (Alt) action.Keys.Add(KeyCode.VcLeftAlt);
            if (Win) action.Keys.Add(KeyCode.VcLeftMeta);
            if (MainKey is { } key) action.Keys.Add(key);
        }
        return action;
    }

    private static ControlAction WithoutLabel(ControlAction a)
    {
        var copy = a.Clone();
        copy.Label = null;
        return copy;
    }
}
