using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text.Json;
using AgrusScanner.Models;
using AgrusScanner.Services;
using ModelContextProtocol.Server;

namespace AgrusScanner.Mcp;

[McpServerToolType]
public class ScannerMcpTools
{
    private static readonly PingScanner _ping = new();
    private static readonly PortScanner _portScanner = new();
    private static readonly DnsResolver _dns = new();
    private static readonly AiServiceProber _aiProber = new();
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private static readonly List<HostScanRecord> _lastResults = [];

    private record HostScanRecord(
        string Ip, string Hostname, bool Alive, long? PingMs,
        List<PortResult> OpenPorts, List<AiServiceResult> AiServices);

    [McpServerTool(Name = "scan_network"), Description("Scan an IP range: ping sweep, port scan, DNS resolution, and optional AI/ML service detection. Returns JSON array of host results.")]
    public static async Task<string> ScanNetwork(
        [Description("IP range in CIDR (192.168.1.0/24) or range (10.0.0.1-254) format")] string ip_range,
        [Description("Port preset: quick, common, extended, ai, or none (default: quick)")] string preset = "quick",
        [Description("Scan all IPs regardless of ping response (default: false)")] bool skip_ping = false,
        CancellationToken cancellationToken = default)
    {
        var addresses = IpRangeParser.Parse(ip_range);
        var ports = GetPortsForPreset(preset);
        var runAiProbe = preset.Equals("ai", StringComparison.OrdinalIgnoreCase);
        var results = new List<HostScanRecord>();
        var lockObj = new object();

        // Ping sweep
        var aliveHosts = new List<(IPAddress ip, long? ms)>();
        await _ping.SweepAsync(addresses, 1000, 256, (ip, alive, ms) =>
        {
            if (alive || skip_ping)
                lock (lockObj) { aliveHosts.Add((ip, alive ? ms : null)); }
        }, cancellationToken);

        // Scan each host
        var tasks = aliveHosts.Select(async h =>
        {
            var result = await ScanSingleHost(h.ip, h.ms, ports, runAiProbe, cancellationToken);
            lock (lockObj) { results.Add(result); }
        });
        await Task.WhenAll(tasks);

        lock (lockObj)
        {
            _lastResults.Clear();
            _lastResults.AddRange(results);
        }

        return JsonSerializer.Serialize(results.Select(FormatRecord), _json);
    }

    [McpServerTool(Name = "probe_host"), Description("Deep-scan a single IP address: port scan and AI/ML service detection. Returns JSON object with full host detail.")]
    public static async Task<string> ProbeHost(
        [Description("Single IP address to probe")] string ip,
        [Description("Comma-separated ports, or preset name: quick, common, extended, ai (default: ai)")] string ports = "ai",
        CancellationToken cancellationToken = default)
    {
        var address = IPAddress.Parse(ip);

        // Resolve port list
        int[] portList;
        if (int.TryParse(ports.Split(',')[0].Trim(), out _))
        {
            portList = ports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var p) ? p : -1)
                .Where(p => p > 0 && p <= 65535)
                .ToArray();
        }
        else
        {
            portList = GetPortsForPreset(ports);
        }

        var runAiProbe = ports.Equals("ai", StringComparison.OrdinalIgnoreCase)
            || portList.Intersect(ScanConfig.AiPorts).Any();

        // Ping
        var (alive, ms) = await _ping.PingAsync(address, 1000, cancellationToken);
        var result = await ScanSingleHost(address, alive ? ms : null, portList, runAiProbe, cancellationToken);

        lock (_lastResults)
        {
            _lastResults.Clear();
            _lastResults.Add(result);
        }

        return JsonSerializer.Serialize(FormatRecord(result), _json);
    }

    [McpServerTool(Name = "export_results"), Description("Export the last scan_network or probe_host results to a file. Supports JSON and CSV formats.")]
    public static async Task<string> ExportResults(
        [Description("Output file path (e.g. scan-results.json or scan-results.csv)")] string file_path,
        [Description("Format: json or csv (default: inferred from file extension, falls back to json)")] string format = "auto",
        CancellationToken cancellationToken = default)
    {
        if (_lastResults.Count == 0)
            return """{"error": "No scan results to export. Run scan_network or probe_host first."}""";

        var fmt = format.ToLowerInvariant();
        if (fmt == "auto")
            fmt = file_path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "json";

        var fullPath = Path.GetFullPath(file_path);

        if (fmt == "csv")
        {
            var lines = new List<string>
            {
                "ip,hostname,alive,ping_ms,open_ports,ai_services"
            };
            foreach (var host in _lastResults)
            {
                var portsStr = string.Join(";", host.OpenPorts.Select(p => $"{p.Port}/{p.ServiceName}"));
                var aiStr = string.Join(";", host.AiServices.Select(a => $"{a.ServiceName}:{a.Port}"));
                lines.Add($"{host.Ip},{Csv(host.Hostname)},{host.Alive},{host.PingMs?.ToString() ?? ""},{Csv(portsStr)},{Csv(aiStr)}");
            }
            await File.WriteAllLinesAsync(fullPath, lines, cancellationToken);
        }
        else
        {
            var json = JsonSerializer.Serialize(_lastResults.Select(h => new
            {
                ip = h.Ip,
                hostname = h.Hostname,
                alive = h.Alive,
                ping_ms = h.PingMs,
                open_ports = h.OpenPorts.Select(p => new { p.Port, service = p.ServiceName }).ToArray(),
                ai_services = h.AiServices.Select(a => new
                {
                    service = a.ServiceName,
                    category = a.Category,
                    port = a.Port,
                    confidence = a.Confidence,
                    details = a.Details
                }).ToArray()
            }), _json);
            await File.WriteAllTextAsync(fullPath, json, cancellationToken);
        }

        return JsonSerializer.Serialize(new { exported = _lastResults.Count, path = fullPath, format = fmt }, _json);
    }

    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    [McpServerTool(Name = "list_presets"), Description("List available scan presets with their port counts and port numbers.")]
    public static string ListPresets()
    {
        var presets = new[]
        {
            new { name = "quick", port_count = ScanConfig.QuickPorts.Length, ports = ScanConfig.QuickPorts },
            new { name = "common", port_count = ScanConfig.CommonPorts.Length, ports = ScanConfig.CommonPorts },
            new { name = "extended", port_count = ScanConfig.ExtendedPorts.Length, ports = ScanConfig.ExtendedPorts },
            new { name = "ai", port_count = ScanConfig.AiPorts.Length, ports = ScanConfig.AiPorts },
            new { name = "none", port_count = 0, ports = Array.Empty<int>() }
        };
        return JsonSerializer.Serialize(presets, _json);
    }

    private static int[] GetPortsForPreset(string preset) => preset.ToLowerInvariant() switch
    {
        "quick" => ScanConfig.QuickPorts,
        "common" => ScanConfig.CommonPorts,
        "extended" => ScanConfig.ExtendedPorts,
        "ai" => ScanConfig.AiPorts,
        "none" => [],
        _ => ScanConfig.QuickPorts
    };

    private static async Task<HostScanRecord> ScanSingleHost(
        IPAddress address, long? pingMs, int[] ports, bool runAiProbe, CancellationToken ct)
    {
        var ip = address.ToString();
        var hostname = await _dns.ResolveAsync(address, ct);

        List<PortResult> openPorts = [];
        if (ports.Length > 0)
            openPorts = await _portScanner.ScanAsync(address, ports, 2000, 64, ct);

        List<AiServiceResult> aiServices = [];
        if (runAiProbe && openPorts.Count > 0)
            aiServices = await _aiProber.ProbeAllAsync(ip, openPorts.Select(p => p.Port).ToArray(), ct);

        return new HostScanRecord(ip, hostname, pingMs.HasValue, pingMs, openPorts, aiServices);
    }

    private static object FormatRecord(HostScanRecord h) => new
    {
        ip = h.Ip,
        hostname = h.Hostname,
        alive = h.Alive,
        ping_ms = h.PingMs,
        open_ports = h.OpenPorts.Select(p => new { p.Port, service = p.ServiceName }).ToArray(),
        ai_services = h.AiServices.Select(a => new
        {
            service = a.ServiceName,
            category = a.Category,
            port = a.Port,
            confidence = a.Confidence,
            details = a.Details
        }).ToArray()
    };
}
