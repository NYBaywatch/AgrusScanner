namespace AgrusScanner.Models;

public class ScanProgress
{
    public int Completed { get; set; }
    public int Total { get; set; }
    public int AliveCount { get; set; }
    public double ElapsedSeconds { get; set; }

    public double Percentage => Total > 0 ? (double)Completed / Total * 100 : 0;
}
