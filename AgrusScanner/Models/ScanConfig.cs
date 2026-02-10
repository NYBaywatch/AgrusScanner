namespace AgrusScanner.Models;

public class ScanConfig
{
    public string IpRange { get; set; } = string.Empty;
    public int PingTimeoutMs { get; set; } = 1000;
    public int PortTimeoutMs { get; set; } = 2000;
    public int MaxConcurrency { get; set; } = 256;
    public int[] PortsToScan { get; set; } = [];

    public static readonly int[] QuickPorts = [80, 443, 22, 21, 3389, 8080];

    public static readonly int[] CommonPorts = [
        20, 21, 22, 23, 25, 53, 80, 110, 111, 135, 139, 143,
        443, 445, 993, 995, 1723, 3306, 3389, 5900, 8080, 8443
    ];

    public static readonly int[] AiPorts = [
        11434, 8000, 8080, 3000, 4000, 1234, 5000, 3001,
        7860, 5001, 8443, 4891, 1337, 3080
    ];

    public static readonly int[] ExtendedPorts = [
        20, 21, 22, 23, 25, 53, 67, 68, 69, 80, 110, 111, 119, 123,
        135, 137, 138, 139, 143, 161, 162, 179, 389, 443, 445, 465,
        500, 514, 515, 520, 587, 631, 636, 993, 995, 1080, 1433,
        1434, 1521, 1723, 2049, 2082, 2083, 2086, 2087, 3306, 3389,
        5432, 5900, 5901, 6379, 8080, 8443, 8888, 9090, 9200, 27017
    ];
}
