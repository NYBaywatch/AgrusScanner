using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AgrusScanner.Models;

public class HostResult : INotifyPropertyChanged
{
    private string _ipAddress = string.Empty;
    private string _hostname = string.Empty;
    private long? _pingMs;
    private bool _isAlive;
    private ObservableCollection<PortResult> _openPorts = [];
    private string _aiService = string.Empty;
    private List<AiServiceResult> _aiResults = [];

    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(); }
    }

    public string Hostname
    {
        get => _hostname;
        set { _hostname = value; OnPropertyChanged(); }
    }

    public long? PingMs
    {
        get => _pingMs;
        set { _pingMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(PingDisplay)); }
    }

    public string PingDisplay => PingMs.HasValue ? $"{PingMs}ms" : "-";

    public bool IsAlive
    {
        get => _isAlive;
        set { _isAlive = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
    }

    public string Status => IsAlive ? "Alive" : "Dead";

    public ObservableCollection<PortResult> OpenPorts
    {
        get => _openPorts;
        set { _openPorts = value; OnPropertyChanged(); OnPropertyChanged(nameof(PortsDisplay)); }
    }

    public string AiService
    {
        get => _aiService;
        set { _aiService = value; OnPropertyChanged(); }
    }

    public List<AiServiceResult> AiResults
    {
        get => _aiResults;
        set
        {
            _aiResults = value;
            OnPropertyChanged();
            // Build the display string from all detected services
            AiService = FormatAiServices(value);
        }
    }

    private static string FormatAiServices(List<AiServiceResult> results)
    {
        if (results.Count == 0) return "";

        var parts = new List<string>();
        foreach (var r in results)
        {
            var entry = $"[{r.Category}] {r.ServiceName} :{r.Port}";
            if (!string.IsNullOrEmpty(r.Details))
                entry += $" ({r.Details})";
            parts.Add(entry);
        }
        return string.Join(" | ", parts);
    }

    public string PortsDisplay =>
        OpenPorts.Count > 0
            ? string.Join(", ", OpenPorts.Select(p => $"{p.Port}/{p.ServiceName}"))
            : "-";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
