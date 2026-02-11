# Agrus Scanner

Network reconnaissance tool with deep AI/ML service detection. Scans your network to discover hosts, open ports, and identifies AI services running across your infrastructure.

Built for security teams, IT admins, and researchers who need visibility into shadow AI, rogue LLM deployments, and GPU infrastructure on their networks.

## Features

- **Ping Sweep** - Fast ICMP discovery across subnets (256 concurrent)
- **Port Scanning** - TCP connect scan with preset profiles (Quick, Common, Extended, AI)
- **AI Service Detection** - 45 probe definitions identifying 25+ AI/ML services
- **Docker Container Enumeration** - Detects AI containers via exposed Docker API
- **GPU Infrastructure** - Finds NVIDIA DCGM exporters and inference metrics
- **Real-time Results** - Live-updating UI as scan progresses

## AI Detection Categories

| Category | Services Detected |
|----------|-------------------|
| **LLM** | Ollama, vLLM, HF TGI, llama.cpp, KoboldCpp, LM Studio, LiteLLM, Jan.ai, GPT4All, LocalAI, FastChat, Tabby |
| **Image Gen** | Stable Diffusion (A1111), ComfyUI |
| **ML Platform** | NVIDIA Triton, TorchServe, TensorFlow Serving, MLflow, Ray Serve, BentoML, KServe, MindsDB |
| **AI Platform** | Open WebUI, AnythingLLM, LibreChat, Flowise, Dify, Gradio apps |
| **GPU Infra** | NVIDIA DCGM Exporter, Triton Metrics, TorchServe Metrics |
| **Container** | Docker API with 33 AI image pattern matches |

Detection goes beyond port scanning - the prober queries service-specific API endpoints, extracts model names, versions, GPU info, and container details.

## Requirements

- Windows 10/11
- .NET 9 SDK (for building)

## Build & Run

```powershell
dotnet run --project AgrusScanner
```

## Build MSI Installer

Creates a self-contained installer (no .NET runtime required on target machine):

```powershell
# One-time: install WiX CLI
dotnet tool install --global wix

# Build installer
.\build-installer.ps1
# Output: Installer\bin\Release\AgrusScanner-Setup.msi
```

## Usage

1. Enter an IP range (CIDR, range, or single IP)
2. Select a scan preset:
   - **Quick** - 6 common ports
   - **Common** - 22 well-known ports
   - **Extended** - 58 service ports
   - **AI Scan** - 28 AI/ML-specific ports with service probing
   - **No port scan** - Ping sweep only
3. Click **START**

AI Scan results show detected services with extracted details:
```
[LLM] Ollama :11434 (llama3, mistral) | [GPU Infra] NVIDIA DCGM :9400 (RTX 4090)
```

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Ctrl + = | Zoom in |
| Ctrl + - | Zoom out |
| Ctrl + 0 | Reset zoom |
| Ctrl + Scroll | Zoom |

## License

All rights reserved.
