using System.Windows;
using AgrusScanner.Mcp;
using AgrusScanner.Services;
using Application = System.Windows.Application;

namespace AgrusScanner;

public partial class App : Application
{
    private McpHostManager? _mcpHost;
    private TrayIcon? _trayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--mcp-only", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settings = new SettingsService().Load();
            var port = settings.McpPort;

            _trayIcon = new TrayIcon(port);
            _mcpHost = new McpHostManager();

            try
            {
                await _mcpHost.StartAsync(port);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to start MCP server: {ex.Message}",
                    "Agrus Scanner", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }
        else
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_mcpHost is not null)
            await _mcpHost.StopAsync();

        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
