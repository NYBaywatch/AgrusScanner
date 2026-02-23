using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace AgrusScanner.Services;

public record UpdateInfo(string TagName, string HtmlUrl);

public static class UpdateChecker
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static UpdateChecker()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("AgrusScanner/1.0");
    }

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var resp = await Http.GetFromJsonAsync<GitHubRelease>(
                "https://api.github.com/repos/NYBaywatch/AgrusScanner/releases/latest");

            if (resp is null || string.IsNullOrEmpty(resp.TagName))
                return null;

            var remote = ParseVersion(resp.TagName);
            var local = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

            if (remote > local)
                return new UpdateInfo(resp.TagName, resp.HtmlUrl ?? $"https://github.com/NYBaywatch/AgrusScanner/releases/tag/{resp.TagName}");

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

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }
}
