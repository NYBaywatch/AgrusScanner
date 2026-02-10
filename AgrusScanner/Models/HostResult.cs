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

    public string PortsDisplay =>
        OpenPorts.Count > 0
            ? string.Join(", ", OpenPorts.Select(p => $"{p.Port}/{p.ServiceName}"))
            : "-";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
