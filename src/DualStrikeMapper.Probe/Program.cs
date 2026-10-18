using System.Runtime.InteropServices;
using SDL3;
using SharpHook.Providers;
using DualStrikeMapper.Core.Output;

// "center": checks that CenterCursor lands in the middle of the main screen.
if (args is ["center"])
{
    if (OperatingSystem.IsWindows())
        SetProcessDPIAware(); // match the real app, which is DPI aware

    foreach (var s in UioHookProvider.Instance.CreateScreenInfo() ?? [])
        Console.WriteLine($"screen {s.Number}: x={s.X} y={s.Y} {s.Width}x{s.Height}");
    using var sink = new SharpHookSink();
    sink.CenterCursor();
    Thread.Sleep(100);
    if (OperatingSystem.IsWindows() && GetCursorPos(out var p))
        Console.WriteLine($"cursor now at {p.X},{p.Y}");
    return;
}

// Quick SDL3 probe: lists joysticks and prints state changes of the first one.
SDL.SetHint(SDL.Hints.JoystickAllowBackgroundEvents, "1");
if (!SDL.Init(SDL.InitFlags.Joystick))
{
    Console.WriteLine($"SDL init failed: {SDL.GetError()}");
    return;
}

var seconds = args.Length > 0 ? int.Parse(args[0]) : 3;
var ids = SDL.GetJoysticks(out var count);
Console.WriteLine($"joysticks: {count}");
if (ids is null || count == 0) return;

foreach (var id in ids)
    Console.WriteLine($"  id={id} name='{SDL.GetJoystickNameForID(id)}' vid={SDL.GetJoystickVendorForID(id):X4} pid={SDL.GetJoystickProductForID(id):X4} type={SDL.GetJoystickTypeForID(id)}");

var joy = SDL.OpenJoystick(ids[0]);
Console.WriteLine($"axes={SDL.GetNumJoystickAxes(joy)} buttons={SDL.GetNumJoystickButtons(joy)} hats={SDL.GetNumJoystickHats(joy)} path={SDL.GetJoystickPath(joy)}");

string last = "";
var end = DateTime.UtcNow.AddSeconds(seconds);
while (DateTime.UtcNow < end)
{
    SDL.UpdateJoysticks();
    var axes = string.Join(",", Enumerable.Range(0, SDL.GetNumJoystickAxes(joy)).Select(i => SDL.GetJoystickAxis(joy, i)));
    var buttons = string.Concat(Enumerable.Range(0, SDL.GetNumJoystickButtons(joy)).Select(i => SDL.GetJoystickButton(joy, i) ? "1" : "0"));
    var hat = SDL.GetNumJoystickHats(joy) > 0 ? SDL.GetJoystickHat(joy, 0).ToString() : "-";
    var line = $"axes={axes} buttons={buttons} hat={hat}";
    if (line != last)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
        last = line;
    }
    Thread.Sleep(5);
}
SDL.CloseJoystick(joy);
SDL.Quit();

[DllImport("user32.dll")]
static extern bool GetCursorPos(out CursorPoint point);

[DllImport("user32.dll")]
static extern bool SetProcessDPIAware();

[StructLayout(LayoutKind.Sequential)]
struct CursorPoint
{
    public int X;
    public int Y;
}
