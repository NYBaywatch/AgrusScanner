using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
        }
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
}
