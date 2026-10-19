using Avalonia;
using System;

namespace DualStrikeMapper.App;

sealed class Program
{
    // Initialization code. Don't use Avalonia, third-party APIs, or
    // SynchronizationContext-reliant code before AppMain is called: things aren't
    // initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // If the app is already running, show its window and exit.
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            SingleInstance.ShowRunningCopy();
            return;
        }

        Instance = instance;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Holds this copy's single-instance lock.</summary>
    internal static SingleInstance? Instance { get; private set; }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
