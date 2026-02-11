using System.Drawing;
using System.Windows;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;

namespace AgrusScanner;

public class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;

    public TrayIcon(int port)
    {
        var menu = new WinForms.ContextMenuStrip();
        var label = menu.Items.Add($"Agrus MCP \u2014 localhost:{port}");
        label.Enabled = false;
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            Application.Current.Shutdown();
        });

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = LoadEmbeddedIcon(),
            Text = $"Agrus Scanner MCP :{port}",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    private static Icon LoadEmbeddedIcon()
    {
        var uri = new Uri("pack://application:,,,/icon.ico", UriKind.Absolute);
        var stream = Application.GetResourceStream(uri)?.Stream;
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
