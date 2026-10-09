using AgrusScanner.Mcp;
using AgrusScanner.Services;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Headless Agrus Scanner MCP server.
//   agrus-mcp                 stdio transport (default; what MCP clients launch)
//   agrus-mcp --http [port]   Streamable HTTP at /mcp (default port 8999)
//   --bind <address>          HTTP bind address. Defaults to 127.0.0.1, or 0.0.0.0 when
//                             running inside a container (DOTNET_RUNNING_IN_CONTAINER=true)
//   MCP_TOKEN=<secret>        If set, HTTP requests must carry "Authorization: Bearer <secret>"
//
// All diagnostics go to stderr: in stdio mode stdout is the protocol channel.

var httpIndex = Array.FindIndex(args, a => a.Equals("--http", StringComparison.OrdinalIgnoreCase));
var useHttp = httpIndex >= 0;
var port = 8999;
if (useHttp && httpIndex + 1 < args.Length && int.TryParse(args[httpIndex + 1], out var p))
    port = p;

var inContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
var bind = inContainer ? "0.0.0.0" : "127.0.0.1";
var bindIndex = Array.FindIndex(args, a => a.Equals("--bind", StringComparison.OrdinalIgnoreCase));
if (bindIndex >= 0 && bindIndex + 1 < args.Length)
    bind = args[bindIndex + 1];
var token = Environment.GetEnvironmentVariable("MCP_TOKEN");

if (args.Any(a => a is "-h" or "--help"))
{
    Console.Error.WriteLine("Usage: agrus-mcp [--http [port]] [--bind <address>]");
    Console.Error.WriteLine("  (no args)         MCP over stdio");
    Console.Error.WriteLine("  --http [port]     MCP over Streamable HTTP at /mcp (default port 8999)");
    Console.Error.WriteLine("  --bind <address>  Bind address (default 127.0.0.1; 0.0.0.0 inside a container)");
    Console.Error.WriteLine("  MCP_TOKEN env     Require 'Authorization: Bearer <token>' on HTTP requests");
    return 0;
}

// Load the embedded signature catalog (and any verified installed package) before the first probe.
SignatureStore.Initialize();

var version = (typeof(ScannerMcpTools).Assembly.GetName().Version ?? new Version(0, 0, 0)).ToString(3);

if (useHttp)
{
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.WebHost.UseUrls($"http://{bind}:{port}");

    builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "agrus-scanner", Version = version })
        .WithHttpTransport()
        .WithTools<ScannerMcpTools>();

    var app = builder.Build();

    var loopback = bind is "127.0.0.1" or "localhost" or "::1";
    app.Use(async (context, next) =>
    {
        // Loopback binding: reject non-local Host headers to block DNS rebinding (same rule as the desktop app).
        if (loopback && context.Request.Host.Host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden: invalid Host header");
            return;
        }
        // Optional shared secret for any non-local deployment.
        if (token is { Length: > 0 })
        {
            var header = context.Request.Headers.Authorization.ToString();
            var ok = header.StartsWith("Bearer ", StringComparison.Ordinal)
                && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(header["Bearer ".Length..]), Encoding.UTF8.GetBytes(token));
            if (!ok)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
        }
        await next();
    });

    app.MapMcp("/mcp");
    Console.Error.WriteLine($"agrus-scanner {version} listening on http://{bind}:{port}/mcp"
        + (token is { Length: > 0 } ? " (bearer token required)" : "")
        + (!loopback && token is not { Length: > 0 } ? " WARNING: non-loopback bind with no MCP_TOKEN set" : ""));
    await app.RunAsync();
}
else
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "agrus-scanner", Version = version })
        .WithStdioServerTransport()
        .WithTools<ScannerMcpTools>();

    await builder.Build().RunAsync();
}

return 0;
