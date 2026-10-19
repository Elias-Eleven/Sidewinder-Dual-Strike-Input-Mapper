using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DualStrikeMapper.Core;
using DualStrikeMapper.Core.Controller;
using DualStrikeMapper.Core.Mapping;
using DualStrikeMapper.Core.Settings;

namespace DualStrikeMapper.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    /// <summary>Other mappers that would fight us for the cursor.</summary>
    private static readonly string[] ConflictingProcesses = ["DualStrikeMouse"];

    private readonly AppSettings _settings;
    private readonly MapperEngine? _engine;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _conflictTimer;
    private bool _loadingMouse;

    /// <summary>Design-time constructor for the previewer.</summary>
    public MainViewModel() : this(new AppSettings(), null)
    {
        Connected = true;
        ControllerName = "SideWinder Dual Strike";
    }

    public MainViewModel(AppSettings settings, MapperEngine? engine)
    {
        _settings = settings;
        _engine = engine;
        Editor = new ActionEditorViewModel(ApplyEditedAction);
        Callouts = new ObservableCollection<CalloutViewModel>(ControlNames.DualStrikeControls.Select(id => new CalloutViewModel(id)));

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => SaveNow();

        _conflictTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _conflictTimer.Tick += (_, _) => CheckConflicts();

        MouseEnabled = settings.MouseEnabled;
        LoadProfile();

        if (_engine is not null)
        {
            _engine.MouseEnabled = MouseEnabled;
            _engine.SnapshotUpdated += s => Dispatcher.UIThread.Post(() => ApplySnapshot(s));
            _engine.MouseEnabledChanged += on => Dispatcher.UIThread.Post(() => MouseEnabled = on);
            _engine.Error += message => Dispatcher.UIThread.Post(() => ErrorText = message);
            _conflictTimer.Start();
            CheckConflicts();
        }
    }

    public ObservableCollection<CalloutViewModel> Callouts { get; }
    public ActionEditorViewModel Editor { get; }

    [ObservableProperty] public partial bool Connected { get; set; }
    [ObservableProperty] public partial string ControllerName { get; set; } = "";
    [ObservableProperty] public partial bool MouseEnabled { get; set; }
    [ObservableProperty] public partial float HandleX { get; set; }
    [ObservableProperty] public partial float HandleY { get; set; }
    [ObservableProperty] public partial string ProfileName { get; set; } = "";
    [ObservableProperty] public partial string ProfileDescription { get; set; } = "";
    [ObservableProperty] public partial bool IsEditorOpen { get; set; }
    [ObservableProperty] public partial bool IsMouseSettingsOpen { get; set; }
    [ObservableProperty] public partial string? ConflictText { get; set; }
    [ObservableProperty] public partial string? ErrorText { get; set; }

    // Mouse tuning (mirrors the active profile's MouseSettings).
    [ObservableProperty] public partial bool IsHybrid { get; set; } = true;
    [ObservableProperty] public partial double AimSensitivity { get; set; }
    [ObservableProperty] public partial double AimZone { get; set; }
    [ObservableProperty] public partial double SpinSpeed { get; set; }
    [ObservableProperty] public partial double Deadzone { get; set; }
    [ObservableProperty] public partial double Curve { get; set; }
    [ObservableProperty] public partial double Smoothing { get; set; }
    [ObservableProperty] public partial double Momentum { get; set; }
    [ObservableProperty] public partial double PrecisionFactor { get; set; }
    [ObservableProperty] public partial bool InvertX { get; set; }
    [ObservableProperty] public partial bool InvertY { get; set; }

    public bool StartMinimized
    {
        get => _settings.StartMinimized;
        set { _settings.StartMinimized = value; OnPropertyChanged(); QueueSave(); }
    }

    public bool CloseToTray
    {
        get => _settings.CloseToTray;
        set { _settings.CloseToTray = value; OnPropertyChanged(); QueueSave(); }
    }

    public string StatusText => Connected ? ControllerName : "Plug in your controller";
    public string MouseStateText => MouseEnabled ? "Mouse ON" : "Mouse OFF";
    public string ProfilePosition => $"{_settings.ActiveProfile + 1} / {_settings.Profiles.Count}";
    public string SettingsPath => SettingsStore.FilePath;
    public bool HasConflict => ConflictText is not null;
    public bool HasError => ErrorText is not null;

    partial void OnConnectedChanged(bool value) => OnPropertyChanged(nameof(StatusText));
    partial void OnControllerNameChanged(string value) => OnPropertyChanged(nameof(StatusText));
    partial void OnConflictTextChanged(string? value) => OnPropertyChanged(nameof(HasConflict));
    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnMouseEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(MouseStateText));
        if (_engine is not null)
            _engine.MouseEnabled = value;
        _settings.MouseEnabled = value;
        QueueSave();
    }

    partial void OnProfileNameChanged(string value)
    {
        if (_settings.Active.Name == value)
            return;
        _settings.Active.Name = value;
        QueueSave();
    }

    partial void OnIsHybridChanged(bool value) => MouseChanged();
    partial void OnAimSensitivityChanged(double value) => MouseChanged();
    partial void OnAimZoneChanged(double value) => MouseChanged();
    partial void OnSpinSpeedChanged(double value) => MouseChanged();
    partial void OnDeadzoneChanged(double value) => MouseChanged();
    partial void OnCurveChanged(double value) => MouseChanged();
    partial void OnSmoothingChanged(double value) => MouseChanged();
    partial void OnMomentumChanged(double value) => MouseChanged();
    partial void OnPrecisionFactorChanged(double value) => MouseChanged();
    partial void OnInvertXChanged(bool value) => MouseChanged();
    partial void OnInvertYChanged(bool value) => MouseChanged();

    [RelayCommand]
    private void ToggleMouse() => MouseEnabled = !MouseEnabled;

    [RelayCommand]
    private void NextProfile() => SwitchProfile(+1);

    [RelayCommand]
    private void PreviousProfile() => SwitchProfile(-1);

    [RelayCommand]
    private void Select(CalloutViewModel callout)
    {
        foreach (var c in Callouts)
            c.IsSelected = c == callout;
        Editor.Load(callout.ControlName, callout.Chip, _settings.Active.Get(callout.Id));
        IsMouseSettingsOpen = false;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void OpenMouseSettings()
    {
        IsEditorOpen = false;
        IsMouseSettingsOpen = true;
        foreach (var c in Callouts)
            c.IsSelected = false;
    }

    [RelayCommand]
    private void ClosePanels()
    {
        IsEditorOpen = false;
        IsMouseSettingsOpen = false;
        foreach (var c in Callouts)
            c.IsSelected = false;
    }

    // ---- Layouts: + button, click-to-rename, ⋯ menu ----

    [ObservableProperty] public partial bool IsRenaming { get; set; }
    [ObservableProperty] public partial string RenameText { get; set; } = "";
    [ObservableProperty] public partial bool IsConfirmingDelete { get; set; }

    public bool CanDeleteProfile => _settings.Profiles.Count > 1;

    public string DeleteConfirmText => $"Delete the layout \"{ProfileName}\"? This can't be undone.";

    partial void OnIsConfirmingDeleteChanged(bool value) => OnPropertyChanged(nameof(DeleteConfirmText));

    /// <summary>The built-in layout this one started from, so "reset" knows what to go back to.</summary>
    private Profile? DefaultFor(Profile profile)
    {
        var origin = profile.BasedOn ?? profile.Name;
        return DefaultProfiles.Create().FirstOrDefault(p => p.Name == origin);
    }

    public bool CanResetProfile => DefaultFor(_settings.Active) is not null;

    /// <summary>"+": a new layout, copied from the current one.</summary>
    [RelayCommand]
    private void AddProfile()
    {
        var source = _settings.Active;
        var copy = source.Clone();
        copy.BasedOn = source.BasedOn ?? source.Name;
        copy.Name = UniqueName("Layout");
        copy.Description = $"Based on {source.Name}.";
        _settings.Profiles.Insert(_settings.ActiveProfile + 1, copy);
        _settings.ActiveProfile++;
        LoadProfile();
        QueueSave();
        StartRename();
    }

    [RelayCommand]
    private void DuplicateProfile()
    {
        var source = _settings.Active;
        var copy = source.Clone();
        copy.BasedOn = source.BasedOn ?? source.Name;
        copy.Name = UniqueName(source.Name + " copy");
        _settings.Profiles.Insert(_settings.ActiveProfile + 1, copy);
        _settings.ActiveProfile++;
        LoadProfile();
        QueueSave();
    }

    [ObservableProperty] public partial string RenameDescription { get; set; } = "";

    /// <summary>Opens the footer editor for the layout's name and description.</summary>
    [RelayCommand]
    private void StartRename()
    {
        RenameText = ProfileName;
        RenameDescription = ProfileDescription;
        IsRenaming = true;
    }

    [RelayCommand]
    private void CommitRename()
    {
        if (!IsRenaming)
            return;
        IsRenaming = false;

        var name = RenameText.Trim();
        if (name.Length > 0 && name != ProfileName)
            ProfileName = _settings.Profiles.Any(p => p != _settings.Active && p.Name == name) ? UniqueName(name) : name;

        var description = RenameDescription.Trim();
        if (description != ProfileDescription)
        {
            ProfileDescription = description;
            _settings.Active.Description = description;
            QueueSave();
        }
    }

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;

    [RelayCommand(CanExecute = nameof(CanResetProfile))]
    private void ResetProfile()
    {
        if (DefaultFor(_settings.Active) is not { } fresh)
            return;
        var current = _settings.Active;
        fresh.Name = current.Name;
        fresh.BasedOn = current.BasedOn;
        if (current.BasedOn is not null)
            fresh.Description = current.Description;
        _settings.Profiles[_settings.ActiveProfile] = fresh;
        LoadProfile();
        QueueSave();
    }

    /// <summary>Delete asks first; the banner calls <see cref="DeleteProfile"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private void AskDeleteProfile() => IsConfirmingDelete = true;

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    [RelayCommand]
    private void DeleteProfile()
    {
        IsConfirmingDelete = false;
        if (_settings.Profiles.Count <= 1)
            return;
        _settings.Profiles.RemoveAt(_settings.ActiveProfile);
        _settings.ActiveProfile = Math.Min(_settings.ActiveProfile, _settings.Profiles.Count - 1);
        LoadProfile();
        QueueSave();
    }

    private string UniqueName(string baseName)
    {
        bool Taken(string n) => _settings.Profiles.Any(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
        if (baseName != "Layout" && !Taken(baseName))
            return baseName;
        for (var i = baseName == "Layout" ? _settings.Profiles.Count + 1 : 2; ; i++)
            if (!Taken($"{baseName} {i}"))
                return $"{baseName} {i}";
    }

    [RelayCommand]
    private void CloseConflicts()
    {
        foreach (var name in ConflictingProcesses)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try { process.Kill(); }
                catch (Exception e) { ErrorText = $"Couldn't close {name}: {e.Message}"; }
            }
        }
        CheckConflicts();
    }

    [RelayCommand]
    private void DismissError() => ErrorText = null;

    public void SaveNow()
    {
        _saveTimer.Stop();
        try
        {
            SettingsStore.Save(_settings);
        }
        catch (Exception e)
        {
            ErrorText = $"Couldn't save settings: {e.Message}";
        }
    }

    private void SwitchProfile(int step)
    {
        var count = _settings.Profiles.Count;
        _settings.ActiveProfile = ((_settings.ActiveProfile + step) % count + count) % count;
        LoadProfile();
        QueueSave();
    }

    private void LoadProfile()
    {
        IsRenaming = false;
        IsConfirmingDelete = false;
        OnPropertyChanged(nameof(CanDeleteProfile));
        OnPropertyChanged(nameof(CanResetProfile));
        ResetProfileCommand.NotifyCanExecuteChanged();
        AskDeleteProfileCommand.NotifyCanExecuteChanged();

        var profile = _settings.Active;
        ProfileName = profile.Name;
        ProfileDescription = profile.Description;
        OnPropertyChanged(nameof(ProfilePosition));
        foreach (var c in Callouts)
            c.ActionText = ActionText.Describe(profile.Get(c.Id));

        _loadingMouse = true;
        var m = profile.Mouse;
        IsHybrid = m.Mode == MouseMode.Hybrid;
        AimSensitivity = m.AimSensitivity;
        AimZone = m.AimZone;
        SpinSpeed = m.SpinSpeed;
        Deadzone = m.Deadzone;
        Curve = m.Curve;
        Smoothing = m.Smoothing;
        Momentum = m.Momentum;
        PrecisionFactor = m.PrecisionFactor;
        InvertX = m.InvertX;
        InvertY = m.InvertY;
        _loadingMouse = false;

        if (IsEditorOpen && Callouts.FirstOrDefault(c => c.IsSelected) is { } selected)
            Editor.Load(selected.ControlName, selected.Chip, profile.Get(selected.Id));

        _engine?.SetProfile(profile);
    }

    private void MouseChanged()
    {
        if (_loadingMouse)
            return;
        var m = _settings.Active.Mouse;
        m.Mode = IsHybrid ? MouseMode.Hybrid : MouseMode.Joystick;
        m.AimSensitivity = AimSensitivity;
        m.AimZone = AimZone;
        m.SpinSpeed = SpinSpeed;
        m.Deadzone = Deadzone;
        m.Curve = Curve;
        m.Smoothing = Smoothing;
        m.Momentum = Momentum;
        m.PrecisionFactor = PrecisionFactor;
        m.InvertX = InvertX;
        m.InvertY = InvertY;
        _engine?.SetProfile(_settings.Active);
        QueueSave();
    }

    private void ApplyEditedAction(ControlAction action)
    {
        if (Callouts.FirstOrDefault(c => c.IsSelected) is not { } selected)
            return;
        _settings.Active.Mappings[selected.Id] = action;
        selected.ActionText = ActionText.Describe(action);
        _engine?.SetProfile(_settings.Active);
        QueueSave();
    }

    private void ApplySnapshot(ControllerSnapshot s)
    {
        Connected = s.Connected;
        ControllerName = s.Connected ? FriendlyName(s.Name) : "";
        // Show the handle the way the cursor will move, so inverting an axis flips the pad too.
        HandleX = InvertX ? -s.X : s.X;
        HandleY = InvertY ? -s.Y : s.Y;
        foreach (var c in Callouts)
            c.IsPressed = s.IsPressed(c.Id);
    }

    private string FriendlyName(string name) =>
        _engine?.IsDualStrike == true ? "SideWinder Dual Strike" : name;

    private void CheckConflicts()
    {
        var running = ConflictingProcesses.FirstOrDefault(n => Process.GetProcessesByName(n).Length > 0);
        ConflictText = running is null ? null : $"{running} is also running, so both apps will move the cursor.";
    }

    private void QueueSave()
    {
        if (_engine is null)
            return; // design time
        _saveTimer.Stop();
        _saveTimer.Start();
    }
}
