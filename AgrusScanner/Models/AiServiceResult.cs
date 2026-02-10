namespace AgrusScanner.Models;

public class AiServiceResult
{
    public string ServiceName { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Confidence { get; set; } = "low"; // high, medium, low
    public string Details { get; set; } = string.Empty;
    public int Specificity { get; set; } // higher = more specific match
}
