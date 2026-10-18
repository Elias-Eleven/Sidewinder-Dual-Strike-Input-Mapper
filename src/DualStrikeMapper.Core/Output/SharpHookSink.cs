using SharpHook.Data;
using SharpHook.Providers;
using SharpHook.Simulation;
using DualStrikeMapper.Core.Mapping;

namespace DualStrikeMapper.Core.Output;

/// <summary>
/// Sends input through SharpHook (libuiohook): SendInput on Windows, CGEvent on macOS,
/// XTest or uinput virtual devices on Linux.
/// </summary>
public sealed class SharpHookSink : IInputSink
{
    private const short WheelNotch = 120;

    private readonly EventSimulator _simulator = EventSimulator.Create("DualStrikeMapper");

    public void MoveRelative(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
            return;
        _simulator.SimulateMouseMovementRelative(Clamp(dx), Clamp(dy));
    }

    public void CenterCursor()
    {
        var screens = UioHookProvider.Instance.CreateScreenInfo();
        if (screens is null || screens.Length == 0)
            return;
        // The main screen is the one at the desktop origin.
        var screen = screens.FirstOrDefault(s => s.X == 0 && s.Y == 0, screens[0]);
        _simulator.SimulateMouseMovement(Clamp(screen.X + screen.Width / 2), Clamp(screen.Y + screen.Height / 2));
    }

    public void MouseDown(MouseButtonKind button) => _simulator.SimulateMousePress(ToSharpHook(button));

    public void MouseUp(MouseButtonKind button) => _simulator.SimulateMouseRelease(ToSharpHook(button));

    public void Scroll(ScrollDirection direction)
    {
        // SharpHook: positive rotation = up / left.
        var (rotation, axis) = direction switch
        {
            ScrollDirection.Up => (WheelNotch, MouseWheelScrollDirection.Vertical),
            ScrollDirection.Down => ((short)-WheelNotch, MouseWheelScrollDirection.Vertical),
            ScrollDirection.Left => (WheelNotch, MouseWheelScrollDirection.Horizontal),
            _ => ((short)-WheelNotch, MouseWheelScrollDirection.Horizontal),
        };
        _simulator.SimulateMouseWheel(rotation, axis, MouseWheelScrollType.UnitScroll);
    }

    public void KeyDown(KeyCode key) => _simulator.SimulateKeyPress(key);

    public void KeyUp(KeyCode key) => _simulator.SimulateKeyRelease(key);

    private static short Clamp(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private static MouseButton ToSharpHook(MouseButtonKind button) => button switch
    {
        MouseButtonKind.Left => MouseButton.Button1,
        MouseButtonKind.Right => MouseButton.Button2,
        MouseButtonKind.Middle => MouseButton.Button3,
        MouseButtonKind.Back => MouseButton.Button4,
        _ => MouseButton.Button5,
    };

    public void Dispose() => _simulator.Dispose();
}
