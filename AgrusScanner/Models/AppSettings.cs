namespace AgrusScanner.Models;

public class AppSettings
{
    public bool SkipPing { get; set; }

    // Extra ports added by user (per preset)
    public int[] QuickExtraPorts { get; set; } = [];
    public int[] CommonExtraPorts { get; set; } = [];
    public int[] ExtendedExtraPorts { get; set; } = [];
    public int[] AiExtraPorts { get; set; } = [];

    // Ports removed by user (per preset, except AI)
    public int[] QuickRemovedPorts { get; set; } = [];
    public int[] CommonRemovedPorts { get; set; } = [];
    public int[] ExtendedRemovedPorts { get; set; } = [];

    // Built-in MCP server (only runs under --mcp-only). When disabled, that mode refuses to start.
    public bool McpServerEnabled { get; set; } = true;
    public int McpPort { get; set; } = 8999;

    // After an MCP server answers initialize, also call tools/list (read-only) to show its tool names.
    // Off by default because it goes a step beyond a bare handshake.
    public bool EnumerateMcpTools { get; set; }

    // Update checking (set false to disable update check pings)
    public bool CheckForUpdates { get; set; } = true;

    // Detection signature feed: Off = never check, Notify = show banner, Auto = download + install (default)
    public Services.SignatureUpdateMode SignatureUpdates { get; set; } = Services.SignatureUpdateMode.Auto;
    public int SignatureCheckIntervalHours { get; set; } = 24;
}
