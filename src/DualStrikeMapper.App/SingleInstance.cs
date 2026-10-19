using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DualStrikeMapper.App;

/// <summary>
/// Lets only one copy of the app run. Opening it again shows the running
/// copy's window instead.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private static readonly string Id = "DualStrikeMapper-" + Environment.UserName;

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Null when another copy is already running.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, @"Local\" + Id, out var createdNew);
        if (createdNew)
            return new SingleInstance(mutex);
        mutex.Dispose();
        return null;
    }

    /// <summary>Runs <paramref name="onShowRequested"/> on a background thread when another copy asks to show the window.</summary>
    public void Listen(Action onShowRequested) => Task.Run(async () =>
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(Id, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token);
                onShowRequested();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                await Task.Delay(500);
            }
        }
    });

    /// <summary>Asks the running copy to show its window.</summary>
    public static void ShowRunningCopy()
    {
        // Windows only lets the app the user just launched bring a window to the front.
        // Hand that right on, or the running copy's window would just flash in the taskbar.
        if (OperatingSystem.IsWindows())
            AllowSetForegroundWindow(AsfwAny);

        try
        {
            using var client = new NamedPipeClientStream(".", Id, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(2000);
        }
        catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // The running copy is busy starting up or shutting down. Nothing more to do.
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _mutex.Dispose();
    }

    private const int AsfwAny = -1;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
