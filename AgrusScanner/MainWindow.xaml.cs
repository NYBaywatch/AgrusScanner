using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AgrusScanner.Models;
using AgrusScanner.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AgrusScanner;

public partial class MainWindow : Window
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private double _zoomLevel = 1.0;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Enable dark title bar
        var hwnd = new WindowInteropHelper(this).Handle;
        int value = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                Zoom(0.1);
                e.Handled = true;
            }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                Zoom(-0.1);
                e.Handled = true;
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                _zoomLevel = 1.0;
                ApplyZoom();
                e.Handled = true;
            }
            else if (e.Key == Key.C)
            {
                CopySelectedIp();
                e.Handled = true;
            }
        }
    }

    private void CopySelectedIp()
    {
        if (ResultsGrid.SelectedItem is HostResult host && !string.IsNullOrEmpty(host.IpAddress))
            System.Windows.Clipboard.SetText(host.IpAddress);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            Zoom(e.Delta > 0 ? 0.1 : -0.1);
            e.Handled = true;
        }
    }

    private void Zoom(double delta)
    {
        _zoomLevel = Math.Clamp(_zoomLevel + delta, 0.5, 3.0);
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        var transform = new ScaleTransform(_zoomLevel, _zoomLevel);
        ((FrameworkElement)Content).LayoutTransform = transform;
        HelpPopupContent.LayoutTransform = new ScaleTransform(_zoomLevel, _zoomLevel);
        SettingsPopupContent.LayoutTransform = new ScaleTransform(_zoomLevel, _zoomLevel);
    }

    // --- Context menu ---

    private void ResultsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = ResultsGrid.ContextMenu;
        if (menu is null) return;
        menu.Items.Clear();

        if (ResultsGrid.SelectedItem is not HostResult host)
        {
            e.Handled = true; // suppress empty menu
            return;
        }

        // Copy IP
        menu.Items.Add(new MenuItem
        {
            Header = "Copy IP Address",
            Command = new RelayCommand(_ => System.Windows.Clipboard.SetText(host.IpAddress))
        });

        // Copy All
        var allText = $"{host.IpAddress}\t{host.Hostname}\t{host.PortsDisplay}";
        menu.Items.Add(new MenuItem
        {
            Header = "Copy All",
            Command = new RelayCommand(_ => System.Windows.Clipboard.SetText(allText))
        });

        // Dynamic "Open" items from open ports
        if (host.OpenPorts.Count > 0)
        {
            menu.Items.Add(new Separator());

            foreach (var port in host.OpenPorts)
            {
                var (label, action) = GetOpenAction(host.IpAddress, port.Port);
                if (action is not null)
                {
                    menu.Items.Add(new MenuItem
                    {
                        Header = label,
                        Command = new RelayCommand(_ => action())
                    });
                }
            }
        }
    }

    private static (string Label, Action? Action) GetOpenAction(string ip, int port)
    {
        Action launch = port switch
        {
            443 or 8443 => () => ShellOpen($"https://{ip}:{port}"),
            80 => () => ShellOpen($"http://{ip}"),
            8080 => () => ShellOpen($"http://{ip}:{port}"),
            3389 => () => ShellOpen("mstsc", $"/v:{ip}"),
            22 => () => ShellOpen("ssh", ip),
            5900 or 5901 => () => ShellOpen($"vnc://{ip}:{port}"),
            21 => () => ShellOpen($"ftp://{ip}"),
            _ => () => ShellOpen($"http://{ip}:{port}")
        };

        var label = port switch
        {
            80 => "Open HTTP",
            443 or 8443 => $"Open HTTPS (:{port})",
            8080 => "Open HTTP (:8080)",
            3389 => "Open RDP",
            22 => "Open SSH",
            5900 or 5901 => $"Open VNC (:{port})",
            21 => "Open FTP",
            _ => $"Open :{port} in Browser"
        };

        return (label, launch);
    }

    private static void ShellOpen(string target, string? args = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target)
            {
                Arguments = args ?? "",
                UseShellExecute = true
            });
        }
        catch { /* fail silently if no handler */ }
    }

    // --- Update link ---

    private void UpdateLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenUpdateCommand.Execute(null);
    }

    // --- Custom sorting ---

    private void ResultsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (e.Column.Header is not string header) return;

        IComparer? comparer = header switch
        {
            "IP ADDRESS" => new NaturalIpComparer(),
            "PING" => new PingComparer(),
            _ => null
        };

        if (comparer is null) return;

        e.Handled = true; // we handle it ourselves

        // Toggle direction
        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        e.Column.SortDirection = direction;

        var view = CollectionViewSource.GetDefaultView(ResultsGrid.ItemsSource);
        if (view is ListCollectionView lcv)
        {
            lcv.CustomSort = direction == ListSortDirection.Ascending
                ? comparer
                : new ReverseComparer(comparer);
        }
    }
}

// --- Comparers ---

internal class NaturalIpComparer : IComparer
{
    public int Compare(object? x, object? y)
    {
        if (x is not HostResult a || y is not HostResult b) return 0;
        return CompareIps(a.IpAddress, b.IpAddress);
    }

    private static int CompareIps(string ipA, string ipB)
    {
        var partsA = ipA.Split('.');
        var partsB = ipB.Split('.');
        for (int i = 0; i < Math.Min(partsA.Length, partsB.Length); i++)
        {
            int.TryParse(partsA[i], out var a);
            int.TryParse(partsB[i], out var b);
            var cmp = a.CompareTo(b);
            if (cmp != 0) return cmp;
        }
        return partsA.Length.CompareTo(partsB.Length);
    }
}

internal class PingComparer : IComparer
{
    public int Compare(object? x, object? y)
    {
        if (x is not HostResult a || y is not HostResult b) return 0;
        // nulls (no ping) sort to end
        if (!a.PingMs.HasValue && !b.PingMs.HasValue) return 0;
        if (!a.PingMs.HasValue) return 1;
        if (!b.PingMs.HasValue) return -1;
        return a.PingMs.Value.CompareTo(b.PingMs.Value);
    }
}

internal class ReverseComparer(IComparer inner) : IComparer
{
    public int Compare(object? x, object? y) => inner.Compare(y, x);
}
