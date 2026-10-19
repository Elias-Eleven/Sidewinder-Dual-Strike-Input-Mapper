using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using DualStrikeMapper.App.ViewModels;
using DualStrikeMapper.App.Views;
using DualStrikeMapper.Core;
using DualStrikeMapper.Core.Settings;

namespace DualStrikeMapper.App;

public partial class App : Application
{
    private MapperEngine? _engine;
    private MainViewModel? _viewModel;
    private MainWindow? _window;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>The app icon as PNG, for platforms that don't use the Windows .ico.</summary>
    public static WindowIcon LoadPngIcon() =>
        new(AssetLoader.Open(new Uri("avares://DualStrikeMapper/Assets/app.png")));

    public override void OnFrameworkInitializationCompleted()
    {
        if (!OperatingSystem.IsWindows() && TrayIcon.GetIcons(this) is { } trayIcons)
            foreach (var tray in trayIcons)
                tray.Icon = LoadPngIcon();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The window hides to the tray instead of closing. Quit from the tray menu.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settings = SettingsStore.Load();
            _engine = new MapperEngine(settings.Active);
            _viewModel = new MainViewModel(settings, _engine);
            _window = new MainWindow { DataContext = _viewModel };

            if (!settings.StartMinimized)
                _window.Show();

            _engine.Start();
            Program.Instance?.Listen(() => Dispatcher.UIThread.Post(ShowWindow));
            desktop.Exit += (_, _) =>
            {
                _viewModel.SaveNow();
                _engine.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowWindow()
    {
        if (_window is null)
            return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void Quit()
    {
        if (_window is not null)
            _window.AllowClose = true;
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowWindow();

    private void OnTrayShow(object? sender, EventArgs e) => ShowWindow();

    private void OnTrayToggleMouse(object? sender, EventArgs e) => _viewModel?.ToggleMouseCommand.Execute(null);

    private void OnTrayQuit(object? sender, EventArgs e) => Quit();
}
