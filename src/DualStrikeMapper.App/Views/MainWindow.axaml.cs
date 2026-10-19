using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DualStrikeMapper.App.Input;
using DualStrikeMapper.App.ViewModels;
using DualStrikeMapper.Core.Mapping;

namespace DualStrikeMapper.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Windows uses the multi-size .ico from the XAML; Linux and macOS window managers prefer a PNG.
        if (!OperatingSystem.IsWindows())
            Icon = App.LoadPngIcon();

        // The capture box in the button editor records whatever input comes next:
        // a key, a mouse button, or the wheel. Tunnel so Tab, Enter etc. are captured
        // instead of moving focus.
        InputCapture.AddHandler(KeyDownEvent, OnCaptureKeyDown, RoutingStrategies.Tunnel);
        InputCapture.AddHandler(PointerPressedEvent, OnCapturePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputCapture.AddHandler(PointerWheelChangedEvent, OnCaptureWheel, RoutingStrategies.Tunnel, handledEventsToo: true);
        InputCapture.GotFocus += (_, _) => SetCapturing(true);
        InputCapture.LostFocus += (_, _) => SetCapturing(false);

        // Layout name + description editor: Enter saves, Esc cancels. Clicking away
        // from both fields saves.
        foreach (var box in new[] { RenameBox, DescriptionBox })
        {
            box.AddHandler(KeyDownEvent, OnRenameKeyDown, RoutingStrategies.Tunnel);
            box.LostFocus += (_, _) => Dispatcher.UIThread.Post(CommitIfFocusLeftEditor, DispatcherPriority.Input);
        }
    }

    /// <summary>Close the ⋯ menu once an option is picked (the command runs through its binding).</summary>
    private void OnOptionsItemClick(object? sender, RoutedEventArgs e) => OptionsButton.Flyout?.Hide();

    /// <summary>Moving from the name to the description keeps editing; leaving both saves.</summary>
    private void CommitIfFocusLeftEditor()
    {
        if (!RenameBox.IsKeyboardFocusWithin && !DescriptionBox.IsKeyboardFocusWithin)
            _vm?.CommitRenameCommand.Execute(null);
    }

    private MainViewModel? _vm;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelChanged;
        _vm = DataContext as MainViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsRenaming) && _vm?.IsRenaming == true)
        {
            // Wait until the box is visible, then put the cursor in it with the name selected.
            Dispatcher.UIThread.Post(() =>
            {
                RenameBox.Focus();
                RenameBox.SelectAll();
            }, DispatcherPriority.Input);
        }
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null)
            return;
        if (e.Key == Key.Enter)
        {
            _vm.CommitRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Set when quitting from the tray so the window really closes.</summary>
    public bool AllowClose { get; set; }

    private ActionEditorViewModel? Editor => (DataContext as MainViewModel)?.Editor;

    private void SetCapturing(bool on)
    {
        if (Editor is { } editor)
            editor.IsCapturing = on;
    }

    private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (Editor is { } editor && KeyMap.TryGet(e.Key, out var code))
            editor.CaptureKey(code);
        e.Handled = true;
    }

    private void OnCapturePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        var kind = e.GetCurrentPoint(InputCapture).Properties.PointerUpdateKind;

        // The first left click only starts listening; after that a left click is recorded too.
        if (!InputCapture.IsFocused)
        {
            InputCapture.Focus();
            if (kind == PointerUpdateKind.LeftButtonPressed)
                return;
        }

        MouseButtonKind? button = kind switch
        {
            PointerUpdateKind.LeftButtonPressed => MouseButtonKind.Left,
            PointerUpdateKind.RightButtonPressed => MouseButtonKind.Right,
            PointerUpdateKind.MiddleButtonPressed => MouseButtonKind.Middle,
            PointerUpdateKind.XButton1Pressed => MouseButtonKind.Back,
            PointerUpdateKind.XButton2Pressed => MouseButtonKind.Forward,
            _ => null,
        };
        if (button is { } b)
            Editor?.CaptureMouse(b);
    }

    private void OnCaptureWheel(object? sender, PointerWheelEventArgs e)
    {
        // Don't let the editor sheet scroll while recording the wheel.
        e.Handled = true;
        ScrollDirection? direction =
            e.Delta.Y > 0 ? ScrollDirection.Up :
            e.Delta.Y < 0 ? ScrollDirection.Down :
            e.Delta.X > 0 ? ScrollDirection.Left :
            e.Delta.X < 0 ? ScrollDirection.Right : null;
        if (direction is { } d)
            Editor?.CaptureScroll(d);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && DataContext is MainViewModel vm)
            vm.ClosePanelsCommand.Execute(null);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (AllowClose)
            return;

        if (DataContext is MainViewModel { CloseToTray: true })
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            AllowClose = true;
            (Application.Current as App)?.Quit();
        }
    }
}
