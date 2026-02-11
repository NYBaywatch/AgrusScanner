# AI Service Detection Research - Comprehensive Reference

**Researched:** 2026-02-10
**Domain:** Network detection signatures for self-hosted AI/ML services, shadow AI detection, AI service fingerprinting
**Purpose:** Build detection signatures into the AgrusScanner network scanner

---

## Table of Contents

1. [Self-Hosted LLM/AI Services](#1-self-hosted-llmai-services)
2. [Shadow AI Detection - Cloud Provider Domains](#2-shadow-ai-detection---cloud-provider-domains)
3. [AI Service Fingerprinting Techniques](#3-ai-service-fingerprinting-techniques)
4. [GPU/Infrastructure Indicators](#4-gpuinfrastructure-indicators)
5. [Container-Based AI Detection](#5-container-based-ai-detection)
6. [Self-Hosted AI Chat/Platform UIs](#6-self-hosted-ai-chatplatform-uis)
7. [Kubernetes AI Inference Platforms](#7-kubernetes-ai-inference-platforms)

---

## 1. Self-Hosted LLM/AI Services

### 1.1 Ollama

| Property | Value |
|----------|-------|
| **Default Port** | 11434 |
| **Root Endpoint** | `GET /` returns plain text `"Ollama is running"` |
| **API Base** | `/api/` |
| **Key Endpoints** | `/api/tags` (list models), `/api/generate`, `/api/chat`, `/api/show`, `/api/embeddings`, `/api/ps` (running models), `/api/version` |
| **OpenAI-Compatible** | `/v1/chat/completions`, `/v1/models` |
| **Banner/Fingerprint** | Root returns `"Ollama is running"` plain text. Shodan query: `port:11434 "Ollama"`. Many instances also show `Server: uvicorn` header when behind a proxy. |
| **Version Detection** | `/api/version` returns JSON `{"version":"X.Y.Z"}` |
| **Health Check** | `GET /` - 200 OK with `"Ollama is running"` |
| **Docker Image** | `ollama/ollama` |
| **Notes** | 175,000+ publicly exposed instances found (Jan 2026 SentinelLABS/Censys). 88.9% use OpenAI-compatible API schema. No authentication by default. |

### 1.2 LocalAI

| Property | Value |
|----------|-------|
| **Default Port** | 8080 |
| **API Base** | `/v1/` (OpenAI-compatible) |
| **Key Endpoints** | `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`, `/v1/audio/transcriptions`, `/v1/images/generations`, `/v1/models`, `/v1/messages` (Anthropic-compatible) |
| **Unique Endpoints** | `/models/apply` (model management), `/readyz` (health check) |
| **Web UI** | Accessible at root `/` |
| **Banner/Fingerprint** | FastAPI/Uvicorn backend. Web UI at root path. |
| **Docker Image** | `localai/localai` (variants: `latest-gpu-nvidia-cuda-12`, `latest-aio-cpu`) |

### 1.3 LM Studio

| Property | Value |
|----------|-------|
| **Default Port** | 1234 |
| **API Base** | `/v1/` (OpenAI-compatible) |
| **Key Endpoints** | `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`, `/v1/models` |
| **REST API v0** | `/api/v0/chat/completions`, `/api/v0/completions`, `/api/v0/embeddings` |
| **Banner/Fingerprint** | LM Studio specific REST API at `/api/v0/` path in addition to OpenAI-compatible `/v1/` |
| **Version Detection** | Model listing at `/v1/models` includes LM Studio-loaded model identifiers |
| **Notes** | Listens on 127.0.0.1 by default. Can be configured to listen on 0.0.0.0. |

### 1.4 vLLM

| Property | Value |
|----------|-------|
| **Default Port** | 8000 |
| **API Base** | `/v1/` (OpenAI-compatible) |
| **Key Endpoints** | `/v1/completions`, `/v1/chat/completions`, `/v1/models`, `/v1/embeddings`, `/v1/tokenize`, `/v1/detokenize` |
| **Unique Endpoints** | `/rerank`, `/v1/rerank`, `/v2/rerank`, `/health`, `/version` |
| **Banner/Fingerprint** | Runs on Uvicorn (FastAPI). `Server: uvicorn` header. Optional `X-Request-Id` header with `--enable-request-id-headers`. |
| **Health Check** | `/health` endpoint |
| **Docker Image** | `vllm/vllm-openai` |
| **Notes** | Supports `--api-key` flag for bearer token auth. One of the most commonly exposed LLM servers alongside Ollama. |

### 1.5 Text Generation WebUI (oobabooga)

| Property | Value |
|----------|-------|
| **Default Port** | 7860 (Web UI), 5000 (API) |
| **API Base** | `/v1/` (OpenAI-compatible) |
| **Key Endpoints** | `/v1/chat/completions`, `/v1/completions`, `/v1/models`, `/v1/embeddings` |
| **Docs** | `/docs` (Swagger/OpenAPI auto-generated) |
| **Banner/Fingerprint** | Gradio-based Web UI on port 7860. API on separate port 5000. |
| **Activation** | API requires `--api` flag to enable |
| **Auth** | Optional `--api-key` flag for bearer token |
| **Notes** | Built on Gradio framework. Web UI has characteristic Gradio HTML/JS signatures. |

### 1.6 Stable Diffusion WebUI (AUTOMATIC1111)

| Property | Value |
|----------|-------|
| **Default Port** | 7860 |
| **API Base** | `/sdapi/v1/` |
| **Key Endpoints** | `/sdapi/v1/txt2img`, `/sdapi/v1/img2img`, `/sdapi/v1/options`, `/sdapi/v1/cmd-flags`, `/sdapi/v1/samplers`, `/sdapi/v1/sd-models`, `/sdapi/v1/progress` |
| **Docs** | `/docs` (Swagger/OpenAPI) |
| **Banner/Fingerprint** | Gradio Web UI. Unique `/sdapi/v1/` path prefix is distinctive. API requires `--api` flag. |
| **Health/Detection** | Presence of `/sdapi/v1/` endpoints is unique to A1111. |

### 1.7 ComfyUI

| Property | Value |
|----------|-------|
| **Default Port** | 8188 |
| **API Base** | `/` |
| **Key Endpoints** | `/prompt` (execute workflow), `/queue` (queue status), `/history`, `/view` (view images), `/upload/image`, `/object_info`, `/system_stats` |
| **WebSocket** | WebSocket connection at root `/ws` for real-time progress tracking |
| **Banner/Fingerprint** | Custom web interface (not Gradio). WebSocket-based progress. `/object_info` returns node definitions. `/system_stats` returns system info. |
| **Detection** | `/system_stats` is highly unique - returns GPU info, VRAM, Python version |

### 1.8 TensorFlow Serving

| Property | Value |
|----------|-------|
| **Default Ports** | 8501 (REST), 8500 (gRPC) |
| **API Base** | `/v1/models/` |
| **Key Endpoints** | `GET /v1/models/{MODEL}` (status), `POST /v1/models/{MODEL}:predict`, `POST /v1/models/{MODEL}:classify`, `POST /v1/models/{MODEL}:regress` |
| **Version Endpoint** | `/v1/models/{MODEL}/versions/{VER}` |
| **Banner/Fingerprint** | Distinctive `/v1/models/{name}:predict` URL pattern with colon syntax. Model status returns TF-specific metadata. |
| **Docker Image** | `tensorflow/serving`, `bitnami/tensorflow-serving` |

### 1.9 TorchServe

| Property | Value |
|----------|-------|
| **Default Ports** | 8080 (Inference), 8081 (Management), 8082 (Metrics) |
| **Inference Endpoints** | `GET /ping` (health), `POST /predictions/{MODEL}`, `POST /explanations/{MODEL}` |
| **Management Endpoints** | `GET /models` (list), `POST /models` (register), `DELETE /models/{MODEL}`, `PUT /models/{MODEL}` (scale) |
| **Metrics Endpoint** | `GET /metrics` (Prometheus format on port 8082) |
| **Banner/Fingerprint** | Three distinct ports is highly distinctive. `/ping` health check on inference port. Management API on separate port. |
| **Docker Image** | `pytorch/torchserve` |
| **Auth** | Optional Bearer token via Authorization header |

### 1.10 NVIDIA Triton Inference Server

| Property | Value |
|----------|-------|
| **Default Ports** | 8000 (HTTP), 8001 (gRPC), 8002 (Metrics) |
| **Key Endpoints** | `/v2/health/ready`, `/v2/health/live`, `/v2/models/{MODEL}/infer`, `/v2/models/{MODEL}/ready`, `/v2/models`, `/v2/repository/index` |
| **Metrics** | `GET /metrics` on port 8002 (Prometheus format) |
| **Banner/Fingerprint** | Three-port pattern (8000/8001/8002). V2 inference protocol paths. Startup logs show "Started HTTPService", "Started GRPCInferenceService", "Started Metrics Service". |
| **Docker Image** | `nvcr.io/nvidia/tritonserver` (NGC registry, not Docker Hub) |

### 1.11 MLflow

| Property | Value |
|----------|-------|
| **Default Port** | 5000 |
| **API Base** | `/api/2.0/mlflow/` |
| **Key Endpoints** | `/api/2.0/mlflow/experiments/search`, `/api/2.0/mlflow/experiments/create`, `/api/2.0/mlflow/runs/create`, `/api/2.0/mlflow/runs/log-metric` |
| **Version Detection** | `GET /version` returns MLflow version string |
| **Health/Detection** | `/version` endpoint is unique to MLflow. Web UI at root. API prefix `/api/2.0/mlflow/` is distinctive. |
| **Banner/Fingerprint** | Flask/Starlette backend. Version endpoint. MLflow-specific API path structure. |

### 1.12 Ray Serve

| Property | Value |
|----------|-------|
| **Default Ports** | 8000 (HTTP proxy), 9000 (gRPC proxy), 8265 (Dashboard/REST API) |
| **Dashboard Endpoints** | Ray Dashboard at port 8265. Serve REST API at same port. |
| **Key Endpoints** | `/api/serve/deployments/` (list deployments), `/api/serve/applications/` |
| **Banner/Fingerprint** | Ray Dashboard UI at port 8265. Dashboard contains Ray-specific metadata. |
| **Detection** | Dashboard at 8265 is distinctive for Ray clusters. |

### 1.13 BentoML

| Property | Value |
|----------|-------|
| **Default Port** | 3000 |
| **Key Endpoints** | Service-defined routes (e.g., `/predict`, `/generate`), `/docs` (Swagger), `/healthz`, `/livez`, `/readyz`, `/metrics` |
| **Banner/Fingerprint** | Custom service routes. Health endpoints follow Kubernetes probe conventions. Prometheus metrics at `/metrics`. |
| **Docker Image** | Custom-built via `bentoml build` and `bentoml containerize` |

### 1.14 Hugging Face Text Generation Inference (TGI)

| Property | Value |
|----------|-------|
| **Default Port** | 8080 (host), maps to 80 inside container |
| **Key Endpoints** | `/generate`, `/generate_stream`, `/v1/chat/completions` (OpenAI-compatible), `/v1/completions`, `/v1/models` |
| **Docs** | `/docs` (Swagger UI) |
| **Health Check** | `/health` endpoint |
| **Info Endpoint** | `/info` returns model info and configuration |
| **Banner/Fingerprint** | `/info` endpoint returns TGI-specific metadata including model_id, model_dtype, max_batch_total_tokens, etc. `/health` and `/generate` are distinctive combination. |
| **Docker Image** | `ghcr.io/huggingface/text-generation-inference` |

### 1.15 FastChat

| Property | Value |
|----------|-------|
| **Default Ports** | 21001 (Controller), 21002 (Model Worker), 8000 (OpenAI API Server), 7860 (Gradio Web Server) |
| **Key Endpoints** | `/v1/chat/completions`, `/v1/completions`, `/v1/models`, `/v1/embeddings` |
| **Docs** | `/docs` at port 8000 |
| **Banner/Fingerprint** | Multi-component architecture with 4 distinct ports. Controller at unusual port 21001 is distinctive. FastAPI/Uvicorn backend. |

### 1.16 Jan.ai

| Property | Value |
|----------|-------|
| **Default Port** | 1337 |
| **API Base** | `/v1/` (OpenAI-compatible) |
| **Key Endpoints** | `/v1/chat/completions`, `/v1/models`, `/v1/threads`, `/v1/messages` |
| **Banner/Fingerprint** | Port 1337 is distinctive (hacker culture "leet" port). OpenAI-compatible API. |
| **Auth** | Configurable API key in settings |
| **Notes** | Listens on 127.0.0.1 by default. Must enable API server in settings. |

### 1.17 GPT4All

| Property | Value |
|----------|-------|
| **Default Port** | 4891 |
| **API Base** | `/v1/` (OpenAI-compatible) |
| **Key Endpoints** | `/v1/chat/completions`, `/v1/completions`, `/v1/models` |
| **Banner/Fingerprint** | Port 4891 is distinctive/unique to GPT4All. HTTP only (no HTTPS). IPv4 only (127.0.0.1, not ::1). |
| **Notes** | Must enable "Enable Local API Server" in application settings. |

### 1.18 LangServe

| Property | Value |
|----------|-------|
| **Default Port** | 8000 |
| **Key Endpoints** | `/{chain}/invoke`, `/{chain}/batch`, `/{chain}/stream`, `/{chain}/stream_log`, `/{chain}/input_schema`, `/{chain}/output_schema`, `/{chain}/playground/` |
| **Docs** | `/docs` (auto-generated Swagger with JSONSchema) |
| **Banner/Fingerprint** | Distinctive `/invoke`, `/batch`, `/stream` pattern under chain paths. Interactive `/playground/` endpoint. FastAPI/Uvicorn backend. |

### 1.19 KoboldAI / KoboldCpp

| Property | Value |
|----------|-------|
| **Default Port** | 5001 |
| **API Base** | `/api/` (Kobold API), `/v1/` (OpenAI-compatible) |
| **Key Endpoints** | `/api/v1/generate`, `/api/v1/model`, `/api/v1/info/version`, `/api/v1/config/max_context_length`, `/api/v1/config/max_length` |
| **Additional API Emulation** | `/api/chat`, `/api/generate` (Ollama), `/prompt` (ComfyUI), plus A1111/Forge, Whisper, XTTS emulation endpoints |
| **Banner/Fingerprint** | CORS header `access-control-allow-origin: *` always set. Multi-API emulation is distinctive. `/api/v1/info/version` returns KoboldCpp version. |
| **Notes** | Wide-open CORS allows any webpage to connect to localhost:5001. |

### 1.20 llama.cpp Server

| Property | Value |
|----------|-------|
| **Default Port** | 8080 |
| **Key Endpoints** | `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`, `/v1/models`, `/completion` (native), `/health`, `/slots`, `/props`, `/metrics` |
| **Health Check** | `/health` returns JSON with status, slots_idle, slots_processing |
| **Banner/Fingerprint** | `/slots` endpoint is unique - returns active inference slot information. `/props` returns server properties. Native `/completion` endpoint alongside OpenAI-compatible `/v1/` paths. |
| **Version/Info** | `/props` returns server configuration including model info |

### 1.21 Tabby (AI Coding Assistant)

| Property | Value |
|----------|-------|
| **Default Port** | 8080 |
| **Key Endpoints** | `/v1/completions` (code completions), `/v1/chat/completions`, `/v1/health`, `/v1/events` |
| **Banner/Fingerprint** | Code-completion specific. Web UI at root. Tabby-specific response metadata in completions. |
| **Docker Image** | `tabbyml/tabby` |

### 1.22 MindsDB

| Property | Value |
|----------|-------|
| **Default Ports** | 47335 (MySQL-compatible), 47334 (REST API) |
| **Key Endpoints** | REST API at port 47334, MySQL wire protocol at 47335 |
| **Banner/Fingerprint** | Highly distinctive ports (47335, 47334). MySQL wire protocol on non-standard port. Flask/Starlette REST API. |

---

## 2. Shadow AI Detection - Cloud Provider Domains

### 2.1 Primary AI API Domains (Outbound Traffic Monitoring)

| Provider | API Domain(s) | Auth Header Pattern | Notes |
|----------|--------------|---------------------|-------|
| **OpenAI** | `api.openai.com` | `Authorization: Bearer sk-...` | Prefix `sk-` for API keys. ~66% of enterprises have users calling this. |
| **Anthropic** | `api.anthropic.com` | `x-api-key: sk-ant-...`, `anthropic-version: 2023-06-01` | Unique `x-api-key` + `anthropic-version` header combo. ~13% of enterprises. |
| **Google AI (Gemini)** | `generativelanguage.googleapis.com` | `x-goog-api-key: ...` or OAuth | Path: `/v1beta/models/...` |
| **Google Vertex AI** | `{region}-aiplatform.googleapis.com` | OAuth/Service Account | Region-prefixed domain pattern |
| **Azure OpenAI** | `{instance}.openai.azure.com` | `api-key: ...` header | Custom subdomain per instance. Path includes `/openai/deployments/` |
| **AWS Bedrock** | `bedrock-runtime.{region}.amazonaws.com` | AWS SigV4 signing | Region-specific domains. Service name: `bedrock-runtime` |
| **AWS SageMaker** | `runtime.sagemaker.{region}.amazonaws.com` | AWS SigV4 signing | Region-specific domains |
| **Cohere** | `api.cohere.com` or `api.cohere.ai` | `Authorization: Bearer ...` | V1 and V2 API versions |
| **Mistral AI** | `api.mistral.ai` | `Authorization: Bearer ...` | Path: `/v1/chat/completions` |
| **Perplexity AI** | `api.perplexity.ai` | `Authorization: Bearer pplx-...` | Key prefix `pplx-` |
| **Groq** | `api.groq.com` | `Authorization: Bearer gsk_...` | Path: `/openai/v1/`. Key prefix `gsk_` |
| **Together AI** | `api.together.xyz` | `Authorization: Bearer ...` | OpenAI-compatible |
| **Fireworks AI** | `api.fireworks.ai` | `Authorization: Bearer ...` | Path: `/inference/v1/` |
| **Replicate** | `api.replicate.com` | `Authorization: Bearer r8_...` | Key prefix `r8_` |
| **DeepSeek** | `api.deepseek.com` | `Authorization: Bearer sk-...` | OpenAI-compatible |
| **Hugging Face** | `api-inference.huggingface.co`, `router.huggingface.co` | `Authorization: Bearer hf_...` | Key prefix `hf_` |
| **xAI (Grok)** | `api.x.ai` | `Authorization: Bearer xai-...` | Key prefix `xai-` |
| **OpenRouter** | `openrouter.ai` | `Authorization: Bearer sk-or-...` | Key prefix `sk-or-` |

### 2.2 API Key Prefix Patterns (for DLP/traffic inspection)

| Provider | Key Prefix | Example Pattern |
|----------|-----------|-----------------|
| OpenAI | `sk-` | `sk-proj-...`, `sk-...` |
| Anthropic | `sk-ant-` | `sk-ant-api03-...` |
| Hugging Face | `hf_` | `hf_AbCdEfGh...` |
| Groq | `gsk_` | `gsk_...` |
| Perplexity | `pplx-` | `pplx-...` |
| Replicate | `r8_` | `r8_...` |
| xAI | `xai-` | `xai-...` |
| OpenRouter | `sk-or-` | `sk-or-v1-...` |

### 2.3 Shadow AI Detection Methods

1. **DNS Monitoring**: Watch egress DNS queries for AI API domains listed above. `api.openai.com` rarely blends into normal traffic. Lesser-known like `generativelanguage.googleapis.com` may be under generic "cloud" labels.

2. **TLS SNI Inspection**: Monitor Server Name Indication in TLS handshakes for AI domain patterns without needing to decrypt traffic.

3. **DLP/Proxy Rules**: Inspect decrypted HTTPS traffic for:
   - `/v1/chat/completions` path pattern
   - `Authorization: Bearer sk-` prefixed tokens
   - `x-api-key` + `anthropic-version` header combination
   - Request bodies containing `"model":` and `"messages":` JSON fields

4. **Network Egress Firewall**: Block or alert on connections to known AI API domains.

5. **Browser Extension Detection**: AI browser extensions (ChatGPT helpers, Copilot, etc.) intercept `fetch()` and `XMLHttpRequest`, look for `Authorization` headers, and may exfiltrate session tokens. Over 1,550 GenAI SaaS apps tracked by Netskope.

---

## 3. AI Service Fingerprinting Techniques

### 3.1 HTTP Response Headers

| Header | Value | Indicates |
|--------|-------|-----------|
| `Server: uvicorn` | Present | FastAPI/Uvicorn backend (vLLM, Ollama proxied, FastChat, LangServe, many others) |
| `Server: hypercorn` | Present | ASGI server, possibly AI service |
| `content-type: text/event-stream` | Present | SSE streaming (LLM streaming responses) |
| `transfer-encoding: chunked` | With SSE | Streaming LLM inference response |
| `x-request-id` | UUID | Common in vLLM, OpenAI-compatible servers |
| `openai-organization` | Org ID | Proxied OpenAI API or compatible server |
| `openai-processing-ms` | Number | OpenAI API or compatible server |
| `openai-version` | `2020-10-01` | OpenAI API |
| `anthropic-organization-id` | Org ID | Anthropic API |
| `access-control-allow-origin: *` | Present | Wide-open CORS, common in KoboldCpp, many local AI tools |

### 3.2 OpenAI-Compatible API Detection

Most self-hosted LLM servers implement the OpenAI-compatible API. Universal detection approach:

```
Step 1: GET /v1/models
  - 200 OK with JSON {"data": [...], "object": "list"} = OpenAI-compatible server
  - Response contains model IDs specific to the service

Step 2: GET /v1/chat/completions (OPTIONS)
  - Check CORS headers for permissive access

Step 3: Check root endpoint
  - "Ollama is running" = Ollama
  - HTML with Gradio = oobabooga/SD WebUI/etc.
  - JSON API docs = FastAPI-based service
  - Swagger UI redirect = many AI frameworks
```

### 3.3 Streaming Response Patterns (SSE Detection)

LLM streaming responses use Server-Sent Events (SSE):
```
data: {"id":"chatcmpl-...","object":"chat.completion.chunk","created":...,"model":"...","choices":[{"index":0,"delta":{"role":"assistant","content":""},"finish_reason":null}]}

data: {"id":"chatcmpl-...","object":"chat.completion.chunk","created":...,"model":"...","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

data: [DONE]
```

Detection signatures:
- Content-Type: `text/event-stream`
- Lines prefixed with `data: `
- JSON with `"object":"chat.completion.chunk"`
- Stream termination with `data: [DONE]`

### 3.4 Framework-Specific Detection Paths

| Path | Framework/Service |
|------|-------------------|
| `/docs` or `/redoc` | FastAPI-based (vLLM, LangServe, FastChat, TGI, LocalAI) |
| `/sdapi/v1/` | AUTOMATIC1111 Stable Diffusion WebUI |
| `/api/tags` | Ollama |
| `/api/v1/generate` | KoboldAI/KoboldCpp |
| `/generate` + `/generate_stream` | Hugging Face TGI |
| `/predictions/{model}` | TorchServe |
| `/v1/models/{name}:predict` | TensorFlow Serving |
| `/v2/health/ready` | NVIDIA Triton / KServe V2 protocol |
| `/v2/models/{name}/infer` | NVIDIA Triton / KServe V2 protocol |
| `/api/2.0/mlflow/` | MLflow |
| `/{chain}/invoke` | LangServe |
| `/{chain}/playground/` | LangServe |
| `/prompt` | ComfyUI |
| `/system_stats` | ComfyUI |
| `/object_info` | ComfyUI |
| `/slots` | llama.cpp server |
| `/props` | llama.cpp server |
| `/info` | Hugging Face TGI |
| `/version` | MLflow |
| `/api/version` | Ollama |

### 3.5 Service-Specific Root Responses

| Root Response | Service |
|---------------|---------|
| `"Ollama is running"` (plain text) | Ollama |
| Gradio HTML (contains `gradio` in JS/CSS) | oobabooga, SD WebUI, FastChat Web |
| Swagger/OpenAPI JSON redirect | FastAPI-based services |
| ComfyUI interface HTML | ComfyUI |
| React/Vue SPA | Open WebUI, AnythingLLM, LibreChat |

---

## 4. GPU/Infrastructure Indicators

### 4.1 NVIDIA DCGM Exporter (GPU Metrics)

| Property | Value |
|----------|-------|
| **Default Port** | 9400 |
| **Metrics Endpoint** | `GET /metrics` (Prometheus format) |
| **Key Metrics** | `DCGM_FI_DEV_SM_CLOCK`, `DCGM_FI_DEV_MEM_CLOCK`, `DCGM_FI_DEV_MEMORY_TEMP`, `DCGM_FI_DEV_GPU_UTIL`, `DCGM_FI_DEV_MEM_COPY_UTIL`, `DCGM_FI_DEV_POWER_USAGE` |
| **Detection** | Port 9400 with Prometheus metrics containing `DCGM_FI_` prefix is definitive NVIDIA GPU monitoring. Labels include GPU UUID, index, model name. |
| **Docker Image** | `nvcr.io/nvidia/k8s/dcgm-exporter` |

### 4.2 NVIDIA GPU Operator (Kubernetes)

| Component | Detection |
|-----------|-----------|
| **Device Plugin** | DaemonSet `nvidia-device-plugin-daemonset`. Registers `nvidia.com/gpu` resource. |
| **DCGM Exporter** | DaemonSet exporting GPU metrics to Prometheus on port 9400. |
| **Driver Container** | Contains `nvidia-smi` tool. Pod name pattern: `nvidia-driver-*`. |
| **Node Feature Discovery** | Labels nodes with `feature.node.kubernetes.io/pci-10de.present=true` (NVIDIA PCI vendor ID). |
| **GPU Resource** | Node has `nvidia.com/gpu` in allocatable resources. |

### 4.3 NVIDIA Triton Metrics (Port 8002)

Prometheus metrics on port 8002 include:
- `nv_inference_request_success` - successful inference count
- `nv_inference_request_failure` - failed inference count
- `nv_inference_compute_infer_duration_us` - inference compute time
- `nv_gpu_utilization` - GPU utilization
- `nv_gpu_memory_total_bytes` - total GPU memory
- `nv_gpu_memory_used_bytes` - used GPU memory

### 4.4 Other GPU Monitoring Endpoints

| Service | Port | Endpoint | Detection |
|---------|------|----------|-----------|
| **Prometheus Node Exporter** (with GPU) | 9100 | `/metrics` | Contains `nvidia_*` metrics if GPU plugin installed |
| **NVIDIA Management Library (NVML)** | N/A | Accessed via API, not network | Library used by nvidia-smi, DCGM |
| **AMD ROCm SMI** | N/A | Command-line, not network-exposed | `rocm-smi` for AMD GPUs |

---

## 5. Container-Based AI Detection

### 5.1 Docker API Exposure

| Property | Value |
|----------|-------|
| **Docker API Ports** | 2375 (unencrypted), 2376 (TLS) |
| **Container List** | `GET /containers/json` returns all running containers |
| **Container Inspect** | `GET /containers/{id}/json` returns full config including image name, labels, env vars |
| **Image List** | `GET /images/json` returns all pulled images |

### 5.2 Known AI Docker Image Names/Patterns

| Image Pattern | Service |
|---------------|---------|
| `ollama/ollama` | Ollama |
| `localai/localai` | LocalAI |
| `vllm/vllm-openai` | vLLM |
| `ghcr.io/huggingface/text-generation-inference` | Hugging Face TGI |
| `nvcr.io/nvidia/tritonserver` | NVIDIA Triton |
| `pytorch/torchserve` | TorchServe |
| `tensorflow/serving` | TensorFlow Serving |
| `tabbyml/tabby` | Tabby Coding Assistant |
| `koboldai/koboldcpp` | KoboldCpp |
| `ghcr.io/open-webui/open-webui` | Open WebUI |
| `docker.io/semitechnologies/weaviate` | Weaviate Vector DB |
| `chromadb/chroma` | ChromaDB Vector DB |
| `qdrant/qdrant` | Qdrant Vector DB |
| `milvusdb/milvus` | Milvus Vector DB |
| `quay.io/coreos/etcd` (in AI contexts) | ML pipeline metadata store |
| `bentoml/*` | BentoML services |
| `langchain/*` | LangChain-based apps |
| `*stable-diffusion*` | Stable Diffusion variants |
| `*comfyui*` | ComfyUI |
| `*whisper*` | Whisper speech-to-text |
| `*llama*` | Llama-based model servers |
| `*mistral*` | Mistral model containers |
| `deepseek-ai/*` | DeepSeek model containers |

### 5.3 Docker Label/Environment Variable Patterns

Look for these environment variables in container inspection:
- `OLLAMA_HOST`, `OLLAMA_MODELS`
- `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`
- `CUDA_VISIBLE_DEVICES`
- `NVIDIA_VISIBLE_DEVICES`
- `HF_TOKEN`, `HUGGING_FACE_HUB_TOKEN`
- `MODEL_ID`, `MODEL_NAME`
- `TRANSFORMERS_CACHE`

Look for these Docker labels:
- `com.nvidia.volumes.needed`
- Runtime: `nvidia` in HostConfig

### 5.4 Docker Compose Patterns

AI workloads often use `docker-compose.yml` with:
- `runtime: nvidia` or `deploy.resources.reservations.devices` with `nvidia` capabilities
- Port mappings matching known AI service ports (11434, 8080, 7860, 8000, etc.)
- Volume mounts to `/models`, `/data`, `/.cache/huggingface`

---

## 6. Self-Hosted AI Chat/Platform UIs

| Service | Default Port | Key Endpoints | Detection |
|---------|-------------|---------------|-----------|
| **Open WebUI** | 3000 or 8080 | `/api/`, Web SPA at root | React SPA, connects to Ollama/OpenAI backends |
| **AnythingLLM** | 3001 | `/api/`, Web SPA at root | Full-stack AI chat application |
| **LibreChat** | 3080 | `/api/`, Web SPA at root | Multi-provider chat interface |
| **Flowise** | 3000 | `/api/v1/`, `/canvas` | Visual AI workflow builder, Swagger at `/api-docs` |
| **Dify** | 3000 (web), 5001 (API) | `/v1/`, `/console/api/` | AI application development platform |
| **LiteLLM Proxy** | 4000 | `/v1/` (OpenAI-compatible), `/ui` (admin) | Multi-provider proxy/gateway |
| **PrivateGPT** | 8001 | `/v1/` | Private document Q&A |
| **Chatbot UI** | 3000 | Web SPA at root | ChatGPT-style interface |

---

## 7. Kubernetes AI Inference Platforms

### 7.1 KServe (Open Inference Protocol)

| Property | Value |
|----------|-------|
| **V1 Protocol** | `/v1/models/{model}:predict`, `/v1/models/{model}:explain` |
| **V2 Protocol** | `/v2/health/ready`, `/v2/health/live`, `/v2/models/{model}/infer`, `/v2/models/{model}/ready`, `/v2/repository/index` |
| **Default Ports** | 8080 (HTTP), 9000 (gRPC) |
| **Detection** | V2 protocol paths (`/v2/health/ready`, `/v2/models/`) are part of the Open Inference Protocol standard. |

### 7.2 Seldon Core

| Property | Value |
|----------|-------|
| **Protocol** | Open Inference Protocol (V2), also supports V1 |
| **Default Ports** | 8080 (HTTP), 9000 (gRPC) via MLServer |
| **Detection** | Seldon-specific annotations on Kubernetes services. `seldon-*` pod name patterns. |

### 7.3 Kubeflow

| Property | Value |
|----------|-------|
| **Pipeline UI Port** | 3000 (proxied through Istio at 8080) |
| **Ingress Port** | 31380 (NodePort) |
| **Detection** | Istio gateway with Kubeflow-specific virtual services. Pipeline API at `/api/v1beta1/`. |

---

## Quick Reference: Port Scan Priority List

These are the most important ports to check when scanning for AI services:

| Port | Primary Service(s) | Confidence |
|------|-------------------|------------|
| **11434** | Ollama | HIGH - almost exclusively Ollama |
| **7860** | Gradio apps (SD WebUI, oobabooga, FastChat) | HIGH - Gradio default |
| **8080** | LocalAI, TorchServe, llama.cpp, TGI, KServe, Tabby | MEDIUM - very common general port |
| **8000** | vLLM, NVIDIA Triton (HTTP), FastChat API, LangServe, Ray Serve | MEDIUM - common general port |
| **8188** | ComfyUI | HIGH - distinctive for ComfyUI |
| **1234** | LM Studio | HIGH - fairly distinctive |
| **1337** | Jan.ai | HIGH - distinctive |
| **4891** | GPT4All | HIGH - unique to GPT4All |
| **5001** | KoboldAI/KoboldCpp | HIGH - fairly distinctive |
| **5000** | MLflow, oobabooga API | MEDIUM |
| **3000** | BentoML, Open WebUI, Flowise, Dify, Kubeflow UI | MEDIUM - common web port |
| **3001** | AnythingLLM | MEDIUM |
| **3080** | LibreChat | HIGH - fairly distinctive |
| **4000** | LiteLLM Proxy | MEDIUM |
| **8001** | NVIDIA Triton (gRPC), PrivateGPT | MEDIUM |
| **8002** | NVIDIA Triton (Metrics) | MEDIUM |
| **8081** | TorchServe (Management) | MEDIUM |
| **8082** | TorchServe (Metrics) | MEDIUM |
| **8265** | Ray Dashboard/Serve API | HIGH - Ray specific |
| **8500** | TensorFlow Serving (gRPC) | MEDIUM |
| **8501** | TensorFlow Serving (REST) | MEDIUM |
| **9400** | NVIDIA DCGM Exporter | HIGH - GPU monitoring specific |
| **21001** | FastChat Controller | HIGH - unusual port, distinctive |
| **21002** | FastChat Model Worker | HIGH - unusual port, distinctive |
| **47334** | MindsDB REST API | HIGH - very distinctive |
| **47335** | MindsDB MySQL | HIGH - very distinctive |
| **2375** | Docker API (unencrypted) | HIGH - Docker specific |
| **2376** | Docker API (TLS) | HIGH - Docker specific |

---

## Detection Algorithm Recommendations

### Phase 1: Port Discovery
Scan target for all ports in the priority list above.

### Phase 2: Banner Grabbing / Service Identification
For each open port, send HTTP GET to root (`/`) and check:
1. Plain text "Ollama is running" --> Ollama confirmed
2. HTML containing "gradio" --> Gradio-based AI app (SD, oobabooga, etc.)
3. JSON or redirect to `/docs` --> FastAPI-based (check further)
4. Prometheus metrics format with `DCGM_FI_` --> NVIDIA GPU monitoring
5. Prometheus metrics with `nv_inference_` --> NVIDIA Triton metrics

### Phase 3: Endpoint Probing
For services that passed Phase 2, probe distinctive endpoints:
- `/v1/models` --> OpenAI-compatible API (then check model IDs for service type)
- `/api/tags` --> Ollama confirmed
- `/api/version` --> Ollama version
- `/sdapi/v1/txt2img` --> AUTOMATIC1111
- `/system_stats` --> ComfyUI
- `/health` + `/info` --> TGI
- `/v2/health/ready` --> Triton or KServe V2
- `/ping` (port 8080) --> TorchServe
- `/models` (port 8081) --> TorchServe Management
- `/api/2.0/mlflow/` --> MLflow
- `/version` --> MLflow
- `/slots` --> llama.cpp
- `/api/v1/info/version` --> KoboldCpp

### Phase 4: Response Analysis
Examine responses for:
- `Server:` header (uvicorn, hypercorn, etc.)
- Model IDs in `/v1/models` response (identifies loaded model types)
- Version strings in service-specific version endpoints
- CORS headers (`access-control-allow-origin: *` common in local AI tools)
