using System.IO;
using System.Net;
using System.Text;
using AgrusScanner.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgrusScanner.Tests;

// Detection of OTHER people's MCP servers on the network. Nothing here touches Agrus's own MCP tools.
public class McpExtractionTests
{
    [Fact]
    public void Legacy_initialize_result_json()
    {
        const string body = """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{"listChanged":true},"resources":{},"logging":{}},"serverInfo":{"name":"playwright-mcp","version":"0.0.41"}}}""";
        Assert.Equal("playwright-mcp v0.0.41 · tools, resources · 2025-06-18", AiServiceProber.ExtractMcpInfo(body));
    }

    [Fact]
    public void Initialize_result_wrapped_in_sse_frame()
    {
        const string body = "event: message\r\ndata: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"agrus-scanner\",\"version\":\"0.2.2\"}}}\r\n\r\n";
        Assert.Equal("agrus-scanner v0.2.2 · tools · 2025-11-25", AiServiceProber.ExtractMcpInfo(body));
    }

    [Fact]
    public void Modern_discover_result()
    {
        const string body = """{"jsonrpc":"2.0","id":1,"result":{"supportedVersions":["2026-07-28"],"capabilities":{"tools":{},"prompts":{}},"_meta":{"io.modelcontextprotocol/serverInfo":{"name":"ExampleServer","version":"1.0.0"}}}}""";
        Assert.Equal("ExampleServer v1.0.0 · tools, prompts · 2026-07-28", AiServiceProber.ExtractMcpInfo(body));
    }

    [Fact]
    public void Version_rejection_lists_supported_versions()
    {
        const string body = """{"jsonrpc":"2.0","id":1,"error":{"code":-32022,"message":"Unsupported protocol version","data":{"supported":["2026-07-28","2027-01-01"]}}}""";
        Assert.Equal("MCP 2026-07-28/2027-01-01", AiServiceProber.ExtractMcpInfo(body));
    }

    [Fact]
    public void Bare_get_406_body()
    {
        const string body = """{"jsonrpc":"2.0","error":{"code":-32000,"message":"Not Acceptable: Client must accept text/event-stream"},"id":null}""";
        Assert.Equal("Streamable HTTP (no session)", AiServiceProber.ExtractMcpInfo(body));
    }

    [Fact]
    public void Legacy_sse_endpoint_event()
    {
        Assert.Equal("HTTP+SSE transport", AiServiceProber.ExtractMcpInfo("event: endpoint\ndata: /messages?sessionId=abc\n\n"));
    }

    [Fact]
    public void Server_name_is_sanitised_and_truncated()
    {
        var longName = new string('x', 60) + "\\u0007"; // JSON escape sequence for a BEL control character
        var body = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"serverInfo\":{\"name\":\"" + longName + "\"}}}";
        var info = AiServiceProber.ExtractMcpInfo(body);
        Assert.Equal(40, info.Length);
        Assert.DoesNotContain('\u0007', info);
    }

    [Fact]
    public void Garbage_yields_empty()
    {
        Assert.Equal("", AiServiceProber.ExtractMcpInfo("<html>"));
        Assert.Equal("", AiServiceProber.ExtractMcpInfo("{\"jsonrpc\":\"2.0\",\"result\":{}}"));
    }
}

public class SseReaderTests
{
    [Fact]
    public async Task Returns_first_event_and_stops()
    {
        var bytes = Encoding.UTF8.GetBytes("event: endpoint\ndata: /messages?sessionId=1\n\nevent: message\ndata: {}\n\n");
        var text = await AiServiceProber.ReadFirstEventAsync(new MemoryStream(bytes), isSse: true, CancellationToken.None);
        Assert.Equal("event: endpoint\ndata: /messages?sessionId=1\n", text);
    }

    [Fact]
    public async Task Never_closing_stream_returns_within_cap()
    {
        // A stream that delivers a comment line then blocks forever, like a keep-alive SSE endpoint.
        var stream = new BlockingStream(Encoding.UTF8.GetBytes(": keep-alive\n"));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AiServiceProber.ReadFirstEventAsync(stream, true, cts.Token));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Non_sse_reads_whole_body_up_to_cap()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"a\":1}\n\n{\"b\":2}");
        var text = await AiServiceProber.ReadFirstEventAsync(new MemoryStream(bytes), isSse: false, CancellationToken.None);
        Assert.Equal("{\"a\":1}\n\n{\"b\":2}", text);
    }

    private sealed class BlockingStream(byte[] prefix) : Stream
    {
        private int _pos;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct)
        {
            if (_pos < prefix.Length)
            {
                var n = Math.Min(buffer.Length, prefix.Length - _pos);
                prefix.AsMemory(_pos, n).CopyTo(buffer);
                _pos += n;
                return n;
            }
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}

// End to end against a real MCP server (the same SDK Agrus ships) hosted in-process on a random port.
public class McpProbeIntegrationTests : IAsyncLifetime
{
    private WebApplication? _app;
    private int _port;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddMcpServer(o => o.ServerInfo = new() { Name = "fixture-mcp", Version = "9.9.9" })
            .WithHttpTransport();
        _app = builder.Build();
        _app.MapMcp("/mcp");
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _port = new Uri(address).Port;
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) { await _app.StopAsync(); await _app.DisposeAsync(); }
    }

    [Fact]
    public async Task Prober_identifies_streamable_http_server_and_reads_server_info()
    {
        var result = await new AiServiceProber().ProbeAsync("127.0.0.1", _port, CancellationToken.None, ignorePortHints: true);

        Assert.NotNull(result);
        Assert.Equal("MCP Server", result!.Category);
        Assert.Equal("MCP Server", result.ServiceName);
        Assert.Equal("high", result.Confidence);
        Assert.StartsWith("fixture-mcp v9.9.9", result.Details);
    }
}

// Legacy HTTP+SSE servers hold the GET open forever; the prober must still identify them quickly.
public class LegacySseProbeTests
{
    [Fact]
    public async Task Prober_identifies_legacy_sse_server_without_hanging()
    {
        var listener = new HttpListener();
        var port = FreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serve = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); } catch { break; }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (ctx.Request.Url!.AbsolutePath == "/sse")
                        {
                            ctx.Response.ContentType = "text/event-stream";
                            var bytes = Encoding.UTF8.GetBytes("event: endpoint\ndata: /messages?sessionId=abc\n\n");
                            await ctx.Response.OutputStream.WriteAsync(bytes);
                            await ctx.Response.OutputStream.FlushAsync();
                            await Task.Delay(10_000); // never closes on its own
                        }
                        else ctx.Response.StatusCode = 404;
                    }
                    catch { }
                    finally { try { ctx.Response.Close(); } catch { } }
                });
            }
        });

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await new AiServiceProber().ProbeAsync("127.0.0.1", port, CancellationToken.None, ignorePortHints: true);
            Assert.NotNull(result);
            Assert.Equal("MCP Server (HTTP+SSE)", result!.ServiceName);
            Assert.Equal("HTTP+SSE transport", result.Details);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"probe took {sw.Elapsed}");
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
