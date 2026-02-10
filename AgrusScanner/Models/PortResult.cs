namespace AgrusScanner.Models;

public class PortResult
{
    public int Port { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
}
