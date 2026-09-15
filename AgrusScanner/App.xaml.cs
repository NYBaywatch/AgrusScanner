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

        // Load detection signatures: embedded baseline, then a verified installed package if present.
        SignatureStore.Initialize();

        if (e.Args.Contains("--mcp-only", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settings = new SettingsService().Load();
            if (!settings.McpServerEnabled)
            {
                System.Windows.MessageBox.Show("The built-in MCP server is disabled in Settings. Enable it in the Agrus Scanner window to use --mcp-only mode.",
                    "Agrus Scanner", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(1);
                return;
            }
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
