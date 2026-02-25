using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AgrusScanner.Mcp;

public class McpHostManager
{
    private WebApplication? _app;

    public async Task StartAsync(int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://localhost:{port}");

        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "agrus-scanner",
                Version = "0.2.2"
            };
        })
        .WithHttpTransport()
        .WithTools<ScannerMcpTools>();

        _app = builder.Build();

        // Block DNS rebinding attacks — only allow localhost Host headers
        _app.Use(async (context, next) =>
        {
            var host = context.Request.Host.Host;
            if (host != "localhost" && host != "127.0.0.1" && host != "::1")
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Forbidden: invalid Host header");
                return;
            }
            await next();
        });

        _app.MapMcp("/mcp");

        await _app.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
