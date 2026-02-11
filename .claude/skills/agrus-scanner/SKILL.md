---
name: agrus-scanner
description: Network reconnaissance and AI/ML service detection. Scan IP ranges with ping sweeps, port scanning, DNS resolution, and AI service probing across 45 detection signatures. Use when the user wants to discover hosts, open ports, or AI/ML services on a network.
metadata:
  author: agrus
  version: "0.1.0"
compatibility: Requires the Agrus Scanner MCP server running (AgrusScanner.exe --mcp-only). Windows only.
allowed-tools: mcp__agrus-scanner__scan_network mcp__agrus-scanner__probe_host mcp__agrus-scanner__list_presets
---

# Agrus Scanner

Network reconnaissance tool that discovers hosts, open ports, and AI/ML services on local networks.

## Prerequisites

The Agrus Scanner MCP server must be running before using these tools:

```
AgrusScanner.exe --mcp-only
```

This starts a tray icon and MCP server on the configured port (default 8999).

## Available Tools

### scan_network

Full network scan: ping sweep, port scan, DNS resolution, optional AI service detection.

Parameters:
- `ip_range` (required) — CIDR notation like `192.168.1.0/24` or range like `10.0.0.1-254`
- `preset` — Port preset: `quick` (6 ports), `common` (22 ports), `extended` (58 ports), `ai` (28 AI/ML ports), `none` (ping only). Default: `quick`
- `skip_ping` — Scan all IPs regardless of ping response. Default: `false`

Returns JSON array of host objects.

### probe_host

Deep-scan a single IP with port scanning and AI service detection.

Parameters:
- `ip` (required) — Single IP address like `192.168.1.100`
- `ports` — Comma-separated ports (`80,443,8080`) or preset name (`ai`, `quick`, etc). Default: `ai`

Returns JSON object with full host detail.

### list_presets

List all available scan presets with port counts and port numbers. Takes no parameters.

## Workflow

1. Start with `list_presets` to understand available scan options
2. Use `scan_network` with an IP range and appropriate preset for broad discovery
3. Use `probe_host` on interesting IPs for deeper investigation
4. The `ai` preset is best for finding AI/ML services (Ollama, vLLM, Stable Diffusion, ComfyUI, etc.)

## Example

To scan a local network for AI services:

```
scan_network(ip_range="192.168.1.0/24", preset="ai")
```

To deep-probe a specific host:

```
probe_host(ip="192.168.1.50", ports="ai")
```

## AI Services Detected

The scanner detects 45+ AI/ML services across these categories:
- **LLM**: Ollama, vLLM, HuggingFace TGI, llama.cpp, KoboldCpp, LM Studio, LiteLLM, Jan.ai, GPT4All, LocalAI, FastChat, Tabby
- **Image Gen**: Stable Diffusion (A1111), ComfyUI
- **ML Platforms**: NVIDIA Triton, TorchServe, TensorFlow Serving, MLflow, Ray Serve, BentoML, KServe, MindsDB
- **AI UIs**: Open WebUI, AnythingLLM, LibreChat, Flowise, Dify
- **GPU/Infra**: NVIDIA DCGM, Docker API with AI container detection
