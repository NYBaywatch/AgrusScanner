using System.IO;
using System.Net.Http;
using System.Text.Json;
using AgrusScanner.Models;

namespace AgrusScanner.Services;

public class AiServiceProber
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(3),
        MaxResponseContentBufferSize = 1_048_576 // 1 MB — prevent memory bombs from malicious servers
    };

    private readonly SemaphoreSlim _semaphore = new(32);

    private static readonly string UserAgent =
        $"AgrusScanner/{(typeof(AiServiceProber).Assembly.GetName().Version ?? new Version(0, 0, 0)).ToString(3)}";

    // ── Detection definitions come from the signature catalog (signatures/catalog.json baseline,
    //    or a verified .agsig package). See SignatureStore. ──

    internal static ProbeDefinition[] Probes => SignatureStore.Current.Probes;
    private static string[] AiDockerPatterns => SignatureStore.Current.DockerPatterns;

    /// <summary>
    /// Probe a single port — returns the best matching AI service or null.
    /// </summary>
    public async Task<AiServiceResult?> ProbeAsync(string ip, int port, CancellationToken ct, bool ignorePortHints = false)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            AiServiceResult? best = null;

            foreach (var probe in Probes)
            {
                ct.ThrowIfCancellationRequested();

                // If probe has a port hint, only run it on that specific port (unless ignoring hints)
                if (!ignorePortHints && probe.PortHint.HasValue && probe.PortHint.Value != port)
                    continue;

                try
                {
                    var scheme = port == 8443 || port == 2376 ? "https" : "http";
                    var url = $"{scheme}://{ip}:{port}{probe.Path}";

                    using var request = BuildRequest(probe, url);

                    // SSE endpoints never close, so read headers first and cap the body read ourselves.
                    var streaming = probe.AcceptHeader?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true;
                    using var response = await _http.SendAsync(request,
                        streaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, ct);

                    // Check status code match
                    if (probe.StatusCode.HasValue && (int)response.StatusCode != probe.StatusCode.Value)
                        continue;

                    string? body = null;

                    // If we need to check body content, read it
                    if (probe.BodyContains != null || probe.HeaderContains != null || NeedsDetailExtraction(probe))
                    {
                        body = streaming ? await ReadStreamingBodyAsync(response, ct) : await response.Content.ReadAsStringAsync(ct);
                    }

                    // Status-code-only match (no body/header check needed)
                    if (probe.BodyContains == null && probe.HeaderContains == null && probe.StatusCode.HasValue)
                    {
                        if (best == null || probe.Specificity > best.Specificity)
                        {
                            var details = body != null ? TryExtractDetails(probe.ServiceName, probe.Path, body, port) : "";
                            best = new AiServiceResult
                            {
                                ServiceName = probe.ServiceName,
                                Category = probe.Category,
                                Port = port,
                                Confidence = probe.Confidence,
                                Specificity = probe.Specificity,
                                Details = details
                            };
                        }
                        continue;
                    }

                    // Check body contains
                    if (probe.BodyContains != null && body != null)
                    {
                        if (!body.Contains(probe.BodyContains, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var details = TryExtractDetails(probe.ServiceName, probe.Path, body, port);

                        // MCP initialize may have opened a session; close it (spec: client SHOULD DELETE).
                        if (probe.Method == "POST" && response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds))
                            _ = CloseMcpSessionAsync(url, sessionIds.First());

                        if (best == null || probe.Specificity > best.Specificity)
                        {
                            best = new AiServiceResult
                            {
                                ServiceName = probe.ServiceName,
                                Category = probe.Category,
                                Port = port,
                                Confidence = probe.Confidence,
                                Specificity = probe.Specificity,
                                Details = details
                            };
                        }
                    }

                    // Check header contains
                    if (probe.HeaderContains != null)
                    {
                        var allHeaders = string.Join(" ", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
                        if (allHeaders.Contains(probe.HeaderContains, StringComparison.OrdinalIgnoreCase))
                        {
                            if (best == null || probe.Specificity > best.Specificity)
                            {
                                best = new AiServiceResult
                                {
                                    ServiceName = probe.ServiceName,
                                    Category = probe.Category,
                                    Port = port,
                                    Confidence = probe.Confidence,
                                    Specificity = probe.Specificity
                                };
                            }
                        }
                    }
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"Probe {probe.ServiceName} on {ip}:{port}{probe.Path} failed: {ex.GetType().Name}");
                }
            }

            return best;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Probe all open ports on a host and return ALL detected AI services (not just best).
    /// </summary>
    public async Task<List<AiServiceResult>> ProbeAllAsync(string ip, int[] openPorts, CancellationToken ct, bool ignorePortHints = false)
    {
        var results = new List<AiServiceResult>();
        var seen = new HashSet<string>(); // avoid duplicate service names

        var tasks = openPorts.Select(async port =>
        {
            var result = await ProbeAsync(ip, port, ct, ignorePortHints);
            return result;
        });

        var probeResults = await Task.WhenAll(tasks);

        foreach (var r in probeResults)
        {
            if (r != null)
            {
                var key = $"{r.ServiceName}:{r.Port}";
                if (seen.Add(key))
                    results.Add(r);
            }
        }

        // If Docker API was found, enumerate AI containers
        var dockerResult = results.FirstOrDefault(r => r.ServiceName == "Docker API");
        if (dockerResult != null)
        {
            var containers = await EnumerateDockerAiContainersAsync(ip, dockerResult.Port, ct);
            if (containers.Count > 0)
            {
                dockerResult.Details = string.Join(", ", containers);
            }
        }

        // Sort by specificity descending
        results.Sort((a, b) => b.Specificity.CompareTo(a.Specificity));
        return results;
    }

    /// <summary>
    /// Query Docker API for running containers and filter for AI-related images.
    /// </summary>
    private async Task<List<string>> EnumerateDockerAiContainersAsync(string ip, int port, CancellationToken ct)
    {
        var aiContainers = new List<string>();
        try
        {
            var url = $"http://{ip}:{port}/containers/json";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", UserAgent);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            foreach (var container in doc.RootElement.EnumerateArray())
            {
                var image = container.TryGetProperty("Image", out var img) ? img.GetString() ?? "" : "";
                var imageLower = image.ToLowerInvariant();

                // Check if image matches any known AI pattern
                foreach (var pattern in AiDockerPatterns)
                {
                    if (imageLower.Contains(pattern))
                    {
                        // Get container name
                        var name = "";
                        if (container.TryGetProperty("Names", out var names) && names.GetArrayLength() > 0)
                            name = names[0].GetString()?.TrimStart('/') ?? "";

                        var state = container.TryGetProperty("State", out var s) ? s.GetString() ?? "" : "";
                        var display = !string.IsNullOrEmpty(name) ? $"{name} ({image})" : image;
                        if (state == "running") display += " [running]";

                        aiContainers.Add(display);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Docker enumeration on {ip}:{port} failed: {ex.GetType().Name}");
        }
        return aiContainers;
    }

    private static bool NeedsDetailExtraction(ProbeDefinition probe)
    {
        // Services where we want to read the body even for status-code-only probes
        return probe.ServiceName is "Docker API" or "NVIDIA Triton" or "ComfyUI"
            or "TorchServe" or "MLflow" or "Ray Serve"
            || probe.Category == "MCP Server";
    }

    // ── Request shaping + streaming reads (driven by the catalog's optional request fields) ──

    private static HttpRequestMessage BuildRequest(ProbeDefinition probe, string url)
    {
        var method = probe.Method == "POST" ? HttpMethod.Post : HttpMethod.Get;
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("User-Agent", UserAgent);
        if (probe.AcceptHeader is not null)
            request.Headers.TryAddWithoutValidation("Accept", probe.AcceptHeader);
        if (probe.Headers is not null)
            foreach (var (name, value) in probe.Headers)
                request.Headers.TryAddWithoutValidation(name, value);
        if (method == HttpMethod.Post)
            request.Content = new StringContent(probe.Body ?? "", System.Text.Encoding.UTF8, probe.ContentType ?? "application/json");
        return request;
    }

    private const int StreamingReadCapBytes = 64 * 1024;
    private static readonly TimeSpan StreamingReadCap = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Body read for responses obtained with ResponseHeadersRead. For text/event-stream, returns the first
    /// complete SSE event (raw framing kept, so bodyContains can match "event: endpoint" or a data: payload).
    /// Otherwise reads up to the cap. Always bounded by <see cref="StreamingReadCap"/>.
    /// </summary>
    private static async Task<string> ReadStreamingBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(StreamingReadCap);
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            var isSse = response.Content.Headers.ContentType?.MediaType?.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase) == true;
            return await ReadFirstEventAsync(stream, isSse, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ""; // stream stayed silent past the cap; nothing to match
        }
    }

    /// <summary>Reads until the first blank line after a data: line (one SSE event), or the byte cap, or EOF.</summary>
    internal static async Task<string> ReadFirstEventAsync(Stream stream, bool isSse, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var collected = new MemoryStream();
        while (collected.Length < StreamingReadCapBytes)
        {
            var n = await stream.ReadAsync(buffer, ct);
            if (n == 0) break;
            collected.Write(buffer, 0, n);
            if (!isSse) continue;

            var text = System.Text.Encoding.UTF8.GetString(collected.GetBuffer(), 0, (int)collected.Length);
            var end = FirstEventEnd(text);
            if (end >= 0) return text[..end];
        }
        return System.Text.Encoding.UTF8.GetString(collected.GetBuffer(), 0, (int)collected.Length);
    }

    private static int FirstEventEnd(string text)
    {
        var sawData = false;
        var pos = 0;
        while (pos < text.Length)
        {
            var nl = text.IndexOf('\n', pos);
            if (nl < 0) return -1;
            var line = text[pos..nl].TrimEnd('\r');
            if (line.StartsWith("data:", StringComparison.Ordinal)) sawData = true;
            else if (line.Length == 0 && sawData) return nl;
            pos = nl + 1;
        }
        return -1;
    }

    private static async Task CloseMcpSessionAsync(string url, string sessionId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var del = new HttpRequestMessage(HttpMethod.Delete, url);
            del.Headers.Add("User-Agent", UserAgent);
            del.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
            using var _ = await _http.SendAsync(del, cts.Token);
        }
        catch { /* best effort; 405 or timeout are both fine */ }
    }

    private static string TryExtractDetails(string service, string path, string body, int port)
    {
        try
        {
            // MCP replies may arrive SSE-framed ("event: message\ndata: {...}"); unwrap before parsing.
            if (service.StartsWith("MCP Server", StringComparison.Ordinal))
                return ExtractMcpInfo(body);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            return service switch
            {
                // LLM services
                "Ollama" when path == "/api/tags" && root.TryGetProperty("models", out var models) =>
                    FormatModelList(models),
                "vLLM" when root.TryGetProperty("version", out var v) =>
                    $"v{v.GetString()}",
                "HF TGI" when root.TryGetProperty("model_id", out var m) =>
                    FormatTgiInfo(root, m),
                "llama.cpp" when path == "/props" && root.TryGetProperty("default_generation_settings", out var gs) =>
                    FormatLlamaCppInfo(gs),
                "KoboldCpp" when path.Contains("version") && root.TryGetProperty("result", out var ver) =>
                    $"v{ver.GetString()}",
                "KoboldCpp" when path.Contains("model") && root.TryGetProperty("result", out var model) =>
                    model.GetString() ?? "",
                "LM Studio" when root.TryGetProperty("data", out var data) =>
                    FormatModelNames(data),
                "Jan.ai" when root.TryGetProperty("data", out var data) =>
                    FormatModelNames(data),
                "GPT4All" when root.TryGetProperty("data", out var data) =>
                    FormatModelNames(data),
                "LiteLLM" when root.TryGetProperty("data", out var data) =>
                    FormatLitellmModels(data),
                "FastChat" when root.TryGetProperty("data", out var data) =>
                    FormatModelNames(data),
                "Tabby" when root.TryGetProperty("model", out var model) =>
                    model.GetString() ?? "",
                "LocalAI" when root.TryGetProperty("data", out var localData) =>
                    FormatModelNames(localData),

                // v0.3.0 LLM serving
                "MLX-LM" when root.TryGetProperty("data", out var mlxData) =>
                    FormatModelNames(mlxData),

                // v0.3.0 voice
                "Speaches" when root.TryGetProperty("data", out var spData) =>
                    FormatModelNames(spData),

                // v0.3.0 embeddings
                "HF TEI" when root.TryGetProperty("model_id", out var teiModel) =>
                    FormatTgiInfo(root, teiModel),

                // v0.3.0 agent platforms
                "AutoGen Studio" when root.TryGetProperty("data", out var asData) && asData.TryGetProperty("version", out var asVer) =>
                    $"v{asVer.GetString()}",
                "Letta" when root.TryGetProperty("version", out var lVer) =>
                    $"v{lVer.GetString()}",
                "Langflow" when root.TryGetProperty("status", out var lfStatus) =>
                    lfStatus.GetString() ?? "ok",

                // v0.3.0 RAG
                "R2R" when root.TryGetProperty("results", out var r2rRes) && r2rRes.TryGetProperty("response", out var r2rResp) =>
                    r2rResp.GetString() ?? "ok",
                "Verba" when root.TryGetProperty("deployments", out var vbDep) && vbDep.ValueKind == JsonValueKind.Object =>
                    $"{vbDep.EnumerateObject().Count()} deployment(s)",
                "RAGFlow" when root.TryGetProperty("data", out var rfData) && rfData.TryGetProperty("version", out var rfVer) =>
                    $"v{rfVer.GetString()}",

                // v0.3.0 image gen
                "SD WebUI Forge" when root.TryGetProperty("sd_model_checkpoint", out var fckpt) =>
                    fckpt.GetString() ?? "",

                // Image generation
                "Stable Diffusion (A1111)" when path.Contains("sd-models") =>
                    FormatSdModels(root),
                "Stable Diffusion (A1111)" when path.Contains("options") && root.TryGetProperty("sd_model_checkpoint", out var ckpt) =>
                    ckpt.GetString() ?? "",
                "ComfyUI" when root.TryGetProperty("system", out var sys) =>
                    FormatComfyInfo(sys),

                // ML platforms
                "NVIDIA Triton" when path.Contains("repository") =>
                    FormatTritonModels(root),
                "TorchServe" when path == "/models" && root.TryGetProperty("models", out var tsModels) =>
                    FormatTorchServeModels(tsModels),
                "TensorFlow Serving" when root.TryGetProperty("model_version_status", out var mvs) =>
                    FormatTfServingInfo(mvs),
                "MLflow" when path == "/version" =>
                    $"v{body.Trim().Trim('"')}",
                "Ray Serve" => "active",

                // GPU infra
                "NVIDIA DCGM" => ExtractGpuInfo(body),
                "Triton Metrics" => ExtractMetricsSummary(body, "nv_inference_request_success"),

                // OpenAI-compatible (generic)
                "OpenAI-compatible" when root.TryGetProperty("data", out var data) =>
                    FormatModelNames(data),
                "LM Studio / TGW" when root.TryGetProperty("data", out var data) =>
                    FormatModelNames(data),

                _ => ""
            };
        }
        catch
        {
            // Not JSON — try plain text extraction
            return service switch
            {
                "NVIDIA DCGM" => ExtractGpuInfo(body),
                "Triton Metrics" => ExtractMetricsSummary(body, "nv_inference_request_success"),
                "TorchServe Metrics" => ExtractMetricsSummary(body, "ts_inference_"),
                "MLflow" when path == "/version" => $"v{body.Trim().Trim('"')}",
                _ => ""
            };
        }
    }

    // ── Detail extraction helpers ──

    /// <summary>
    /// One-line MCP summary: "{name} v{version} · {tools, resources, prompts} · {protocolVersion}".
    /// Handles legacy initialize results, 2026-07-28 server/discover results, version-rejection errors,
    /// the SDKs' 406 "must accept text/event-stream" body, and legacy "event: endpoint" SSE streams.
    /// </summary>
    internal static string ExtractMcpInfo(string body)
    {
        var text = body.TrimStart();
        if (text.StartsWith("event:", StringComparison.Ordinal) || text.StartsWith("data:", StringComparison.Ordinal))
        {
            if (text.Contains("event: endpoint", StringComparison.Ordinal)) return "HTTP+SSE transport";
            text = string.Join("", text.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.StartsWith("data:", StringComparison.Ordinal))
                .Select(l => l[5..].TrimStart()));
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                if (err.TryGetProperty("data", out var data) && data.TryGetProperty("supported", out var sup) && sup.ValueKind == JsonValueKind.Array)
                    return "MCP " + string.Join("/", sup.EnumerateArray().Select(v => v.GetString()).Where(v => v is not null));
                if (err.TryGetProperty("message", out var msg) && (msg.GetString() ?? "").Contains("must accept", StringComparison.OrdinalIgnoreCase))
                    return "Streamable HTTP (no session)";
                return "";
            }

            if (!root.TryGetProperty("result", out var result)) return "";

            string? name = null, version = null, protocol = null;
            if (result.TryGetProperty("serverInfo", out var si))
            {
                name = si.TryGetProperty("name", out var n) ? n.GetString() : null;
                version = si.TryGetProperty("version", out var v) ? v.GetString() : null;
            }
            else if (result.TryGetProperty("_meta", out var meta) && meta.TryGetProperty("io.modelcontextprotocol/serverInfo", out si))
            {
                name = si.TryGetProperty("name", out var n) ? n.GetString() : null;
                version = si.TryGetProperty("version", out var v) ? v.GetString() : null;
            }
            if (result.TryGetProperty("protocolVersion", out var pv)) protocol = pv.GetString();
            else if (result.TryGetProperty("supportedVersions", out var sv) && sv.ValueKind == JsonValueKind.Array && sv.GetArrayLength() > 0)
                protocol = sv[0].GetString();

            var caps = new List<string>();
            if (result.TryGetProperty("capabilities", out var c) && c.ValueKind == JsonValueKind.Object)
                foreach (var key in new[] { "tools", "resources", "prompts" })
                    if (c.TryGetProperty(key, out _)) caps.Add(key);

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(name))
            {
                var clean = new string(name.Where(ch => !char.IsControl(ch)).ToArray());
                if (clean.Length > 40) clean = clean[..40];
                parts.Add(string.IsNullOrWhiteSpace(version) ? clean : $"{clean} v{version}");
            }
            if (caps.Count > 0) parts.Add(string.Join(", ", caps));
            if (!string.IsNullOrWhiteSpace(protocol)) parts.Add(protocol);
            return string.Join(" · ", parts);
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static string FormatModelList(JsonElement models)
    {
        var count = models.GetArrayLength();
        if (count == 0) return "no models";

        var names = new List<string>();
        foreach (var m in models.EnumerateArray())
        {
            if (m.TryGetProperty("name", out var name))
                names.Add(name.GetString() ?? "");
            if (names.Count >= 3) break; // show max 3
        }
        var display = string.Join(", ", names);
        return count > 3 ? $"{display} +{count - 3} more" : display;
    }

    private static string FormatModelNames(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Array) return "";
        var count = data.GetArrayLength();
        if (count == 0) return "no models";

        var names = new List<string>();
        foreach (var m in data.EnumerateArray())
        {
            if (m.TryGetProperty("id", out var id))
                names.Add(id.GetString() ?? "");
            if (names.Count >= 3) break;
        }
        var display = string.Join(", ", names);
        return count > 3 ? $"{display} +{count - 3} more" : display;
    }

    private static string FormatTgiInfo(JsonElement root, JsonElement modelId)
    {
        var name = modelId.GetString() ?? "";
        if (root.TryGetProperty("model_dtype", out var dtype))
            name += $" ({dtype.GetString()})";
        return name;
    }

    private static string FormatLlamaCppInfo(JsonElement gs)
    {
        var parts = new List<string>();
        if (gs.TryGetProperty("model", out var model))
        {
            var m = model.GetString() ?? "";
            if (m.Length > 40) m = m[..40] + "...";
            parts.Add(m);
        }
        if (gs.TryGetProperty("n_ctx", out var ctx))
            parts.Add($"ctx:{ctx.GetInt32()}");
        return string.Join(", ", parts);
    }

    private static string FormatLitellmModels(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Array) return "";
        var count = data.GetArrayLength();
        if (count == 0) return "no models";

        var names = new List<string>();
        foreach (var m in data.EnumerateArray())
        {
            if (m.TryGetProperty("model_name", out var name))
                names.Add(name.GetString() ?? "");
            else if (m.TryGetProperty("id", out var id))
                names.Add(id.GetString() ?? "");
            if (names.Count >= 3) break;
        }
        var display = string.Join(", ", names);
        return count > 3 ? $"{display} +{count - 3} more" : display;
    }

    private static string FormatSdModels(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) return "";
        var count = root.GetArrayLength();
        if (count == 0) return "no models";

        var names = new List<string>();
        foreach (var m in root.EnumerateArray())
        {
            if (m.TryGetProperty("model_name", out var name))
                names.Add(name.GetString() ?? "");
            else if (m.TryGetProperty("title", out var title))
                names.Add(title.GetString() ?? "");
            if (names.Count >= 3) break;
        }
        var display = string.Join(", ", names);
        return count > 3 ? $"{display} +{count - 3} more" : display;
    }

    private static string FormatComfyInfo(JsonElement sys)
    {
        var parts = new List<string>();
        if (sys.TryGetProperty("system", out var inner))
        {
            if (inner.TryGetProperty("os", out var os))
                parts.Add(os.GetString() ?? "");
        }
        if (sys.TryGetProperty("devices", out var devices) && devices.GetArrayLength() > 0)
        {
            foreach (var d in devices.EnumerateArray())
            {
                if (d.TryGetProperty("name", out var name))
                    parts.Add(name.GetString() ?? "");
                if (d.TryGetProperty("vram_total", out var vram))
                {
                    var gb = vram.GetInt64() / (1024.0 * 1024 * 1024);
                    parts.Add($"{gb:F1}GB VRAM");
                }
                break; // first device only
            }
        }
        return string.Join(", ", parts);
    }

    private static string FormatTritonModels(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) return "";
        var count = root.GetArrayLength();
        if (count == 0) return "no models";

        var names = new List<string>();
        foreach (var m in root.EnumerateArray())
        {
            if (m.TryGetProperty("name", out var name))
                names.Add(name.GetString() ?? "");
            if (names.Count >= 3) break;
        }
        var display = string.Join(", ", names);
        return count > 3 ? $"{display} +{count - 3} more" : display;
    }

    private static string FormatTorchServeModels(JsonElement models)
    {
        if (models.ValueKind != JsonValueKind.Array) return "";
        var count = models.GetArrayLength();
        if (count == 0) return "no models";

        var names = new List<string>();
        foreach (var m in models.EnumerateArray())
        {
            if (m.TryGetProperty("modelName", out var name))
                names.Add(name.GetString() ?? "");
            if (names.Count >= 3) break;
        }
        var display = string.Join(", ", names);
        return count > 3 ? $"{display} +{count - 3} more" : display;
    }

    private static string FormatTfServingInfo(JsonElement mvs)
    {
        if (mvs.ValueKind != JsonValueKind.Array || mvs.GetArrayLength() == 0) return "";
        var first = mvs[0];
        var state = first.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";
        return state.ToLowerInvariant() == "available" ? "serving" : state;
    }

    private static string ExtractGpuInfo(string metricsText)
    {
        // Parse Prometheus metrics for GPU names
        var gpus = new HashSet<string>();
        foreach (var line in metricsText.Split('\n'))
        {
            if (!line.Contains("DCGM_FI_DEV_GPU_UTIL")) continue;
            var modelStart = line.IndexOf("modelName=\"", StringComparison.Ordinal);
            if (modelStart < 0) continue;
            modelStart += "modelName=\"".Length;
            var modelEnd = line.IndexOf('"', modelStart);
            if (modelEnd > modelStart)
                gpus.Add(line[modelStart..modelEnd]);
            if (gpus.Count >= 2) break;
        }
        return gpus.Count > 0 ? string.Join(", ", gpus) : "GPU metrics";
    }

    private static string ExtractMetricsSummary(string metricsText, string prefix)
    {
        var count = 0;
        foreach (var line in metricsText.Split('\n'))
        {
            if (line.StartsWith(prefix) && !line.StartsWith('#'))
                count++;
            if (count >= 3) break;
        }
        return count > 0 ? $"{count} metric(s)" : "metrics";
    }
}
