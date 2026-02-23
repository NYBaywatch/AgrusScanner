# Agrus Scanner - User Guide

> Discover hosts, open ports, and AI/ML services on your network.
>
> **GitHub:** [github.com/NYBaywatch/AgrusScanner](https://github.com/NYBaywatch/AgrusScanner)

## Table of Contents

- [What is Agrus Scanner?](#what-is-agrus-scanner)
- [Getting Started](#getting-started)
- [How Scanning Works](#how-scanning-works)
- [Scan Presets & Ports](#scan-presets--ports)
- [AI/ML Service Detection](#aiml-service-detection)
- [Reading Your Results](#reading-your-results)
- [Exporting Results](#exporting-results)
- [Settings](#settings)
- [MCP Server (Agent Mode)](#mcp-server-agent-mode)
- [Keyboard Shortcuts](#keyboard-shortcuts)

---

## What is Agrus Scanner?

Agrus Scanner is a Windows desktop application that scans your local network to find:

- **Live hosts** via ICMP ping sweep
- **Open ports** via TCP connect scanning
- **Hostnames** via reverse DNS lookup
- **AI/ML services** via HTTP endpoint probing (45+ detection signatures)

It was built to help discover "shadow AI" - unauthorized AI services running on corporate or home networks - but works as a general-purpose network scanner too.

## Getting Started

1. **Launch** AgrusScanner.exe
2. **Enter an IP range** in the input field:
   - CIDR notation: `192.168.1.0/24` (scans 254 hosts)
   - Range notation: `192.168.1.1-254` (scans from .1 to .254)
   - Single IP: `192.168.1.100`
3. **Choose a scan preset** (Quick, Common, Extended, AI, Deep AI, or No port scan)
4. **Click Scan**

The auto-detect button will fill in your local subnet automatically.

## How Scanning Works

Each scan runs through up to 5 phases:

### 1. Ping Sweep
Sends ICMP ping packets to every IP in the range simultaneously (up to 256 at once). Hosts that respond are marked "Alive" with their round-trip time in milliseconds. Hosts that don't respond within 1 second are marked "Dead" and skipped (unless "Skip Ping" is enabled).

### 2. DNS Resolution
For every alive host, the scanner performs a reverse DNS lookup to find its hostname (e.g., `myserver.local`). This runs in parallel with port scanning.

### 3. Port Scanning
For each alive host, the scanner attempts TCP connections to every port in the selected preset (up to 64 ports at once per host). If a connection succeeds within 2 seconds, the port is "open." Each open port is labeled with its well-known service name (e.g., port 80 = "http").

### 4. AI Service Probing (AI and Deep AI presets)
When using the **AI** or **Deep AI** scan preset, the scanner sends HTTP requests to specific endpoints on each open port. It matches responses against 45 detection signatures to identify AI/ML services. The scanner extracts details like model names, versions, and GPU information where available.

In **Deep AI Scan** mode, the scanner ignores port hints — every probe runs against every open port. This catches AI services running on non-standard ports at the cost of longer scan times.

### 5. Docker Container Detection (AI and Deep AI presets)
If the Docker API is accessible (port 2375), the scanner queries running containers and flags any with AI-related images (Ollama, vLLM, Stable Diffusion, etc.).

## Scan Presets & Ports

### Quick (6 ports)
The fastest scan - covers the most common services.

| Port | Service |
|------|---------|
| 21 | FTP |
| 22 | SSH |
| 80 | HTTP |
| 443 | HTTPS |
| 3389 | RDP (Remote Desktop) |
| 8080 | HTTP Alt |

### Common (22 ports)
Broader coverage including mail, databases, and management interfaces.

| Port | Service | Port | Service |
|------|---------|------|---------|
| 20 | FTP Data | 443 | HTTPS |
| 21 | FTP | 445 | SMB |
| 22 | SSH | 993 | IMAPS |
| 23 | Telnet | 995 | POP3S |
| 25 | SMTP | 1723 | PPTP VPN |
| 53 | DNS | 3306 | MySQL |
| 80 | HTTP | 3389 | RDP |
| 110 | POP3 | 5900 | VNC |
| 111 | RPCBind | 8080 | HTTP Alt |
| 135 | MS-RPC | 8443 | HTTPS Alt |
| 139 | NetBIOS | | |
| 143 | IMAP | | |

### Extended (58 ports)
Comprehensive scan covering nearly everything - DHCP, SNMP, LDAP, databases, NoSQL, and more.

| Port | Service | Port | Service | Port | Service |
|------|---------|------|---------|------|---------|
| 20 | FTP Data | 179 | BGP | 1521 | Oracle |
| 21 | FTP | 389 | LDAP | 1723 | PPTP |
| 22 | SSH | 443 | HTTPS | 2049 | NFS |
| 23 | Telnet | 445 | SMB | 2082 | cPanel |
| 25 | SMTP | 465 | SMTPS | 2083 | cPanel SSL |
| 53 | DNS | 500 | ISAKMP | 2086 | WHM |
| 67 | DHCP | 514 | Syslog | 2087 | WHM SSL |
| 68 | DHCP | 515 | Printer | 3306 | MySQL |
| 69 | TFTP | 520 | RIP | 3389 | RDP |
| 80 | HTTP | 587 | Mail Submit | 5432 | PostgreSQL |
| 110 | POP3 | 631 | IPP/CUPS | 5900 | VNC |
| 111 | RPCBind | 636 | LDAPS | 5901 | VNC :1 |
| 119 | NNTP | 993 | IMAPS | 6379 | Redis |
| 123 | NTP | 995 | POP3S | 8080 | HTTP Alt |
| 135 | MS-RPC | 1080 | SOCKS | 8443 | HTTPS Alt |
| 137 | NetBIOS NS | 1433 | MSSQL | 8888 | HTTP Alt 2 |
| 138 | NetBIOS DGM | 1434 | MSSQL Monitor | 9090 | Admin |
| 139 | NetBIOS SSN | | | 9200 | Elasticsearch |
| 143 | IMAP | | | 27017 | MongoDB |
| 161 | SNMP | | | | |
| 162 | SNMP Trap | | | | |

### AI (28 ports)
Specialized for discovering AI/ML services. Includes all LLM, image generation, ML platform, and GPU infrastructure ports, plus HTTP probing to identify the exact service. Probes are filtered by port hint — each probe only runs on ports where its service is expected.

| Port | Target Service(s) | Category |
|------|--------------------|----------|
| 11434 | Ollama | LLM |
| 8000 | vLLM, NVIDIA Triton | LLM / ML Platform |
| 8080 | HF TGI, llama.cpp, TorchServe, KServe, Tabby | LLM / ML Platform |
| 1234 | LM Studio | LLM |
| 1337 | Jan.ai | LLM |
| 4891 | GPT4All | LLM |
| 5001 | KoboldCpp | LLM |
| 3000 | BentoML, Open WebUI, Flowise | ML Platform / AI Platform |
| 4000 | LiteLLM | LLM |
| 7860 | Stable Diffusion (A1111) | Image Generation |
| 8188 | ComfyUI | Image Generation |
| 8081 | TorchServe Management | ML Platform |
| 8082 | TorchServe Metrics | GPU Infrastructure |
| 8265 | Ray Serve | ML Platform |
| 8500 | (reserved) | ML Platform |
| 8501 | TensorFlow Serving | ML Platform |
| 47334 | MindsDB | ML Platform |
| 47335 | MindsDB (alt) | ML Platform |
| 3001 | AnythingLLM | AI Platform |
| 3080 | LibreChat | AI Platform |
| 5000 | MLflow, Dify | ML Platform / AI Platform |
| 8002 | Triton Metrics | GPU Infrastructure |
| 9400 | NVIDIA DCGM Exporter | GPU Infrastructure |
| 2375 | Docker API (container scan) | Container Detection |
| 8443 | HTTPS Alt | Multi-use |
| 21001 | FastChat Controller | LLM |
| 21002 | FastChat Worker | LLM |

### Deep AI Scan (all 65535 ports)
The most thorough scan mode. Scans every port from 1 to 65535, then runs all AI probes against every open port — regardless of port hints. This catches AI services running on non-standard ports that the regular AI preset would miss.

**Trade-offs:**
- Much slower than the regular AI preset (scanning all ports takes time)
- Best used on small ranges or single IPs
- No port customization (extra/removed ports) — it already scans everything
- All 59 probe definitions run against every open port (ignores PortHint filtering)

## AI/ML Service Detection

When using the AI or Deep AI preset, the scanner identifies services by sending HTTP requests to known API endpoints and matching the response. Here is every service the scanner can detect:

### LLM Services (Large Language Models)

| Service | Default Port | How It's Detected |
|---------|-------------|-------------------|
| [Ollama](https://ollama.ai) | 11434 | Root returns "Ollama is running"; `/api/tags` lists models |
| [vLLM](https://github.com/vllm-project/vllm) | 8000 | `/version` returns version info |
| [Hugging Face TGI](https://github.com/huggingface/text-generation-inference) | 8080 | `/info` contains `model_id` |
| [llama.cpp](https://github.com/ggerganov/llama.cpp) | 8080 | `/props` has `default_generation_settings` |
| [KoboldCpp](https://github.com/LostRuins/koboldcpp) | 5001 | `/api/v1/info/version` and `/api/v1/model` |
| [LM Studio](https://lmstudio.ai) | 1234 | `/api/v0/models` (unique v0 API path) |
| [LiteLLM](https://github.com/BerriAI/litellm) | 4000 | `/model/info` and `/health/liveliness` |
| [Jan.ai](https://jan.ai) | 1337 | `/v1/models` on port 1337 |
| [GPT4All](https://gpt4all.io) | 4891 | `/v1/models` on port 4891 |
| [LocalAI](https://github.com/mudler/LocalAI) | 8080 | `/readyz` health endpoint |
| [FastChat](https://github.com/lm-sys/FastChat) | 21001/21002 | Controller on 21001, worker on 21002 |
| [Tabby](https://github.com/TabbyML/tabby) | 8080 | `/v1/health` returns model info |

### Image Generation

| Service | Default Port | How It's Detected |
|---------|-------------|-------------------|
| [Stable Diffusion (A1111)](https://github.com/AUTOMATIC1111/stable-diffusion-webui) | 7860 | `/sdapi/v1/sd-models` and `/sdapi/v1/options` |
| [ComfyUI](https://github.com/comfyanonymous/ComfyUI) | 8188 | `/system_stats` and `/object_info` |

### ML Platforms

| Service | Default Port | How It's Detected |
|---------|-------------|-------------------|
| [NVIDIA Triton](https://developer.nvidia.com/triton-inference-server) | 8000 | `/v2/health/ready` (V2 inference protocol) |
| [TorchServe](https://pytorch.org/serve/) | 8080/8081 | `/ping` returns "Healthy"; `/models` on 8081 |
| [TensorFlow Serving](https://www.tensorflow.org/tfx/guide/serving) | 8501 | `/v1/models` with `model_version_status` |
| [MLflow](https://mlflow.org) | 5000 | `/version` and `/api/2.0/mlflow/experiments/search` |
| [Ray Serve](https://docs.ray.io/en/latest/serve/) | 8265 | `/api/serve/deployments/` |
| [BentoML](https://www.bentoml.com) | 3000 | `/readyz` on port 3000 |
| [KServe](https://kserve.github.io/website/) | 8080 | `/v2/health/ready` |
| [MindsDB](https://mindsdb.com) | 47334 | Root page on port 47334 |

### AI Chat Platforms

| Service | Default Port | How It's Detected |
|---------|-------------|-------------------|
| [Open WebUI](https://github.com/open-webui/open-webui) | 3000 | HTML contains "Open WebUI" |
| [AnythingLLM](https://github.com/Mintplex-Labs/anything-llm) | 3001 | `/api/health` returns `{online: true}` |
| [LibreChat](https://github.com/danny-avila/LibreChat) | 3080 | HTML contains "LibreChat" |
| [Flowise](https://github.com/FlowiseAI/Flowise) | 3000 | `/api-docs` (Swagger UI) |
| [Dify](https://github.com/langgenius/dify) | 5000 | `/console/api/setup` |

### GPU Infrastructure

| Service | Default Port | How It's Detected |
|---------|-------------|-------------------|
| [NVIDIA DCGM Exporter](https://github.com/NVIDIA/dcgm-exporter) | 9400 | `/metrics` with `DCGM_FI_` prefix (extracts GPU model names) |
| Triton Metrics | 8002 | `/metrics` with `nv_inference_` prefix |
| TorchServe Metrics | 8082 | `/metrics` with `ts_inference_` prefix |

### Container Detection

| Service | Default Port | How It's Detected |
|---------|-------------|-------------------|
| Docker API | 2375 | `/containers/json` - filters running containers by 33 AI image patterns |

**Docker AI image patterns matched:** ollama, localai, vllm, text-generation-inference, tritonserver, torchserve, tensorflow/serving, stable-diffusion, comfyui, open-webui, anythingllm, librechat, flowise, dify, litellm, koboldcpp, tabbyml, whisper, llama, mistral, deepseek, qdrant, chromadb, weaviate, milvus, bentoml, langchain, langserve, ray, mlflow, mindsdb, privategpt, gpt4all

### Generic Fallbacks

If no specific service is matched, the scanner tries these lower-confidence detections:

| Detection | Endpoint | Confidence |
|-----------|----------|------------|
| OpenAI-compatible API | `/v1/models` | Medium |
| LM Studio / Text-Gen-WebUI | `/api/v1/models` | Medium |
| Gradio AI App | Root page contains "gradio" | Medium |
| Generic LLM Service | `/health` returns 200 | Low |

## Reading Your Results

The scan results table shows one row per discovered host:

| Column | Description |
|--------|-------------|
| **IP Address** | The host's IP |
| **Hostname** | Reverse DNS name (if available) |
| **Ping** | Round-trip time in ms |
| **Status** | Alive or Dead |
| **AI Service** | Detected AI/ML services with details (AI preset only) |
| **Ports** | All open ports with service names |

**AI Service column format:**
```
[Category] ServiceName :port (details)
```

Examples:
- `[LLM] Ollama :11434 (llama3, mistral)`
- `[Image Gen] ComfyUI :8188 (NVIDIA GeForce RTX 4090, 24.0GB VRAM)`
- `[GPU Infra] NVIDIA DCGM :9400 (RTX 4090)`
- `[Container] Docker API :2375 (ollama (ollama/ollama:latest) [running])`

## Exporting Results

After a scan completes, the **EXPORT** button appears in the toolbar (between ALIVE ONLY and START). Click it to save results to a file.

| Format | Extension | Description |
|--------|-----------|-------------|
| **CSV** | `.csv` | Comma-separated values. Opens in Excel, Google Sheets, etc. |
| **TXT** | `.txt` | Tab-delimited text. Easy to paste into other tools. |

Both formats include the same columns: IP address, hostname, alive status, ping time, open ports, and AI services.

The Save File dialog lets you pick the format and location. The file format is determined by the extension you choose.

## Settings

Access settings from the gear icon in the toolbar.

| Setting | Description | Default |
|---------|-------------|---------|
| **Skip Ping** | Scan all IPs even if they don't respond to ping. Useful when ICMP is blocked by firewalls. | Off |
| **MCP Server Port** | TCP port for the MCP server (agent integration) | 8999 |
| **Extra Ports** | Additional ports to add to any preset (comma-separated) | Empty |
| **Removed Ports** | Ports to exclude from a preset (comma-separated, not available for AI preset) | Empty |

Settings are saved automatically to `%LOCALAPPDATA%\AgrusScanner\settings.json`.

## MCP Server (Agent Mode)

Agrus Scanner includes a built-in [MCP](https://modelcontextprotocol.io) server that lets AI assistants (Claude Code, OpenClaw, etc.) run scans programmatically.

### Headless Mode

Run without the GUI, just the MCP server:
```
AgrusScanner.exe --mcp-only
```
A system tray icon appears with options to exit. The MCP server listens on the configured port (default 8999).

### Available Tools

| Tool | Description |
|------|-------------|
| `scan_network` | Full network scan with ping, port scan, DNS, and optional AI probing |
| `probe_host` | Deep-scan a single IP for open ports and AI services |
| `export_results` | Export last scan results to JSON or CSV file |
| `list_presets` | List all scan presets with their port numbers |

### Connecting Claude Code

The project includes a [`.mcp.json`](https://github.com/NYBaywatch/AgrusScanner/blob/master/.mcp.json) configuration that connects automatically when working in the project directory.

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl + = | Zoom in |
| Ctrl + - | Zoom out |
| Ctrl + 0 | Reset zoom |
| Ctrl + Mouse Wheel | Zoom in/out |

---

**License:** MIT - Copyright 2026 Joseph Fago

**Source:** [github.com/NYBaywatch/AgrusScanner](https://github.com/NYBaywatch/AgrusScanner)
