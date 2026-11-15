using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace DualStrikeMapper.Core.Updates;

/// <summary>A release on GitHub that is newer than the running app.</summary>
public sealed record UpdateInfo(Version Version, string Tag, string PageUrl);

/// <summary>
/// Asks GitHub for the newest release and compares it with the version this app was built as.
/// Release builds get their version from make-release.ps1 (-Version 1.2.0), and the tag on GitHub
/// is the same number with a "v" in front.
/// </summary>
public static class UpdateChecker
{
    public const string ReleasesPage = "https://github.com/Elias-Eleven/Sidewinder-Dual-Strike-Input-Mapper/releases";
    private const string LatestReleaseApi = "https://api.github.com/repos/Elias-Eleven/Sidewinder-Dual-Strike-Input-Mapper/releases/latest";

    /// <summary>The running app's version, trimmed to three parts like the release tags.</summary>
    // Has to come before Http, since the client puts the version in its User-Agent.
    public static Version CurrentVersion { get; } = Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    private static readonly HttpClient Http = CreateClient();

    /// <summary>
    /// Returns the newer release, or null when we're up to date.
    /// Throws when GitHub can't be reached, so a manual check can say so.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancel = default)
    {
        using var response = await Http.GetAsync(LatestReleaseApi, cancel);
        // 404 just means there's no published release yet.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);
        var root = json.RootElement;

        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var page = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? ReleasesPage : ReleasesPage;

        if (!TryParseTag(tag, out var latest) || latest <= CurrentVersion)
            return null;
        return new UpdateInfo(latest, tag, page);
    }

    /// <summary>Opens a web page in the default browser.</summary>
    public static void OpenInBrowser(string url)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        else if (OperatingSystem.IsMacOS())
            Process.Start("open", url);
        else
            Process.Start("xdg-open", url);
    }

    /// <summary>Reads tags like "v1.2.0" or "1.2". Anything after a dash (like "-beta") is ignored.</summary>
    public static bool TryParseTag(string tag, out Version version)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOf('-');
        if (dash >= 0)
            text = text[..dash];
        // Version.TryParse wants at least "1.0", so a bare "2" gets a ".0" added.
        if (!text.Contains('.'))
            text += ".0";
        if (Version.TryParse(text, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }
        version = new Version(0, 0, 0);
        return false;
    }

    // 1.2 and 1.2.0.0 should both count as 1.2.0, or the comparison treats them as different.
    private static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub turns away requests that don't say who they are.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DualStrikeMapper", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
