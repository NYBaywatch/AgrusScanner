# Agrus Scanner - Developer Documentation

> Internal reference for contributors and maintainers of [Agrus Scanner](https://github.com/NYBaywatch/AgrusScanner).

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Project Structure](#project-structure)
- [Tech Stack](#tech-stack)
- [Scanning Pipeline](#scanning-pipeline)
- [Concurrency Model](#concurrency-model)
- [AI Service Probe System](#ai-service-probe-system)
- [MCP Server](#mcp-server)
- [Agent Integrations](#agent-integrations)
- [Models](#models)
- [Services](#services)
- [UI Layer](#ui-layer)
- [Settings & Persistence](#settings--persistence)
- [Build & Packaging](#build--packaging)
- [Extending the Scanner](#extending-the-scanner)

---

## Architecture Overview

Agrus Scanner follows the **MVVM** (Model-View-ViewModel) pattern built on WPF/.NET 9.0. The core scanning logic lives in stateless service classes, the UI binds to an `ObservableCollection` on the ViewModel, and an embedded ASP.NET Core server exposes MCP tools for agent integration.

```
┌─────────────────────────────────────────────────┐
│  MainWindow.xaml  (View)                        │
│    ↕ data binding                               │
│  MainViewModel.cs (ViewModel)                   │
│    ↓ orchestrates                               │
│  ┌────────────┬──────────────┬────────────────┐ │
│  │PingScanner │ PortScanner  │ AiServiceProber│ │
│  │DnsResolver │ IpRangeParser│ ServiceNameMap │ │
│  └────────────┴──────────────┴────────────────┘ │
│                  Services                        │
├─────────────────────────────────────────────────┤
│  McpHostManager + ScannerMcpTools               │
│  (ASP.NET Core embedded, /mcp endpoint)         │
└─────────────────────────────────────────────────┘
```

## Project Structure

```
Scanner/
├── AgrusScanner/                          # Main WPF application
│   ├── Models/
│   │   ├── ScanConfig.cs                  # Port presets & scan configuration
│   │   ├── HostResult.cs                  # Per-host result (INotifyPropertyChanged)
│   │   ├── PortResult.cs                  # Open port with service name
│   │   ├── AiServiceResult.cs             # AI service detection result
│   │   └── AppSettings.cs                 # User settings (MCP port, custom ports)
│   ├── Services/
│   │   ├── PingScanner.cs                 # ICMP ping sweep (256 concurrent)
│   │   ├── PortScanner.cs                 # TCP connect scan (64 concurrent/host)
│   │   ├── DnsResolver.cs                 # Reverse DNS (PTR) resolution
│   │   ├── IpRangeParser.cs               # CIDR/range/single IP parsing
│   │   ├── AiServiceProber.cs             # HTTP probing, 45 probe definitions
│   │   ├── ServiceNameMap.cs              # Port → service name lookup (70+ entries)
│   │   └── SettingsService.cs             # JSON persistence to %LOCALAPPDATA%
│   ├── ViewModels/
│   │   └── MainViewModel.cs               # UI state, commands, scan orchestration
│   ├── Mcp/
│   │   ├── ScannerMcpTools.cs             # MCP tool definitions (4 tools)
│   │   └── McpHostManager.cs              # ASP.NET Core MCP server host
│   ├── Converters/
│   │   ├── BoolToVisibilityConverter.cs   # WPF value converter
│   │   └── PortListConverter.cs           # Formats port list for display
│   ├── Themes/
│   │   └── DarkTheme.xaml                 # Dark theme resource dictionary
│   ├── App.xaml / App.xaml.cs             # App startup (--mcp-only mode detection)
│   ├── MainWindow.xaml / .xaml.cs         # Main UI layout + code-behind
│   └── TrayIcon.cs                        # System tray for MCP-only mode
├── Installer/                             # WiX MSI installer project
├── openclaw-plugin/                       # OpenClaw agent plugin (TypeScript)
│   ├── index.ts                           # Plugin entry, MCP proxy
│   ├── openclaw.plugin.json               # Plugin metadata
│   └── package.json                       # NPM package
├── .claude/skills/agrus-scanner/SKILL.md  # Claude Code skill definition
├── .mcp.json                              # Claude Code MCP server config
├── docs/                                  # Documentation & assets
├── AgrusScanner.sln                       # Visual Studio solution
├── build-installer.ps1                    # Build automation
└── LICENSE                                # MIT
```

**Key source files by link:**

| File | Purpose |
|------|---------|
| [ScanConfig.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Models/ScanConfig.cs) | Port presets (Quick/Common/Extended/AI/Deep AI) |
| [AiServiceProber.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/AiServiceProber.cs) | 45 AI probe definitions + detail extraction |
| [PingScanner.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/PingScanner.cs) | ICMP ping sweep |
| [PortScanner.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/PortScanner.cs) | TCP connect scan |
| [DnsResolver.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/DnsResolver.cs) | Reverse DNS resolution |
| [IpRangeParser.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/IpRangeParser.cs) | IP range parsing (CIDR/range/single) |
| [ServiceNameMap.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/ServiceNameMap.cs) | Port number → service name map |
| [MainViewModel.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/ViewModels/MainViewModel.cs) | Scan orchestration + UI state |
| [ScannerMcpTools.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Mcp/ScannerMcpTools.cs) | MCP tool definitions |
| [McpHostManager.cs](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Mcp/McpHostManager.cs) | MCP server lifecycle |

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 9.0 (self-contained, Windows) |
| UI | WPF (Windows Presentation Foundation) |
| MCP SDK | ModelContextProtocol v0.8.0-preview.1 |
| MCP Transport | ASP.NET Core (Streamable HTTP) |
| Networking | System.Net (Ping, TcpClient, Dns, HttpClient) |
| Serialization | System.Text.Json |
| Installer | WiX Toolset |
| System Tray | System.Windows.Forms (NotifyIcon) |

**No Electron, no embedded browser, no Node.js runtime.**

## Scanning Pipeline

The scan runs through 5 phases, orchestrated by `MainViewModel.RunScanAsync()`:

### Phase 1: IP Range Parsing
**File:** [`IpRangeParser.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/IpRangeParser.cs)

- Accepts CIDR (`192.168.1.0/24`), range (`10.0.0.1-254`), or single IPs
- Short-form ranges supported (`192.168.1.1-254` expands last octet)
- Max 65,536 addresses per scan
- Network/broadcast addresses excluded for CIDR (except /31, /32)

### Phase 2: Ping Sweep
**File:** [`PingScanner.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/PingScanner.cs)

- 256 concurrent ICMP pings via `SemaphoreSlim`
- 1000ms timeout (configurable)
- Callback per result: `Action<IPAddress, bool, long>`
- Can be skipped ("Skip Ping" setting) for hosts blocking ICMP

### Phase 3: DNS Resolution (parallel with Phase 4)
**File:** [`DnsResolver.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/DnsResolver.cs)

- `Dns.GetHostEntryAsync()` for PTR record lookup
- Runs in parallel with port scanning
- Updates UI in real-time via `Dispatcher.Invoke()`

### Phase 4: Port Scanning
**File:** [`PortScanner.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/PortScanner.cs)

- TCP connect scan via `TcpClient.ConnectAsync()`
- 64 concurrent connections per host
- 2000ms timeout (configurable)
- Service name resolved via [`ServiceNameMap.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/ServiceNameMap.cs)

### Phase 5: AI Service Probing (AI and Deep AI presets)
**File:** [`AiServiceProber.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/AiServiceProber.cs)

- 32 concurrent HTTP probes
- Iterates 45 probe definitions ordered by specificity
- Each probe: GET request → match status code / body / headers
- Keeps best match per port (highest specificity wins)
- Special Docker API handling: enumerates containers, filters by 33 AI image patterns
- Detail extraction: model names, versions, GPU info, VRAM, metrics counts
- **Deep AI mode:** `ignorePortHints=true` skips PortHint filtering, running all probes on every open port

## Concurrency Model

| Phase | Max Concurrent | Mechanism |
|-------|---------------|-----------|
| Ping Sweep | 256 | `SemaphoreSlim(256)` |
| Port Scan | 64 per host | `SemaphoreSlim(64)` |
| AI Probing | 32 total | `SemaphoreSlim(32)` |

All I/O is fully async. UI thread safety via `Dispatcher.Invoke()`. Shared state protected by `lock` objects. `CancellationToken` propagates through the entire pipeline.

## AI Service Probe System

### How Probes Work

Each probe definition in [`AiServiceProber.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/AiServiceProber.cs) has:

```csharp
class ProbeDefinition
{
    string Path;           // HTTP endpoint to hit (e.g., "/api/tags")
    string ServiceName;    // What we call it (e.g., "Ollama")
    string Category;       // Grouping (e.g., "LLM", "Image Gen")
    string Confidence;     // "high", "medium", "low"
    int Specificity;       // 100 = most specific, 20 = generic fallback
    int? StatusCode;       // Expected HTTP status (null = don't check)
    string? BodyContains;  // Substring match in response body
    string? HeaderContains;// Substring match in response headers
    int? PortHint;         // Only run on this port (null = any port)
}
```

**Matching logic:**
1. Iterate probes from highest to lowest specificity
2. Skip probes where `PortHint` doesn't match current port (unless `ignorePortHints` is true)
3. Send HTTP GET with `User-Agent: AgrusScanner/1.0`
4. HTTPS for ports 8443, 2376; HTTP otherwise
5. Match against `StatusCode`, `BodyContains`, `HeaderContains`
6. Track best match (highest specificity) per port
7. Extract details (models, versions, GPU info) from response body

### All 45 Probe Definitions

**LLM Services (16 probes, 12 services):**
Ollama (2), vLLM, HF TGI, llama.cpp (2), KoboldCpp (2), LM Studio, LiteLLM (2), Jan.ai, GPT4All, LocalAI, FastChat (2)

**Image Generation (4 probes, 2 services):**
Stable Diffusion A1111 (2), ComfyUI (2)

**ML Platforms (10 probes, 9 services):**
NVIDIA Triton (2), TorchServe (2), TensorFlow Serving, MLflow (2), Ray Serve, BentoML, KServe, MindsDB, Tabby

**AI Platforms (5 probes, 5 services):**
Open WebUI, AnythingLLM, LibreChat, Flowise, Dify

**GPU Infrastructure (3 probes, 3 services):**
NVIDIA DCGM Exporter, Triton Metrics, TorchServe Metrics

**Container Detection (1 probe):**
Docker API (with 33 AI image patterns)

**Generic Fallbacks (4 probes):**
OpenAI-compatible, LM Studio/TGW, Gradio AI App, Generic LLM Service

### Docker AI Container Patterns

Matched against container image names (case-insensitive substring):

```
ollama, localai, vllm, text-generation-inference, tritonserver,
torchserve, tensorflow/serving, stable-diffusion, comfyui,
open-webui, anythingllm, librechat, flowise, dify, litellm,
koboldcpp, tabbyml, whisper, llama, mistral, deepseek, qdrant,
chromadb, weaviate, milvus, bentoml, langchain, langserve,
ray, mlflow, mindsdb, privategpt, gpt4all
```

## MCP Server

### Startup

**File:** [`McpHostManager.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Mcp/McpHostManager.cs)

- Embedded ASP.NET Core `WebApplication`
- Endpoint: `http://localhost:{port}/mcp` (default port: 8999)
- Server info: `name: "agrus-scanner", version: "0.2.2"`
- Started automatically in GUI mode, or via `--mcp-only` flag for headless

### Tool Definitions

**File:** [`ScannerMcpTools.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Mcp/ScannerMcpTools.cs)

Four tools decorated with `[McpServerTool]`:

| Tool | Parameters | Returns |
|------|-----------|---------|
| `scan_network` | `ip_range` (required), `preset` (optional: quick/common/extended/ai/deep-ai/none), `skip_ping` (optional) | JSON array of host objects |
| `probe_host` | `ip` (required), `ports` (optional: csv or preset name) | JSON host object |
| `export_results` | `file_path` (required), `format` (optional: json/csv/auto) | JSON confirmation with path and count |
| `list_presets` | none | JSON array of preset definitions |

All tools are static methods, accept `CancellationToken`, return serialized JSON strings.

### MCP-Only Mode

```bash
AgrusScanner.exe --mcp-only
```

- No main window shown
- System tray icon ([`TrayIcon.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/TrayIcon.cs)) with context menu
- MCP server starts immediately
- App exits from tray menu

## Agent Integrations

### Claude Code

**Config:** [`.mcp.json`](https://github.com/NYBaywatch/AgrusScanner/blob/master/.mcp.json) in project root points to `http://localhost:8999/mcp`

**Skill:** [`.claude/skills/agrus-scanner/SKILL.md`](https://github.com/NYBaywatch/AgrusScanner/blob/master/.claude/skills/agrus-scanner/SKILL.md) - AgentSkills-compatible markdown defining when/how to use the tools.

### OpenClaw

**Plugin:** [`openclaw-plugin/`](https://github.com/NYBaywatch/AgrusScanner/tree/master/openclaw-plugin) - TypeScript plugin that proxies tool calls to the MCP server.

Install: `openclaw plugins install ./openclaw-plugin`

## Models

### HostResult
Represents a single discovered host. Implements `INotifyPropertyChanged` for real-time UI binding.

**Properties:** `IpAddress`, `Hostname`, `IsAlive`, `PingMs`, `OpenPorts` (List\<PortResult\>), `AiServices` (List\<AiServiceResult\>), `Status`

### PortResult
Simple record: `Port` (int), `ServiceName` (string)

### AiServiceResult
**Properties:** `ServiceName`, `Category`, `Port`, `Confidence`, `Specificity`, `Details`

### ScanConfig
Holds scan parameters and defines the 5 static port presets (Quick/Common/Extended/AI/AllPorts).

### AppSettings
Persisted user preferences: `McpPort`, `ExtraPorts` dict, `RemovedPorts` dict, `SkipPing`

## Services

All scanning services are **stateless** - they can be reused across scans without cleanup.

| Service | Responsibility | Key Method |
|---------|---------------|------------|
| `PingScanner` | ICMP ping sweep | `ScanAsync(IEnumerable<IPAddress>, callback, ct)` |
| `PortScanner` | TCP port scanning | `ScanAsync(string ip, int[] ports, ct)` |
| `DnsResolver` | Reverse DNS | `ResolveAsync(string ip, ct)` |
| `AiServiceProber` | AI service HTTP probing | `ProbeAllAsync(string ip, int[] openPorts, ct, ignorePortHints)` |
| `IpRangeParser` | IP range parsing | `Parse(string input)` (static) |
| `ServiceNameMap` | Port → name lookup | `GetServiceName(int port)` (static) |
| `SettingsService` | JSON settings I/O | `Load()`, `Save(settings)` |

## UI Layer

### MainWindow.xaml
- WPF DataGrid bound to `ObservableCollection<HostResult>`
- Columns: IP, Hostname, Ping, Status, AI Service, Ports
- Dark theme via [`DarkTheme.xaml`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Themes/DarkTheme.xaml)
- Zoom: Ctrl+=/- and Ctrl+MouseWheel
- Settings panel with preset selection, custom port overrides
- Export button: saves results to CSV or TXT via `SaveFileDialog`

### Visual Indicators
- Alive hosts: green-tinted row (`#1a2a1a`)
- AI services: orange accent
- Progress bar: real-time scan percentage
- Status bar: `% | completed/total | elapsed | alive count`

## Settings & Persistence

**Location:** `%LOCALAPPDATA%\AgrusScanner\settings.json`

```json
{
  "McpPort": 8999,
  "SkipPing": false,
  "ExtraPorts": {
    "quick": "9090,8888",
    "common": ""
  },
  "RemovedPorts": {
    "quick": "21",
    "common": ""
  }
}
```

Loaded on startup by [`SettingsService.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/SettingsService.cs). Custom ports are merged with presets at scan time (union extra, subtract removed).

## Build & Packaging

```bash
# Build the application
dotnet build AgrusScanner.sln -c Release

# Publish self-contained
dotnet publish AgrusScanner/AgrusScanner.csproj -c Release -r win-x64 --self-contained

# Build MSI installer
.\build-installer.ps1
```

The installer project uses WiX Toolset to produce an MSI. The published app is self-contained (includes .NET runtime, no installation required on target).

## Extending the Scanner

### Adding a New AI Service Probe

In [`AiServiceProber.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Services/AiServiceProber.cs), add a `ProbeDefinition` to the `Probes` array:

```csharp
new()
{
    Path = "/api/health",          // Endpoint to probe
    ServiceName = "MyService",     // Display name
    Category = "LLM",             // Category for grouping
    Confidence = "high",           // high/medium/low
    Specificity = 85,              // Higher = preferred over generic
    BodyContains = "my_unique_key",// Substring match
    PortHint = 5555                // Optional: only probe this port
},
```

Place it in the array ordered by category, with higher-specificity probes before lower ones.

To extract details, add a case to `TryExtractDetails()`:
```csharp
"MyService" when root.TryGetProperty("models", out var m) =>
    FormatModelNames(m),
```

### Adding a Docker AI Pattern

Append to the `AiDockerPatterns` array in `AiServiceProber.cs`:
```csharp
"myservice"  // case-insensitive substring match on image name
```

### Adding a New Port Preset

Add a static array to [`ScanConfig.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Models/ScanConfig.cs):
```csharp
public static readonly int[] MyPresetPorts = [80, 443, 8080, 5555];
```

Then wire it into the ViewModel and MCP tools.

### Adding a New MCP Tool

In [`ScannerMcpTools.cs`](https://github.com/NYBaywatch/AgrusScanner/blob/master/AgrusScanner/Mcp/ScannerMcpTools.cs):
```csharp
[McpServerTool(Name = "my_tool")]
public static async Task<string> MyTool(
    [Description("Parameter description")] string param,
    CancellationToken ct)
{
    // Implementation
    return JsonSerializer.Serialize(result);
}
```
