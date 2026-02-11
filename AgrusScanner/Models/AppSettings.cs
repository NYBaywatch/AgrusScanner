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

    // MCP server port
    public int McpPort { get; set; } = 8999;
}
