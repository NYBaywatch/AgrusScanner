using System.Net.Http;
using System.Text.Json;
using AgrusScanner.Models;

namespace AgrusScanner.Services;

public class AiServiceProber
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    private readonly SemaphoreSlim _semaphore = new(32);

    private static readonly ProbeDefinition[] Probes =
    [
        // Ollama — root page returns "Ollama is running"
        new()
        {
            Path = "/",
            ServiceName = "Ollama",
            Confidence = "high",
            Specificity = 100,
            BodyContains = "Ollama is running"
        },
        // Ollama — model list endpoint
        new()
        {
            Path = "/api/tags",
            ServiceName = "Ollama",
            Confidence = "high",
            Specificity = 95,
            BodyContains = "\"models\""
        },
        // vLLM — version endpoint
        new()
        {
            Path = "/version",
            ServiceName = "vLLM",
            Confidence = "high",
            Specificity = 90,
            BodyContains = "version"
        },
        // vLLM / LiteLLM / OpenAI-compatible — /v1/models
        new()
        {
            Path = "/v1/models",
            ServiceName = "OpenAI-compatible",
            Confidence = "medium",
            Specificity = 50,
            BodyContains = "\"data\""
        },
        // LM Studio / text-generation-webui — /api/v1/models
        new()
        {
            Path = "/api/v1/models",
            ServiceName = "LM Studio / TGW",
            Confidence = "medium",
            Specificity = 60,
            BodyContains = "\"data\""
        },
        // LocalAI — /api/health or body mentions localai
        new()
        {
            Path = "/api/health",
            ServiceName = "LocalAI",
            Confidence = "medium",
            Specificity = 70,
            StatusCode = 200
        },
        // Generic health endpoint
        new()
        {
            Path = "/health",
            ServiceName = "LLM Service",
            Confidence = "low",
            Specificity = 20,
            StatusCode = 200
        },
        // Hugging Face TGI — /info returns model_id
        new()
        {
            Path = "/info",
            ServiceName = "HF TGI",
            Confidence = "high",
            Specificity = 85,
            BodyContains = "model_id"
        },
        // Open WebUI — root page contains "Open WebUI"
        new()
        {
            Path = "/",
            ServiceName = "Open WebUI",
            Confidence = "high",
            Specificity = 90,
            BodyContains = "Open WebUI"
        },
        // llama.cpp — /props endpoint
        new()
        {
            Path = "/props",
            ServiceName = "llama.cpp",
            Confidence = "high",
            Specificity = 85,
            BodyContains = "default_generation_settings"
        },
        // AnythingLLM — /api/health returns { online: true }
        new()
        {
            Path = "/api/health",
            ServiceName = "AnythingLLM",
            Confidence = "high",
            Specificity = 75,
            BodyContains = "online"
        },
        // LiteLLM — /model/info
        new()
        {
            Path = "/model/info",
            ServiceName = "LiteLLM",
            Confidence = "high",
            Specificity = 80,
            BodyContains = "\"data\""
        },
    ];

    public async Task<AiServiceResult?> ProbeAsync(string ip, int port, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            AiServiceResult? best = null;

            foreach (var probe in Probes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var scheme = port == 8443 ? "https" : "http";
                    var url = $"{scheme}://{ip}:{port}{probe.Path}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("User-Agent", "AgrusScanner/1.0");

                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

                    // Check status code match
                    if (probe.StatusCode.HasValue && (int)response.StatusCode != probe.StatusCode.Value)
                        continue;

                    // If we only need a status code match (no body check), accept it
                    if (probe.BodyContains == null && probe.HeaderContains == null && probe.StatusCode.HasValue)
                    {
                        if (best == null || probe.Specificity > best.Specificity)
                        {
                            best = new AiServiceResult
                            {
                                ServiceName = probe.ServiceName,
                                Port = port,
                                Confidence = probe.Confidence,
                                Specificity = probe.Specificity
                            };
                        }
                        continue;
                    }

                    var body = await response.Content.ReadAsStringAsync(ct);

                    // Check body contains
                    if (probe.BodyContains != null)
                    {
                        if (!body.Contains(probe.BodyContains, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var details = TryExtractDetails(probe.ServiceName, body);

                        if (best == null || probe.Specificity > best.Specificity)
                        {
                            best = new AiServiceResult
                            {
                                ServiceName = probe.ServiceName,
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
                catch
                {
                    // Probe failed (timeout, connection refused, etc.) — skip
                }
            }

            return best;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string TryExtractDetails(string service, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            return service switch
            {
                "vLLM" when root.TryGetProperty("version", out var v) => v.GetString() ?? "",
                "Ollama" when root.TryGetProperty("models", out var models) =>
                    $"{models.GetArrayLength()} model(s)",
                "HF TGI" when root.TryGetProperty("model_id", out var m) => m.GetString() ?? "",
                _ => ""
            };
        }
        catch
        {
            return "";
        }
    }

    private class ProbeDefinition
    {
        public string Path { get; init; } = "/";
        public string ServiceName { get; init; } = "";
        public string Confidence { get; init; } = "low";
        public int Specificity { get; init; }
        public int? StatusCode { get; init; }
        public string? BodyContains { get; init; }
        public string? HeaderContains { get; init; }
    }
}
