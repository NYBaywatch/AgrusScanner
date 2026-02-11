using System.ComponentModel;
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
        var results = new List<object>();
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

        return JsonSerializer.Serialize(results, _json);
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

        return JsonSerializer.Serialize(result, _json);
    }

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

    private static async Task<object> ScanSingleHost(
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

        return new
        {
            ip,
            hostname,
            alive = pingMs.HasValue,
            ping_ms = pingMs,
            open_ports = openPorts.Select(p => new { p.Port, service = p.ServiceName }).ToArray(),
            ai_services = aiServices.Select(a => new
            {
                service = a.ServiceName,
                category = a.Category,
                port = a.Port,
                confidence = a.Confidence,
                details = a.Details
            }).ToArray()
        };
    }
}
