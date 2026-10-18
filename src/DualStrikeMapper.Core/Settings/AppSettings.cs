using System.Text.Json;
using System.Text.Json.Serialization;
using DualStrikeMapper.Core.Mapping;

namespace DualStrikeMapper.Core.Settings;

public sealed class AppSettings
{
    /// <summary>Bump when built-in layouts change so saved settings pick them up.</summary>
    public const int CurrentVersion = 3;

    /// <summary>Missing in old files, so it reads as 0 and triggers an upgrade.</summary>
    public int Version { get; set; }

    public List<Profile> Profiles { get; set; } = DefaultProfiles.Create();
    public int ActiveProfile { get; set; }
    public bool MouseEnabled { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool CloseToTray { get; set; } = true;

    [JsonIgnore]
    public Profile Active
    {
        get
        {
            if (Profiles.Count == 0)
                Profiles = DefaultProfiles.Create();
            return Profiles[Math.Clamp(ActiveProfile, 0, Profiles.Count - 1)];
        }
    }
}

/// <summary>
/// Loads and saves settings.json. Keeps it next to the app when that folder is writable
/// (portable install), otherwise in the user's config folder.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FilePath { get; } = ResolvePath();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) is { } loaded)
                return Upgrade(loaded);
        }
        catch (Exception)
        {
            // A broken file shouldn't stop the app; keep a copy and start fresh.
            TryBackup();
        }
        return new AppSettings { Version = AppSettings.CurrentVersion };
    }

    /// <summary>
    /// Version 2 fixed the Dual Strike default layouts (right trigger = main click, new shooter keys);
    /// version 3 dropped the action labels so buttons show the PC input they send.
    /// Built-in layouts get the new buttons; mouse settings and custom layouts are kept.
    /// </summary>
    private static AppSettings Upgrade(AppSettings settings)
    {
        if (settings.Version < 3)
        {
            foreach (var profile in settings.Profiles)
            {
                if (DefaultProfiles.Create().FirstOrDefault(d => d.Name == profile.Name) is { } fresh)
                {
                    profile.Mappings = fresh.Mappings;
                    profile.Description = fresh.Description;
                }
            }
        }
        settings.Version = AppSettings.CurrentVersion;
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, FilePath, overwrite: true);
    }

    private static void TryBackup()
    {
        try { File.Copy(FilePath, FilePath + ".broken", overwrite: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string ResolvePath()
    {
        // Inside a macOS .app bundle a new file would break the app's signature, so never write there.
        var inAppBundle = AppContext.BaseDirectory.Replace('\\', '/').Contains(".app/Contents/", StringComparison.Ordinal);
        if (!inAppBundle && CanWrite(AppContext.BaseDirectory))
            return Path.Combine(AppContext.BaseDirectory, "settings.json");

        var dir = Path.Combine(UserConfigFolder(), "DualStrikeMapper");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    /// <summary>AppData on Windows, ~/Library/Application Support on macOS, ~/.config on Linux.</summary>
    private static string UserConfigFolder() => OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
        : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static bool CanWrite(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
