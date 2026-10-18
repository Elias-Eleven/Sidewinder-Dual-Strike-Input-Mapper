using SDL3;

namespace DualStrikeMapper.Core.Controller;

/// <summary>
/// Reads a joystick through SDL3 and handles plugging/unplugging.
/// Prefers the SideWinder Dual Strike, falls back to the first joystick found.
/// All calls must come from the same thread.
/// </summary>
public sealed class SdlJoystickReader : IDisposable
{
    public const ushort MicrosoftVendorId = 0x045E;
    public const ushort DualStrikeProductId = 0x0028;

    private IntPtr _joystick;
    private string _name = "";
    private long _nextScanTicks;
    private bool _initialized;

    public bool IsDualStrike { get; private set; }

    public bool Initialize()
    {
        // Keep receiving input while other windows have focus.
        SDL.SetHint(SDL.Hints.JoystickAllowBackgroundEvents, "1");
        _initialized = SDL.Init(SDL.InitFlags.Joystick);
        if (_initialized)
            SDL.SetJoystickEventsEnabled(false); // we poll state ourselves, so don't fill the event queue
        return _initialized;
    }

    public string LastError => SDL.GetError();

    public ControllerSnapshot Poll()
    {
        if (!_initialized)
            return ControllerSnapshot.Disconnected;

        SDL.UpdateJoysticks();

        if (_joystick != IntPtr.Zero && !SDL.JoystickConnected(_joystick))
            Close();

        if (_joystick == IntPtr.Zero)
        {
            if (Environment.TickCount64 < _nextScanTicks)
                return ControllerSnapshot.Disconnected;
            _nextScanTicks = Environment.TickCount64 + 1000;
            if (!TryOpen())
                return ControllerSnapshot.Disconnected;
        }

        var x = SDL.GetNumJoystickAxes(_joystick) > 0 ? Normalize(SDL.GetJoystickAxis(_joystick, 0)) : 0f;
        var y = SDL.GetNumJoystickAxes(_joystick) > 1 ? Normalize(SDL.GetJoystickAxis(_joystick, 1)) : 0f;

        var count = Math.Min(SDL.GetNumJoystickButtons(_joystick), ControllerSnapshot.MaxButtons);
        var buttons = new bool[count];
        for (var i = 0; i < count; i++)
            buttons[i] = SDL.GetJoystickButton(_joystick, i);

        var hat = SDL.GetNumJoystickHats(_joystick) > 0
            ? (HatDirection)(byte)SDL.GetJoystickHat(_joystick, 0)
            : HatDirection.Centered;

        return new ControllerSnapshot(true, _name, x, y, buttons, hat);
    }

    private bool TryOpen()
    {
        var ids = SDL.GetJoysticks(out var count);
        if (ids is null || count == 0)
            return false;

        var pick = ids[0];
        foreach (var id in ids)
        {
            if (SDL.GetJoystickVendorForID(id) == MicrosoftVendorId && SDL.GetJoystickProductForID(id) == DualStrikeProductId)
            {
                pick = id;
                break;
            }
        }

        _joystick = SDL.OpenJoystick(pick);
        if (_joystick == IntPtr.Zero)
            return false;

        _name = SDL.GetJoystickName(_joystick) ?? "Joystick";
        IsDualStrike = SDL.GetJoystickVendor(_joystick) == MicrosoftVendorId && SDL.GetJoystickProduct(_joystick) == DualStrikeProductId;
        return true;
    }

    private void Close()
    {
        if (_joystick != IntPtr.Zero)
            SDL.CloseJoystick(_joystick);
        _joystick = IntPtr.Zero;
        _name = "";
        IsDualStrike = false;
    }

    private static float Normalize(short value) => Math.Clamp(value / 32767f, -1f, 1f);

    public void Dispose()
    {
        Close();
        if (_initialized)
            SDL.Quit();
        _initialized = false;
    }
}
