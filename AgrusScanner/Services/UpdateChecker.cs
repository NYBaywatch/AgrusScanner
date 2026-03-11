using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace AgrusScanner.Services;

public record UpdateInfo(string TagName, string HtmlUrl);

public static class UpdateChecker
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly string AppVersion;
    private static readonly string OsInfo;

    static UpdateChecker()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("AgrusScanner/1.0");

        var ver = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        AppVersion = $"{ver.Major}.{ver.Minor}.{ver.Build}";

        var osVer = Environment.OSVersion.Version;
        OsInfo = $"win{osVer.Major}.{osVer.Minor}.{osVer.Build}";
    }

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var url = $"https://api.jpftech.com/agrus/update-check?v={AppVersion}&os={OsInfo}";
            var resp = await Http.GetFromJsonAsync<UpdateCheckResponse>(url);

            if (resp is null || string.IsNullOrEmpty(resp.LatestVersion))
                return null;

            var remote = ParseVersion(resp.TagName ?? resp.LatestVersion);
            var local = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

            if (remote > local)
                return new UpdateInfo(
                    resp.TagName ?? resp.LatestVersion,
                    resp.ReleaseUrl ?? $"https://github.com/NYBaywatch/AgrusScanner/releases/tag/{resp.TagName}");

            return null;
        }
        catch
        {
            return null; // fail silently
        }
    }

    private static Version ParseVersion(string tag)
    {
        var cleaned = tag.TrimStart('v', 'V');
        return Version.TryParse(cleaned, out var v) ? v : new Version(0, 0, 0);
    }

    private class UpdateCheckResponse
    {
        [JsonPropertyName("latest_version")]
        public string LatestVersion { get; set; } = "";

        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("release_url")]
        public string? ReleaseUrl { get; set; }
    }
}
