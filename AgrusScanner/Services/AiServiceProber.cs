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

    // ── Probe definitions ordered by specificity (highest first within each service) ──

    private static readonly ProbeDefinition[] Probes =
    [
        // ═══════════════════════════════════════════
        // LLM SERVICES
        // ═══════════════════════════════════════════

        // Ollama — root returns "Ollama is running"
        new()
        {
            Path = "/", ServiceName = "Ollama", Category = "LLM",
            Confidence = "high", Specificity = 100,
            BodyContains = "Ollama is running"
        },
        // Ollama — model list
        new()
        {
            Path = "/api/tags", ServiceName = "Ollama", Category = "LLM",
            Confidence = "high", Specificity = 95,
            BodyContains = "\"models\""
        },
        // vLLM — /version endpoint
        new()
        {
            Path = "/version", ServiceName = "vLLM", Category = "LLM",
            Confidence = "high", Specificity = 90,
            BodyContains = "version"
        },
        // Hugging Face TGI — /info returns model_id
        new()
        {
            Path = "/info", ServiceName = "HF TGI", Category = "LLM",
            Confidence = "high", Specificity = 88,
            BodyContains = "model_id"
        },
        // llama.cpp — /props returns default_generation_settings
        new()
        {
            Path = "/props", ServiceName = "llama.cpp", Category = "LLM",
            Confidence = "high", Specificity = 88,
            BodyContains = "default_generation_settings"
        },
        // llama.cpp — /slots returns inference slot info
        new()
        {
            Path = "/slots", ServiceName = "llama.cpp", Category = "LLM",
            Confidence = "high", Specificity = 85,
            BodyContains = "id"
        },
        // KoboldCpp — /api/v1/info/version
        new()
        {
            Path = "/api/v1/info/version", ServiceName = "KoboldCpp", Category = "LLM",
            Confidence = "high", Specificity = 92,
            BodyContains = "result"
        },
        // KoboldCpp — /api/v1/model
        new()
        {
            Path = "/api/v1/model", ServiceName = "KoboldCpp", Category = "LLM",
            Confidence = "high", Specificity = 85,
            BodyContains = "result"
        },
        // LM Studio — /api/v0 path is unique to LM Studio
        new()
        {
            Path = "/api/v0/models", ServiceName = "LM Studio", Category = "LLM",
            Confidence = "high", Specificity = 85,
            BodyContains = "data"
        },
        // LiteLLM — /model/info
        new()
        {
            Path = "/model/info", ServiceName = "LiteLLM", Category = "LLM",
            Confidence = "high", Specificity = 82,
            BodyContains = "\"data\""
        },
        // LiteLLM — /health/liveliness
        new()
        {
            Path = "/health/liveliness", ServiceName = "LiteLLM", Category = "LLM",
            Confidence = "medium", Specificity = 70,
            BodyContains = "I'm alive"
        },
        // Jan.ai — distinctive port 1337
        new()
        {
            Path = "/v1/models", ServiceName = "Jan.ai", Category = "LLM",
            Confidence = "high", Specificity = 80,
            BodyContains = "\"data\"",
            PortHint = 1337
        },
        // GPT4All — distinctive port 4891
        new()
        {
            Path = "/v1/models", ServiceName = "GPT4All", Category = "LLM",
            Confidence = "high", Specificity = 80,
            BodyContains = "\"data\"",
            PortHint = 4891
        },
        // LocalAI — /models returns model list
        new()
        {
            Path = "/models", ServiceName = "LocalAI", Category = "LLM",
            Confidence = "high", Specificity = 80,
            BodyContains = "LocalAI"
        },
        // LocalAI fallback — /readyz + /v1/models combo (port 8080 typical)
        new()
        {
            Path = "/v1/models", ServiceName = "LocalAI", Category = "LLM",
            Confidence = "medium", Specificity = 65,
            BodyContains = "\"object\"",
            PortHint = 8080
        },
        // FastChat controller — distinctive port 21001
        new()
        {
            Path = "/v1/models", ServiceName = "FastChat", Category = "LLM",
            Confidence = "high", Specificity = 85,
            BodyContains = "\"data\"",
            PortHint = 21001
        },
        // FastChat worker — distinctive port 21002
        new()
        {
            Path = "/", ServiceName = "FastChat Worker", Category = "LLM",
            Confidence = "medium", Specificity = 70,
            StatusCode = 200,
            PortHint = 21002
        },

        // ═══════════════════════════════════════════
        // IMAGE GENERATION
        // ═══════════════════════════════════════════

        // Stable Diffusion A1111 — /sdapi/v1/ is unique
        new()
        {
            Path = "/sdapi/v1/sd-models", ServiceName = "Stable Diffusion (A1111)", Category = "Image Gen",
            Confidence = "high", Specificity = 95,
            StatusCode = 200
        },
        // Stable Diffusion A1111 — options endpoint
        new()
        {
            Path = "/sdapi/v1/options", ServiceName = "Stable Diffusion (A1111)", Category = "Image Gen",
            Confidence = "high", Specificity = 90,
            BodyContains = "sd_model_checkpoint"
        },
        // ComfyUI — /system_stats is unique
        new()
        {
            Path = "/system_stats", ServiceName = "ComfyUI", Category = "Image Gen",
            Confidence = "high", Specificity = 95,
            BodyContains = "system"
        },
        // ComfyUI — /object_info returns node definitions
        new()
        {
            Path = "/object_info", ServiceName = "ComfyUI", Category = "Image Gen",
            Confidence = "high", Specificity = 90,
            StatusCode = 200
        },

        // ═══════════════════════════════════════════
        // ML PLATFORMS / SERVING
        // ═══════════════════════════════════════════

        // NVIDIA Triton — /v2/health/ready is V2 inference protocol
        new()
        {
            Path = "/v2/health/ready", ServiceName = "NVIDIA Triton", Category = "ML Platform",
            Confidence = "high", Specificity = 92,
            StatusCode = 200
        },
        // NVIDIA Triton — model repository
        new()
        {
            Path = "/v2/repository/index", ServiceName = "NVIDIA Triton", Category = "ML Platform",
            Confidence = "high", Specificity = 90,
            StatusCode = 200
        },
        // TorchServe — /ping on inference port
        new()
        {
            Path = "/ping", ServiceName = "TorchServe", Category = "ML Platform",
            Confidence = "high", Specificity = 82,
            BodyContains = "Healthy"
        },
        // TorchServe — /models on management port (8081)
        new()
        {
            Path = "/models", ServiceName = "TorchServe", Category = "ML Platform",
            Confidence = "high", Specificity = 85,
            BodyContains = "models",
            PortHint = 8081
        },
        // TensorFlow Serving — /v1/models
        new()
        {
            Path = "/v1/models", ServiceName = "TensorFlow Serving", Category = "ML Platform",
            Confidence = "high", Specificity = 78,
            BodyContains = "model_version_status",
            PortHint = 8501
        },
        // MLflow — /version is unique to MLflow
        new()
        {
            Path = "/version", ServiceName = "MLflow", Category = "ML Platform",
            Confidence = "high", Specificity = 80,
            StatusCode = 200,
            PortHint = 5000
        },
        // MLflow — API path prefix
        new()
        {
            Path = "/api/2.0/mlflow/experiments/search", ServiceName = "MLflow", Category = "ML Platform",
            Confidence = "high", Specificity = 92,
            StatusCode = 200
        },
        // Ray Serve — dashboard at 8265
        new()
        {
            Path = "/api/serve/deployments/", ServiceName = "Ray Serve", Category = "ML Platform",
            Confidence = "high", Specificity = 88,
            StatusCode = 200,
            PortHint = 8265
        },
        // BentoML — /docs returns BentoML-specific OpenAPI
        new()
        {
            Path = "/docs", ServiceName = "BentoML", Category = "ML Platform",
            Confidence = "high", Specificity = 80,
            BodyContains = "BentoML",
            PortHint = 3000
        },
        // KServe V2 — /v2/health/ready
        new()
        {
            Path = "/v2/health/ready", ServiceName = "KServe", Category = "ML Platform",
            Confidence = "medium", Specificity = 75,
            StatusCode = 200
        },
        // MindsDB — distinctive port 47334
        new()
        {
            Path = "/", ServiceName = "MindsDB", Category = "ML Platform",
            Confidence = "high", Specificity = 90,
            StatusCode = 200,
            PortHint = 47334
        },
        // Tabby — /v1/health
        new()
        {
            Path = "/v1/health", ServiceName = "Tabby", Category = "LLM",
            Confidence = "high", Specificity = 78,
            BodyContains = "model"
        },

        // ═══════════════════════════════════════════
        // AI CHAT PLATFORMS / UIs
        // ═══════════════════════════════════════════

        // Open WebUI — root contains "Open WebUI"
        new()
        {
            Path = "/", ServiceName = "Open WebUI", Category = "AI Platform",
            Confidence = "high", Specificity = 90,
            BodyContains = "Open WebUI"
        },
        // AnythingLLM — /api/health returns { online: true }
        new()
        {
            Path = "/api/health", ServiceName = "AnythingLLM", Category = "AI Platform",
            Confidence = "high", Specificity = 78,
            BodyContains = "online"
        },
        // LibreChat — root contains "LibreChat"
        new()
        {
            Path = "/", ServiceName = "LibreChat", Category = "AI Platform",
            Confidence = "high", Specificity = 88,
            BodyContains = "LibreChat"
        },
        // Flowise — /api/v1/chatflows is unique to Flowise
        new()
        {
            Path = "/api/v1/chatflows", ServiceName = "Flowise", Category = "AI Platform",
            Confidence = "high", Specificity = 90,
            StatusCode = 200
        },
        // Flowise — auth-protected instance returns 401
        new()
        {
            Path = "/api/v1/chatflows", ServiceName = "Flowise", Category = "AI Platform",
            Confidence = "medium", Specificity = 85,
            StatusCode = 401
        },
        // Dify — /console/api/
        new()
        {
            Path = "/console/api/setup", ServiceName = "Dify", Category = "AI Platform",
            Confidence = "high", Specificity = 88,
            StatusCode = 200
        },
        // SillyTavern — root contains "SillyTavern"
        new()
        {
            Path = "/", ServiceName = "SillyTavern", Category = "AI Platform",
            Confidence = "high", Specificity = 88,
            BodyContains = "SillyTavern",
            PortHint = 8000
        },
        // n8n — root contains "n8n"
        new()
        {
            Path = "/", ServiceName = "n8n", Category = "AI Platform",
            Confidence = "high", Specificity = 85,
            BodyContains = "n8n",
            PortHint = 5678
        },
        // PrivateGPT — /v1/health
        new()
        {
            Path = "/v1/health", ServiceName = "PrivateGPT", Category = "AI Platform",
            Confidence = "high", Specificity = 85,
            BodyContains = "private_gpt",
            PortHint = 8001
        },

        // ═══════════════════════════════════════════
        // LLM SERVING (additional)
        // ═══════════════════════════════════════════

        // Xinference — /v1/cluster/info is unique
        new()
        {
            Path = "/v1/cluster/info", ServiceName = "Xinference", Category = "LLM",
            Confidence = "high", Specificity = 92,
            StatusCode = 200
        },
        // SGLang — /get_model_info is unique to SGLang
        new()
        {
            Path = "/get_model_info", ServiceName = "SGLang", Category = "LLM",
            Confidence = "high", Specificity = 90,
            StatusCode = 200
        },
        // text-generation-webui (Oobabooga) — /api/v1/model returns single model
        new()
        {
            Path = "/api/v1/model", ServiceName = "text-generation-webui", Category = "LLM",
            Confidence = "high", Specificity = 82,
            BodyContains = "result",
            PortHint = 5000
        },
        // InvokeAI — /api/v1/app/version
        new()
        {
            Path = "/api/v1/app/version", ServiceName = "InvokeAI", Category = "Image Gen",
            Confidence = "high", Specificity = 92,
            StatusCode = 200,
            PortHint = 9090
        },

        // ═══════════════════════════════════════════
        // VECTOR DATABASES
        // ═══════════════════════════════════════════

        // Qdrant — /collections
        new()
        {
            Path = "/collections", ServiceName = "Qdrant", Category = "Vector DB",
            Confidence = "high", Specificity = 90,
            BodyContains = "\"collections\"",
            PortHint = 6333
        },
        // ChromaDB — /api/v1/heartbeat
        new()
        {
            Path = "/api/v1/heartbeat", ServiceName = "ChromaDB", Category = "Vector DB",
            Confidence = "high", Specificity = 92,
            StatusCode = 200,
            PortHint = 8000
        },
        // Weaviate — /v1/meta
        new()
        {
            Path = "/v1/meta", ServiceName = "Weaviate", Category = "Vector DB",
            Confidence = "high", Specificity = 90,
            BodyContains = "\"version\"",
            PortHint = 8080
        },
        // Milvus — /healthz on management port
        new()
        {
            Path = "/healthz", ServiceName = "Milvus", Category = "Vector DB",
            Confidence = "high", Specificity = 85,
            StatusCode = 200,
            PortHint = 9091
        },

        // ═══════════════════════════════════════════
        // MCP SERVERS
        // ═══════════════════════════════════════════

        // Agrus Scanner MCP — /mcp endpoint with SSE transport
        new()
        {
            Path = "/mcp", ServiceName = "Agrus Scanner MCP", Category = "MCP Server",
            Confidence = "high", Specificity = 88,
            BodyContains = "agrus-scanner"
        },

        // ═══════════════════════════════════════════
        // GPU / INFRASTRUCTURE
        // ═══════════════════════════════════════════

        // NVIDIA DCGM Exporter — /metrics with DCGM prefix
        new()
        {
            Path = "/metrics", ServiceName = "NVIDIA DCGM", Category = "GPU Infra",
            Confidence = "high", Specificity = 95,
            BodyContains = "DCGM_FI_",
            PortHint = 9400
        },
        // Triton Metrics — nv_inference on port 8002
        new()
        {
            Path = "/metrics", ServiceName = "Triton Metrics", Category = "GPU Infra",
            Confidence = "high", Specificity = 92,
            BodyContains = "nv_inference_",
            PortHint = 8002
        },
        // TorchServe Metrics — port 8082
        new()
        {
            Path = "/metrics", ServiceName = "TorchServe Metrics", Category = "GPU Infra",
            Confidence = "high", Specificity = 85,
            BodyContains = "ts_inference_",
            PortHint = 8082
        },

        // ═══════════════════════════════════════════
        // CONTAINER DETECTION (Docker API)
        // ═══════════════════════════════════════════

        // Docker API — /containers/json
        new()
        {
            Path = "/containers/json", ServiceName = "Docker API", Category = "Container",
            Confidence = "high", Specificity = 95,
            StatusCode = 200,
            PortHint = 2375
        },

        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════

        // OpenAI-compatible — /v1/models (many services implement this)
        new()
        {
            Path = "/v1/models", ServiceName = "OpenAI-compatible", Category = "LLM",
            Confidence = "medium", Specificity = 50,
            BodyContains = "\"data\""
        },
        // LM Studio / text-generation-webui — /api/v1/models
        new()
        {
            Path = "/api/v1/models", ServiceName = "LM Studio / TGW", Category = "LLM",
            Confidence = "medium", Specificity = 55,
            BodyContains = "\"data\""
        },
        // Gradio detection — root page loads Gradio JS framework
        new()
        {
            Path = "/", ServiceName = "Gradio AI App", Category = "AI Platform",
            Confidence = "medium", Specificity = 70,
            BodyContains = "gradio-app"
        },
    ];

    // ── Known AI-related Docker image patterns ──

    private static readonly string[] AiDockerPatterns =
    [
        "ollama", "localai", "vllm", "text-generation-inference",
        "tritonserver", "torchserve", "tensorflow/serving",
        "stable-diffusion", "comfyui", "open-webui", "anythingllm",
        "librechat", "flowise", "dify", "litellm", "koboldcpp",
        "tabbyml", "whisper", "llama", "mistral", "deepseek",
        "qdrant", "chromadb", "weaviate", "milvus", "bentoml",
        "langchain", "langserve", "ray", "mlflow", "mindsdb",
        "privategpt", "gpt4all", "xinference", "sglang",
        "text-generation-webui", "oobabooga", "invokeai",
        "sillytavern", "n8n", "llamafile", "agrus"
    ];

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

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("User-Agent", "AgrusScanner/1.0");

                    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

                    // Check status code match
                    if (probe.StatusCode.HasValue && (int)response.StatusCode != probe.StatusCode.Value)
                        continue;

                    string? body = null;

                    // If we need to check body content, read it
                    if (probe.BodyContains != null || probe.HeaderContains != null || NeedsDetailExtraction(probe))
                    {
                        body = await response.Content.ReadAsStringAsync(ct);
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
            request.Headers.Add("User-Agent", "AgrusScanner/1.0");

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
        catch { }
        return aiContainers;
    }

    private static bool NeedsDetailExtraction(ProbeDefinition probe)
    {
        // Services where we want to read the body even for status-code-only probes
        return probe.ServiceName is "Docker API" or "NVIDIA Triton" or "ComfyUI"
            or "TorchServe" or "MLflow" or "Ray Serve";
    }

    private static string TryExtractDetails(string service, string path, string body, int port)
    {
        try
        {
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

    private class ProbeDefinition
    {
        public string Path { get; init; } = "/";
        public string ServiceName { get; init; } = "";
        public string Category { get; init; } = "";
        public string Confidence { get; init; } = "low";
        public int Specificity { get; init; }
        public int? StatusCode { get; init; }
        public string? BodyContains { get; init; }
        public string? HeaderContains { get; init; }
        public int? PortHint { get; init; } // only run this probe on this specific port
    }
}
