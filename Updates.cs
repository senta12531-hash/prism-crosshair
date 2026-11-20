using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Prism;

internal static class Updates
{
    private const string Repository = "senta12531-hash/prism-crosshair";
    internal const string AssetName = "Prism-win-x64.zip";
    internal const string ReleasesUrl = "https://github.com/" + Repository + "/releases";

    internal static Version CurrentVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
    internal static string VersionLabel => "v" + CurrentVersion.ToString(3);

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };

    internal sealed record Update(Version Version, Uri Download);

    internal static async Task<Update?> CheckAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/repos/" + Repository + "/releases/latest");
        request.Headers.UserAgent.ParseAdd("Prism/" + CurrentVersion.ToString(3));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await Client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return ReadUpdate(json.RootElement, CurrentVersion);
    }

    internal static Update? ReadUpdate(JsonElement release, Version current)
    {
        var tag = release.GetProperty("tag_name").GetString();
        if (!Version.TryParse(tag?.TrimStart('v', 'V'), out var version))
            return null;

        version = new Version(version.Major, version.Minor, Math.Max(0, version.Build));
        current = new Version(current.Major, current.Minor, Math.Max(0, current.Build));
        if (version <= current)
            return null;

        var asset = release.GetProperty("assets").EnumerateArray().FirstOrDefault(item =>
            item.GetProperty("name").GetString() == AssetName &&
            item.GetProperty("state").GetString() == "uploaded");
        if (asset.ValueKind == JsonValueKind.Undefined)
            return null;

        var url = asset.GetProperty("browser_download_url").GetString();
        var expectedPrefix = "/" + Repository + "/releases/download/";
        return Uri.TryCreate(url, UriKind.Absolute, out var download) &&
            download.Scheme == Uri.UriSchemeHttps && download.Host == "github.com" &&
            download.AbsolutePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
            ? new Update(version, download)
            : null;
    }

    internal static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
