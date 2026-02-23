using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using AgrusScanner.Models;
using AgrusScanner.Services;

namespace AgrusScanner.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly PingScanner _pingScanner = new();
    private readonly PortScanner _portScanner = new();
    private readonly DnsResolver _dnsResolver = new();
    private readonly AiServiceProber _aiProber = new();
    private readonly SettingsService _settingsService = new();

    private AppSettings _settings;
    private string _ipRange = DetectLocalSubnet();
    private string _selectedPreset = "Quick (6 ports)";
    private bool _isScanning;
    private int _completedCount;
    private int _totalCount;
    private int _aliveCount;
    private double _elapsedSeconds;
    private CancellationTokenSource? _cts;
    private string _extraPortsText = "";
    private string _removedPortsText = "";
    private string? _updateText;
    private string? _updateUrl;
    public MainViewModel()
    {
        _settings = _settingsService.Load();
        StartCommand = new RelayCommand(async _ => await StartScanAsync(), _ => !IsScanning);
        StopCommand = new RelayCommand(_ => StopScan(), _ => IsScanning);
        ExportCommand = new RelayCommand(_ => ExportResults(), _ => CanExport);
        OpenUpdateCommand = new RelayCommand(_ =>
        {
            if (_updateUrl is not null)
                Process.Start(new ProcessStartInfo(_updateUrl) { UseShellExecute = true });
        });
        RefreshSettingsFlyout();

        ResultsView = CollectionViewSource.GetDefaultView(Results);
        ResultsView.Filter = obj => obj is HostResult h && (h.IsAlive || h.OpenPorts.Count > 0);

        // Fire-and-forget update check
        _ = CheckForUpdateAsync();
    }

    private async Task CheckForUpdateAsync()
    {
        var info = await UpdateChecker.CheckAsync();
        if (info is not null)
        {
            _updateUrl = info.HtmlUrl;
            UpdateText = $"Update {info.TagName} available";
        }
    }

    private static string DetectLocalSubnet()
    {
        try
        {
            foreach (var iface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;
                if (iface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    continue;

                foreach (var addr in iface.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        continue;

                    var ip = addr.Address.GetAddressBytes();
                    var mask = addr.IPv4Mask.GetAddressBytes();

                    // Calculate network address
                    var network = new byte[4];
                    for (int i = 0; i < 4; i++)
                        network[i] = (byte)(ip[i] & mask[i]);

                    // Count prefix bits
                    int prefix = 0;
                    foreach (var b in mask)
                        for (int bit = 7; bit >= 0; bit--)
                            if ((b & (1 << bit)) != 0) prefix++;
                            else goto done;
                    done:

                    return $"{network[0]}.{network[1]}.{network[2]}.{network[3]}/{prefix}";
                }
            }
        }
        catch { }
        return "192.168.1.0/24";
    }

    public ObservableCollection<HostResult> Results { get; } = [];

    public string IpRange
    {
        get => _ipRange;
        set { _ipRange = value; OnPropertyChanged(); }
    }

    public string SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            _selectedPreset = value;
            OnPropertyChanged();
            RefreshSettingsFlyout();
        }
    }

    public string[] Presets => ["Quick (6 ports)", "Common (22 ports)", "Extended (58 ports)", "AI Scan", "Deep AI Scan (all ports)", "No port scan"];

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            _isScanning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotScanning));
            OnPropertyChanged(nameof(CanExport));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsNotScanning => !IsScanning;

    public int CompletedCount
    {
        get => _completedCount;
        set { _completedCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(ProgressPercentage)); }
    }

    public int TotalCount
    {
        get => _totalCount;
        set { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(ProgressPercentage)); }
    }

    public int AliveCount
    {
        get => _aliveCount;
        set { _aliveCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
    }

    public double ElapsedSeconds
    {
        get => _elapsedSeconds;
        set { _elapsedSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
    }

    public double ProgressPercentage => TotalCount > 0 ? (double)CompletedCount / TotalCount * 100 : 0;

    public string ProgressText
    {
        get
        {
            if (!IsScanning && TotalCount == 0) return "Ready";
            var pct = ProgressPercentage;
            return $"{pct:F0}% | {CompletedCount}/{TotalCount} | {ElapsedSeconds:F1}s | {AliveCount} alive";
        }
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand OpenUpdateCommand { get; }

    public bool CanExport => !IsScanning && Results.Count > 0;
    public ICollectionView ResultsView { get; }

    public string? UpdateText
    {
        get => _updateText;
        set { _updateText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUpdate)); }
    }

    public bool HasUpdate => !string.IsNullOrEmpty(_updateText);

    // --- Settings properties ---

    public bool SkipPing
    {
        get => _settings.SkipPing;
        set
        {
            _settings.SkipPing = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public int McpPort
    {
        get => _settings.McpPort;
        set
        {
            if (value is >= 1 and <= 65535)
            {
                _settings.McpPort = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public bool IsAiPresetSelected => SelectedPreset is "AI Scan" or "Deep AI Scan (all ports)";

    public bool IsNotAiPresetSelected => !IsAiPresetSelected;

    public bool HasPortPreset => SelectedPreset != "No port scan";

    public string BuiltInPortsDisplay
    {
        get
        {
            if (IsDeepScan) return "1\u201365535 (all ports)";
            var basePorts = GetBasePortsForPreset();
            if (basePorts.Length == 0) return "None";
            return string.Join(", ", basePorts);
        }
    }

    public string ExtraPortsText
    {
        get => _extraPortsText;
        set
        {
            _extraPortsText = value;
            OnPropertyChanged();
            ApplyExtraPorts(value);
        }
    }

    public string RemovedPortsText
    {
        get => _removedPortsText;
        set
        {
            _removedPortsText = value;
            OnPropertyChanged();
            ApplyRemovedPorts(value);
        }
    }

    public string EffectivePortCountDisplay
    {
        get
        {
            var ports = GetPortsForPreset();
            return $"Effective: {ports.Length} ports";
        }
    }

    public string PresetDisplayName => SelectedPreset switch
    {
        "Quick (6 ports)" => "Quick",
        "Common (22 ports)" => "Common",
        "Extended (58 ports)" => "Extended",
        "AI Scan" => "AI Scan",
        "Deep AI Scan (all ports)" => "Deep AI Scan",
        "No port scan" => "No port scan",
        _ => SelectedPreset
    };

    // --- Port logic ---

    private int[] GetBasePortsForPreset() => SelectedPreset switch
    {
        "Quick (6 ports)" => ScanConfig.QuickPorts,
        "Common (22 ports)" => ScanConfig.CommonPorts,
        "Extended (58 ports)" => ScanConfig.ExtendedPorts,
        "AI Scan" => ScanConfig.AiPorts,
        "Deep AI Scan (all ports)" => ScanConfig.AllPorts,
        _ => []
    };

    private int[] GetExtraPortsForPreset() => SelectedPreset switch
    {
        "Quick (6 ports)" => _settings.QuickExtraPorts,
        "Common (22 ports)" => _settings.CommonExtraPorts,
        "Extended (58 ports)" => _settings.ExtendedExtraPorts,
        "AI Scan" => _settings.AiExtraPorts,
        _ => [] // Deep AI Scan and No port scan have no customization
    };

    private int[] GetRemovedPortsForPreset() => SelectedPreset switch
    {
        "Quick (6 ports)" => _settings.QuickRemovedPorts,
        "Common (22 ports)" => _settings.CommonRemovedPorts,
        "Extended (58 ports)" => _settings.ExtendedRemovedPorts,
        _ => [] // AI has no removals
    };

    private int[] GetPortsForPreset()
    {
        var basePorts = GetBasePortsForPreset();
        var extra = GetExtraPortsForPreset();
        var removed = GetRemovedPortsForPreset();

        var result = basePorts
            .Concat(extra)
            .Except(removed)
            .Distinct()
            .OrderBy(p => p)
            .ToArray();

        return result;
    }

    private bool IsAiScan => SelectedPreset is "AI Scan" or "Deep AI Scan (all ports)";
    private bool IsDeepScan => SelectedPreset == "Deep AI Scan (all ports)";

    private void ApplyExtraPorts(string text)
    {
        var ports = ParsePortList(text);
        switch (SelectedPreset)
        {
            case "Quick (6 ports)": _settings.QuickExtraPorts = ports; break;
            case "Common (22 ports)": _settings.CommonExtraPorts = ports; break;
            case "Extended (58 ports)": _settings.ExtendedExtraPorts = ports; break;
            case "AI Scan": _settings.AiExtraPorts = ports; break;
        }
        SaveSettings();
        OnPropertyChanged(nameof(EffectivePortCountDisplay));
    }

    private void ApplyRemovedPorts(string text)
    {
        var ports = ParsePortList(text);
        switch (SelectedPreset)
        {
            case "Quick (6 ports)": _settings.QuickRemovedPorts = ports; break;
            case "Common (22 ports)": _settings.CommonRemovedPorts = ports; break;
            case "Extended (58 ports)": _settings.ExtendedRemovedPorts = ports; break;
            // AI preset: no removals allowed
        }
        SaveSettings();
        OnPropertyChanged(nameof(EffectivePortCountDisplay));
    }

    private static int[] ParsePortList(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var p) && p is > 0 and <= 65535 ? p : -1)
            .Where(p => p > 0)
            .Distinct()
            .ToArray();
    }

    private void RefreshSettingsFlyout()
    {
        // Load the text fields from current settings for the selected preset
        _extraPortsText = FormatPortList(GetExtraPortsForPreset());
        _removedPortsText = FormatPortList(GetRemovedPortsForPreset());

        OnPropertyChanged(nameof(ExtraPortsText));
        OnPropertyChanged(nameof(RemovedPortsText));
        OnPropertyChanged(nameof(BuiltInPortsDisplay));
        OnPropertyChanged(nameof(EffectivePortCountDisplay));
        OnPropertyChanged(nameof(IsAiPresetSelected));
        OnPropertyChanged(nameof(IsNotAiPresetSelected));
        OnPropertyChanged(nameof(HasPortPreset));
        OnPropertyChanged(nameof(PresetDisplayName));
    }

    private static string FormatPortList(int[] ports)
    {
        if (ports.Length == 0) return "";
        return string.Join(", ", ports);
    }

    private void SaveSettings()
    {
        _settingsService.Save(_settings);
    }

    // --- Scan logic ---

    private async Task StartScanAsync()
    {
        List<IPAddress> addresses;
        try
        {
            addresses = IpRangeParser.Parse(IpRange);
        }
        catch (ArgumentException ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Invalid IP Range",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        Results.Clear();
        CompletedCount = 0;
        TotalCount = addresses.Count;
        AliveCount = 0;
        ElapsedSeconds = 0;
        IsScanning = true;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var ports = GetPortsForPreset();
        var skipPing = SkipPing;
        var sw = Stopwatch.StartNew();

        // Timer to update elapsed time
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += (_, _) => ElapsedSeconds = sw.Elapsed.TotalSeconds;
        timer.Start();

        try
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            await _pingScanner.SweepAsync(addresses, 1000, 256, (ip, alive, ms) =>
            {
                dispatcher.Invoke(() =>
                {
                    var host = new HostResult
                    {
                        IpAddress = ip.ToString(),
                        IsAlive = alive,
                        PingMs = ms
                    };
                    Results.Add(host);
                    CompletedCount++;
                    if (alive) AliveCount++;

                    // Queue port scan + DNS for alive hosts (or all hosts if skip ping)
                    if (alive || skipPing)
                    {
                        _ = ScanHostDetailsAsync(host, ip, ports, ct, ignorePortHints: IsDeepScan);
                    }
                });
            }, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            timer.Stop();
            ElapsedSeconds = sw.Elapsed.TotalSeconds;
            IsScanning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task ScanHostDetailsAsync(HostResult host, IPAddress ip, int[] ports, CancellationToken ct, bool ignorePortHints = false)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        var runAiProbe = IsAiScan;

        // DNS resolve
        var hostname = await _dnsResolver.ResolveAsync(ip, ct);
        if (!string.IsNullOrEmpty(hostname))
        {
            dispatcher.Invoke(() => host.Hostname = hostname);
        }

        // Port scan
        if (ports.Length > 0)
        {
            var openPorts = await _portScanner.ScanAsync(ip, ports, 2000, 64, ct);
            if (openPorts.Count > 0)
            {
                dispatcher.Invoke(() =>
                {
                    host.OpenPorts = new ObservableCollection<PortResult>(openPorts);
                });

                // AI service probing on open ports
                if (runAiProbe)
                {
                    var aiResults = await _aiProber.ProbeAllAsync(
                        ip.ToString(), openPorts.Select(p => p.Port).ToArray(), ct, ignorePortHints);

                    if (aiResults.Count > 0)
                    {
                        dispatcher.Invoke(() => host.AiResults = aiResults);
                    }
                }
            }
        }
    }

    private void ExportResults()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files|*.csv|Text files|*.txt|All files|*.*",
            DefaultExt = ".csv",
            FileName = "scan-results"
        };
        if (dlg.ShowDialog() != true) return;

        var path = dlg.FileName;
        var isCsv = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        var lines = new List<string>
        {
            isCsv
                ? "ip,hostname,alive,ping_ms,open_ports,ai_services"
                : "ip\thostname\talive\tping_ms\topen_ports\tai_services"
        };

        foreach (var host in Results)
        {
            var ports = host.OpenPorts.Count > 0
                ? string.Join(";", host.OpenPorts.Select(p => $"{p.Port}/{p.ServiceName}"))
                : "";
            var ai = host.AiResults.Count > 0
                ? string.Join(";", host.AiResults.Select(a => $"{a.ServiceName}:{a.Port}"))
                : "";

            lines.Add(isCsv
                ? $"{host.IpAddress},{CsvEscape(host.Hostname)},{host.IsAlive},{host.PingMs?.ToString() ?? ""},{CsvEscape(ports)},{CsvEscape(ai)}"
                : $"{host.IpAddress}\t{host.Hostname}\t{host.IsAlive}\t{host.PingMs?.ToString() ?? ""}\t{ports}\t{ai}");
        }

        File.WriteAllLines(path, lines);
    }

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private void StopScan()
    {
        _cts?.Cancel();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
