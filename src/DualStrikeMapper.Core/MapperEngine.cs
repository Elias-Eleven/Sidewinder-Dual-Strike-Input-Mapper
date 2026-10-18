using System.Diagnostics;
using System.Runtime.InteropServices;
using DualStrikeMapper.Core.Controller;
using DualStrikeMapper.Core.Mapping;
using DualStrikeMapper.Core.Mouse;
using DualStrikeMapper.Core.Output;

namespace DualStrikeMapper.Core;

/// <summary>
/// Runs the controller loop on its own thread: reads the pad, moves the mouse,
/// and turns button presses into mapped actions.
/// </summary>
public sealed class MapperEngine : IDisposable
{
    private const int LoopIntervalMs = 4;
    private const int SnapshotIntervalMs = 16;
    private const int ScrollRepeatDelayMs = 350;
    private const int ScrollRepeatIntervalMs = 70;

    private static readonly ControlId[] AllControls = Enum.GetValues<ControlId>();

    private readonly Func<IInputSink> _sinkFactory;
    private readonly MouseEngine _mouse = new();
    private readonly ControlAction?[] _active = new ControlAction?[AllControls.Length];
    private readonly long[] _nextRepeat = new long[AllControls.Length];
    private Thread? _thread;
    private volatile bool _running;
    private volatile Profile _profile;
    private volatile bool _mouseEnabled = true;
    private int _liftHeld, _precisionHeld;

    public MapperEngine(Profile profile, Func<IInputSink>? sinkFactory = null)
    {
        _profile = profile.Clone();
        _sinkFactory = sinkFactory ?? (() => new SharpHookSink());
    }

    /// <summary>Raised about 60 times a second with the latest reading.</summary>
    public event Action<ControllerSnapshot>? SnapshotUpdated;

    /// <summary>Raised on the engine thread when a control goes down.</summary>
    public event Action<ControlId>? ControlPressed;

    /// <summary>Raised when a ToggleMouse button flips mouse control.</summary>
    public event Action<bool>? MouseEnabledChanged;

    /// <summary>Raised when SDL or input simulation fails to start.</summary>
    public event Action<string>? Error;

    public bool MouseEnabled
    {
        get => _mouseEnabled;
        set => _mouseEnabled = value;
    }

    /// <summary>When true, presses are reported but not acted on (used while editing mappings).</summary>
    public volatile bool SuppressActions;

    public bool IsDualStrike { get; private set; }

    /// <summary>Swap the active profile. Held buttons finish with the action they started with.</summary>
    public void SetProfile(Profile profile) => _profile = profile.Clone();

    public void Start()
    {
        if (_running)
            return;
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "MapperEngine", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(2000);
        _thread = null;
    }

    private void Run()
    {
        using var timerResolution = HighResolutionTimer.Begin();
        using var reader = new SdlJoystickReader();
        if (!reader.Initialize())
        {
            Error?.Invoke($"Could not start controller support (SDL): {reader.LastError}");
            return;
        }

        IInputSink sink;
        try
        {
            sink = _sinkFactory();
        }
        catch (Exception e)
        {
            Error?.Invoke($"Could not start mouse/keyboard output: {e.Message}");
            return;
        }

        using (sink)
        {
            var previous = ControllerSnapshot.Disconnected;
            var clock = Stopwatch.StartNew();
            var lastTick = clock.Elapsed.TotalSeconds;
            long nextSnapshot = 0;

            while (_running)
            {
                var snapshot = reader.Poll();
                IsDualStrike = reader.IsDualStrike;
                var now = clock.Elapsed.TotalSeconds;
                var dt = Math.Min(now - lastTick, 0.1);
                lastTick = now;

                if (!snapshot.Connected)
                {
                    if (previous.Connected)
                    {
                        ReleaseAll(sink);
                        _mouse.Reset();
                    }
                }
                else
                {
                    HandleControls(previous, snapshot, sink, Environment.TickCount64);
                    var profile = _profile;
                    var (dx, dy) = _mouse.Update(snapshot.X, snapshot.Y, dt, profile.Mouse, _liftHeld > 0, _precisionHeld > 0);
                    if (_mouseEnabled)
                        sink.MoveRelative(dx, dy);
                }

                if (Environment.TickCount64 >= nextSnapshot || snapshot.Connected != previous.Connected)
                {
                    nextSnapshot = Environment.TickCount64 + SnapshotIntervalMs;
                    SnapshotUpdated?.Invoke(snapshot);
                }

                previous = snapshot;
                Thread.Sleep(LoopIntervalMs);
            }

            ReleaseAll(sink);
        }
    }

    private void HandleControls(ControllerSnapshot before, ControllerSnapshot now, IInputSink sink, long tick)
    {
        foreach (var id in AllControls)
        {
            var i = (int)id;
            var wasDown = before.Connected && before.IsPressed(id);
            var isDown = now.IsPressed(id);

            if (isDown && !wasDown)
            {
                ControlPressed?.Invoke(id);
                if (SuppressActions)
                    continue;
                var action = _profile.Get(id);
                _active[i] = action;
                Press(action, sink);
                _nextRepeat[i] = tick + ScrollRepeatDelayMs;
            }
            else if (!isDown && wasDown)
            {
                if (_active[i] is { } action)
                    Release(action, sink);
                _active[i] = null;
            }
            else if (isDown && _active[i] is { Kind: ActionKind.Scroll } scroll && tick >= _nextRepeat[i])
            {
                sink.Scroll(scroll.Scroll);
                _nextRepeat[i] = tick + ScrollRepeatIntervalMs;
            }
        }
    }

    private void Press(ControlAction action, IInputSink sink)
    {
        switch (action.Kind)
        {
            case ActionKind.MouseButton:
                sink.MouseDown(action.Button);
                break;
            case ActionKind.Scroll:
                sink.Scroll(action.Scroll);
                break;
            case ActionKind.Keys:
                foreach (var key in action.Keys)
                    sink.KeyDown(key);
                break;
            case ActionKind.Lift:
                _liftHeld++;
                break;
            case ActionKind.Recenter:
                // Forget speed so there's no momentum fling, then hold the cursor in the middle
                // until the button is released (meanwhile the handle can be put back in the middle).
                _mouse.Reset();
                sink.CenterCursor();
                _liftHeld++;
                break;
            case ActionKind.Precision:
                _precisionHeld++;
                break;
            case ActionKind.ToggleMouse:
                _mouseEnabled = !_mouseEnabled;
                MouseEnabledChanged?.Invoke(_mouseEnabled);
                break;
        }
    }

    private void Release(ControlAction action, IInputSink sink)
    {
        switch (action.Kind)
        {
            case ActionKind.MouseButton:
                sink.MouseUp(action.Button);
                break;
            case ActionKind.Keys:
                for (var k = action.Keys.Count - 1; k >= 0; k--)
                    sink.KeyUp(action.Keys[k]);
                break;
            case ActionKind.Lift or ActionKind.Recenter:
                _liftHeld = Math.Max(0, _liftHeld - 1);
                break;
            case ActionKind.Precision:
                _precisionHeld = Math.Max(0, _precisionHeld - 1);
                break;
        }
    }

    private void ReleaseAll(IInputSink sink)
    {
        for (var i = 0; i < _active.Length; i++)
        {
            if (_active[i] is { } action)
                Release(action, sink);
            _active[i] = null;
        }
        _liftHeld = _precisionHeld = 0;
    }

    public void Dispose() => Stop();

    /// <summary>Windows sleeps in ~15 ms steps by default. Ask for 1 ms so the loop runs smoothly.</summary>
    private sealed class HighResolutionTimer : IDisposable
    {
        private readonly bool _active;

        private HighResolutionTimer(bool active) => _active = active;

        public static HighResolutionTimer Begin() =>
            new(OperatingSystem.IsWindows() && timeBeginPeriod(1) == 0);

        public void Dispose()
        {
            if (_active)
                timeEndPeriod(1);
        }

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);
    }
}
