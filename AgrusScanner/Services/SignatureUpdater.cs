using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using AgrusScanner.Models;

namespace AgrusScanner.Services;

/// <summary>What the feed currently offers, when it is newer than what is loaded.</summary>
public record SignatureUpdateInfo(string SigVersion, string Url, string Sha256);

/// <summary>
/// Checks the signature feed and installs new packages. The feed is a rolling GitHub release
/// tagged "signatures" holding latest.json + latest.agsig, published by .github/workflows/signatures.yml.
///
/// Trust does not depend on this transport: whatever is downloaded goes through
/// <see cref="SignatureStore.TryInstall"/>, which rejects anything not signed by the CI key.
/// </summary>
public static class SignatureUpdater
{
    public const string FeedBase = "https://github.com/NYBaywatch/AgrusScanner/releases/download/signatures/";
    private const string ManifestUrl = FeedBase + "latest.json";
    private const long MaxPackageBytes = 4 * 1024 * 1024;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    static SignatureUpdater()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        Http.DefaultRequestHeaders.UserAgent.ParseAdd($"AgrusScanner/{v.ToString(3)}");
    }

    /// <summary>Returns feed info if the feed has a newer signature version than the active catalog, else null.</summary>
    public static async Task<SignatureUpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var manifest = await Http.GetFromJsonAsync<Manifest>(ManifestUrl + "?t=" + DateTime.UtcNow.Ticks, ct);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.SigVersion)) return null;

            var active = SignatureStore.Current.SourceVersion;
            if (active != "embedded" && SignaturePackage.CompareSigVersions(manifest.SigVersion, active) <= 0)
                return null;

            // Skip feeds that need a newer app; the store would reject them anyway.
            var appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            if (!string.IsNullOrWhiteSpace(manifest.MinAppVersion) && SignaturePackage.ParseVersion(manifest.MinAppVersion) > appVersion)
                return null;

            // The manifest may only point inside the feed release; anything else is ignored.
            var url = string.IsNullOrWhiteSpace(manifest.Url) ? FeedBase + "latest.agsig" : manifest.Url;
            if (!url.StartsWith(FeedBase, StringComparison.Ordinal)) url = FeedBase + "latest.agsig";
            return new SignatureUpdateInfo(manifest.SigVersion, url, manifest.Sha256 ?? "");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SignatureUpdater] check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Downloads, verifies, and activates the package. Returns false with a reason on any failure.</summary>
    public static async Task<(bool ok, string error)> DownloadAndInstallAsync(SignatureUpdateInfo info, CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return (false, $"download failed: HTTP {(int)response.StatusCode}");
            if (response.Content.Headers.ContentLength is > MaxPackageBytes) return (false, "package too large");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int n;
            while ((n = await stream.ReadAsync(buffer, ct)) > 0)
            {
                ms.Write(buffer, 0, n);
                if (ms.Length > MaxPackageBytes) return (false, "package too large");
            }
            var bytes = ms.ToArray();

            if (!string.IsNullOrWhiteSpace(info.Sha256))
            {
                var actual = Convert.ToHexString(SHA256.HashData(bytes));
                if (!actual.Equals(info.Sha256, StringComparison.OrdinalIgnoreCase)) return (false, "download hash mismatch");
            }

            return SignatureStore.TryInstall(bytes, out var error) ? (true, "") : (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private sealed class Manifest
    {
        [JsonPropertyName("sig_version")] public string? SigVersion { get; set; }
        [JsonPropertyName("min_app_version")] public string? MinAppVersion { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("probe_count")] public int? ProbeCount { get; set; }
    }
}

public enum SignatureUpdateMode
{
    Off = 0,
    Notify = 1,
    Auto = 2
}
