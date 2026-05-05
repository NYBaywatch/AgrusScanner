# AI Detection Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 31 new AI/ML service detection probes across 5 new categories plus 5 refreshes to existing probes, target release v0.3.0.

**Architecture:** Pure data extension to existing structures. No engine changes. New probe entries are appended to `AiServiceProber.Probes[]`, new ports added to `ScanConfig.AiPorts[]`, new image patterns added to `AiServiceProber.AiDockerPatterns[]`, new detail-extractor arms added to `TryExtractDetails`. Each task is one focused edit + build verification + commit.

**Tech Stack:** C# 12, .NET 9 (WPF on Windows), MSI via WiX. Reference: `D:\Working\Projects\Scanner\docs\superpowers\specs\2026-05-05-ai-detection-refresh-design.md`.

**Testing approach:** This project ships no unit-test infrastructure. Verification is `dotnet build` clean compile after each edit, plus a final live smoke test (Task 16). Adding xUnit + HttpClient mocks for probe-row data would be a separate sub-project bigger than this update — explicitly out of scope.

**Anchor convention for Tasks 3–9:** Each probe-addition task uses the GENERIC/FALLBACK comment block as the insertion anchor. Tasks 3–9 must run in order — each inserts content that appears in the file immediately after the prior task's content.

---

## Task 1: Refresh existing probes

Adds 7 new probe entries adjacent to their existing service neighbors (5 services: Ollama, vLLM, LocalAI, Open WebUI, Dify). All additive; no existing entries are modified.

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Add Ollama `/api/version` fallback**

Use Edit with this `old_string`:

```csharp
        // Ollama — model list
        new()
        {
            Path = "/api/tags", ServiceName = "Ollama", Category = "LLM",
            Confidence = "high", Specificity = 95,
            BodyContains = "\"models\""
        },
        // vLLM — /version endpoint
```

Replace with:

```csharp
        // Ollama — model list
        new()
        {
            Path = "/api/tags", ServiceName = "Ollama", Category = "LLM",
            Confidence = "high", Specificity = 95,
            BodyContains = "\"models\""
        },
        // Ollama — /api/version fallback (catches hosts with zero models pulled)
        new()
        {
            Path = "/api/version", ServiceName = "Ollama", Category = "LLM",
            Confidence = "high", Specificity = 85,
            BodyContains = "version"
        },
        // vLLM — /version endpoint
```

- [ ] **Step 2: Add vLLM `/metrics` body-marker probe**

Use Edit with this `old_string`:

```csharp
        // vLLM — /version endpoint
        new()
        {
            Path = "/version", ServiceName = "vLLM", Category = "LLM",
            Confidence = "high", Specificity = 90,
            BodyContains = "version"
        },
        // Hugging Face TGI — /info returns model_id
```

Replace with:

```csharp
        // vLLM — /version endpoint
        new()
        {
            Path = "/version", ServiceName = "vLLM", Category = "LLM",
            Confidence = "high", Specificity = 90,
            BodyContains = "version"
        },
        // vLLM — /metrics with vllm: prefix (stable signal even when /health regresses)
        new()
        {
            Path = "/metrics", ServiceName = "vLLM", Category = "LLM",
            Confidence = "high", Specificity = 88,
            BodyContains = "vllm:"
        },
        // Hugging Face TGI — /info returns model_id
```

- [ ] **Step 3: Add LocalAI `/p2p/token` v3 tiebreaker**

Use Edit with this `old_string`:

```csharp
        // LocalAI fallback — /readyz + /v1/models combo (port 8080 typical)
        new()
        {
            Path = "/v1/models", ServiceName = "LocalAI", Category = "LLM",
            Confidence = "medium", Specificity = 65,
            BodyContains = "\"object\"",
            PortHint = 8080
        },
        // FastChat controller — distinctive port 21001
```

Replace with:

```csharp
        // LocalAI fallback — /readyz + /v1/models combo (port 8080 typical)
        new()
        {
            Path = "/v1/models", ServiceName = "LocalAI", Category = "LLM",
            Confidence = "medium", Specificity = 65,
            BodyContains = "\"object\"",
            PortHint = 8080
        },
        // LocalAI v3 — /p2p/token returns plaintext token (unique to v3+)
        new()
        {
            Path = "/p2p/token", ServiceName = "LocalAI", Category = "LLM",
            Confidence = "high", Specificity = 85,
            StatusCode = 200
        },
        // FastChat controller — distinctive port 21001
```

- [ ] **Step 4: Add Open WebUI `/manifest.json` + `/health` fallbacks**

Use Edit with this `old_string`:

```csharp
        // Open WebUI — root contains "Open WebUI"
        new()
        {
            Path = "/", ServiceName = "Open WebUI", Category = "AI Platform",
            Confidence = "high", Specificity = 90,
            BodyContains = "Open WebUI"
        },
        // AnythingLLM — /api/health returns { online: true }
```

Replace with:

```csharp
        // Open WebUI — root contains "Open WebUI"
        new()
        {
            Path = "/", ServiceName = "Open WebUI", Category = "AI Platform",
            Confidence = "high", Specificity = 90,
            BodyContains = "Open WebUI"
        },
        // Open WebUI — /manifest.json (works even when SSO gates root)
        new()
        {
            Path = "/manifest.json", ServiceName = "Open WebUI", Category = "AI Platform",
            Confidence = "high", Specificity = 92,
            BodyContains = "Open WebUI"
        },
        // Open WebUI — /health (200 fallback when manifest is gated)
        new()
        {
            Path = "/health", ServiceName = "Open WebUI", Category = "AI Platform",
            Confidence = "medium", Specificity = 60,
            StatusCode = 200,
            PortHint = 8080
        },
        // AnythingLLM — /api/health returns { online: true }
```

- [ ] **Step 5: Add Dify `/console/api/version` fallback**

Use Edit with this `old_string`:

```csharp
        // Dify — /console/api/
        new()
        {
            Path = "/console/api/setup", ServiceName = "Dify", Category = "AI Platform",
            Confidence = "high", Specificity = 88,
            StatusCode = 200
        },
        // SillyTavern — root contains "SillyTavern"
```

Replace with:

```csharp
        // Dify — /console/api/
        new()
        {
            Path = "/console/api/setup", ServiceName = "Dify", Category = "AI Platform",
            Confidence = "high", Specificity = 88,
            StatusCode = 200
        },
        // Dify — /console/api/version fallback (reverse-proxy compatibility)
        new()
        {
            Path = "/console/api/version", ServiceName = "Dify", Category = "AI Platform",
            Confidence = "high", Specificity = 85,
            StatusCode = 200
        },
        // SillyTavern — root contains "SillyTavern"
```

- [ ] **Step 6: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors. Warnings about line-ending conversions are OK.

- [ ] **Step 7: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: refresh probes for Ollama, vLLM, LocalAI v3, Open WebUI, Dify"
```

---

## Task 2: Extend `AiPorts[]` with 11 new distinctive ports

**Files:**
- Modify: `AgrusScanner/Models/ScanConfig.cs`

- [ ] **Step 1: Replace `AiPorts` array literal**

Use Edit with this `old_string`:

```csharp
    public static readonly int[] AiPorts = [
        // LLM services
        11434, 8000, 8080, 1234, 1337, 4891, 5001, 3000, 4000,
        // Image generation
        7860, 8188,
        // ML platforms
        8081, 8082, 8265, 8500, 8501, 47334, 47335,
        // AI platforms / UIs
        3001, 3080, 5000,
        // GPU infrastructure
        8002, 9400,
        // Container detection
        2375,
        // Multi-use / fallback
        8443, 21001, 21002
    ];
```

Replace with:

```csharp
    public static readonly int[] AiPorts = [
        // LLM services
        11434, 8000, 8080, 1234, 1337, 4891, 5001, 3000, 4000,
        // LLM serving (v0.3.0)
        2242,
        // Image generation
        7860, 8188,
        // Image generation (v0.3.0)
        7861, 7865, 7801,
        // ML platforms
        8081, 8082, 8265, 8500, 8501, 47334, 47335,
        // AI platforms / UIs
        3001, 3080, 5000,
        // Voice / STT / TTS (v0.3.0)
        8020, 9880,
        // Agent platforms (v0.3.0)
        8283,
        // RAG platforms (v0.3.0)
        5050, 7272, 42110,
        // Embeddings (v0.3.0)
        7997,
        // GPU infrastructure
        8002, 9400,
        // Container detection
        2375,
        // Multi-use / fallback
        8443, 21001, 21002
    ];
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Models/ScanConfig.cs
git commit -m "feat: extend AiPorts to 38 ports for v0.3.0 service coverage"
```

---

## Task 3: Add LLM Serving v0.3.0 additions (7 probe entries)

Adds NIM, Dynamo, OpenLLM (×2 probes), MLX-LM, llamafile, Aphrodite Engine. Inserted immediately before the GENERIC/FALLBACK comment block.

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Insert LLM serving additions section**

Use Edit with this `old_string`:

```csharp
        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

Replace with:

```csharp
        // ═══════════════════════════════════════════
        // LLM SERVING — v0.3.0 ADDITIONS
        // ═══════════════════════════════════════════

        // NVIDIA NIM — /v1/metadata returns NIM-specific JSON
        new()
        {
            Path = "/v1/metadata", ServiceName = "NVIDIA NIM", Category = "LLM",
            Confidence = "high", Specificity = 85,
            BodyContains = "version",
            PortHint = 8000
        },
        // NVIDIA Dynamo — /openapi.json contains "dynamo"
        new()
        {
            Path = "/openapi.json", ServiceName = "NVIDIA Dynamo", Category = "LLM",
            Confidence = "high", Specificity = 82,
            BodyContains = "dynamo",
            PortHint = 8000
        },
        // OpenLLM (BentoML) — /readyz primary
        new()
        {
            Path = "/readyz", ServiceName = "OpenLLM", Category = "LLM",
            Confidence = "medium", Specificity = 70,
            StatusCode = 200,
            PortHint = 3000
        },
        // OpenLLM (BentoML) — root HTML title (higher specificity disambiguator)
        new()
        {
            Path = "/", ServiceName = "OpenLLM", Category = "LLM",
            Confidence = "high", Specificity = 88,
            BodyContains = "OpenLLM",
            PortHint = 3000
        },
        // MLX-LM server (Apple) — /v1/models returns mlx-community model IDs
        new()
        {
            Path = "/v1/models", ServiceName = "MLX-LM", Category = "LLM",
            Confidence = "high", Specificity = 88,
            BodyContains = "mlx-community",
            PortHint = 8080
        },
        // llamafile — root HTML contains "llamafile"
        new()
        {
            Path = "/", ServiceName = "llamafile", Category = "LLM",
            Confidence = "high", Specificity = 85,
            BodyContains = "llamafile",
            PortHint = 8080
        },
        // Aphrodite Engine — port 2242 + /health
        new()
        {
            Path = "/health", ServiceName = "Aphrodite Engine", Category = "LLM",
            Confidence = "high", Specificity = 90,
            StatusCode = 200,
            PortHint = 2242
        },

        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: detect NVIDIA NIM, Dynamo, OpenLLM, MLX-LM, llamafile, Aphrodite"
```

---

## Task 4: Add Embeddings probes (TEI, Infinity)

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Insert Embeddings section**

Use Edit with this `old_string`:

```csharp
        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

Replace with:

```csharp
        // ═══════════════════════════════════════════
        // EMBEDDINGS / RERANKER — v0.3.0
        // ═══════════════════════════════════════════

        // HuggingFace Text Embeddings Inference — /info has auto_truncate (TGI lacks this key)
        new()
        {
            Path = "/info", ServiceName = "HF TEI", Category = "Embeddings",
            Confidence = "high", Specificity = 88,
            BodyContains = "auto_truncate"
        },
        // Infinity (michaelfeil) — /health returns {"unix": <ts>}
        new()
        {
            Path = "/health", ServiceName = "Infinity", Category = "Embeddings",
            Confidence = "high", Specificity = 92,
            BodyContains = "unix",
            PortHint = 7997
        },

        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: detect HF TEI and Infinity embedding servers"
```

---

## Task 5: Add Voice / STT / TTS probes (7 services)

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Insert Voice section**

Use Edit with this `old_string`:

```csharp
        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

Replace with:

```csharp
        // ═══════════════════════════════════════════
        // VOICE / STT / TTS — v0.3.0
        // ═══════════════════════════════════════════

        // Speaches (faster-whisper-server fork) — /v1/models lists Whisper model IDs
        new()
        {
            Path = "/v1/models", ServiceName = "Speaches", Category = "Voice / STT / TTS",
            Confidence = "high", Specificity = 88,
            BodyContains = "Systran/faster-whisper"
        },
        // whisper.cpp server — GET /inference returns 400 with distinctive error
        new()
        {
            Path = "/inference", ServiceName = "whisper.cpp", Category = "Voice / STT / TTS",
            Confidence = "high", Specificity = 80,
            StatusCode = 400,
            BodyContains = "no inference task",
            PortHint = 8080
        },
        // OpenedAI-Speech — /v1/audio/voices returns canonical voice list
        new()
        {
            Path = "/v1/audio/voices", ServiceName = "OpenedAI-Speech", Category = "Voice / STT / TTS",
            Confidence = "high", Specificity = 75,
            BodyContains = "alloy",
            PortHint = 8000
        },
        // F5-TTS — /tts-status/{id} returns JSON with task_id field
        new()
        {
            Path = "/tts-status/dummy", ServiceName = "F5-TTS", Category = "Voice / STT / TTS",
            Confidence = "high", Specificity = 78,
            BodyContains = "task_id",
            PortHint = 8000
        },
        // GPT-SoVITS — port 9880 + /control returns 400
        new()
        {
            Path = "/control?command=ping", ServiceName = "GPT-SoVITS", Category = "Voice / STT / TTS",
            Confidence = "high", Specificity = 88,
            StatusCode = 400,
            PortHint = 9880
        },
        // XTTS-API-Server — /speakers returns array, port 8020 distinctive
        new()
        {
            Path = "/speakers", ServiceName = "XTTS-API-Server", Category = "Voice / STT / TTS",
            Confidence = "high", Specificity = 78,
            StatusCode = 200,
            PortHint = 8020
        },
        // Coqui XTTS Streaming Server — /studio_speakers returns 200
        new()
        {
            Path = "/studio_speakers", ServiceName = "Coqui XTTS Streaming", Category = "Voice / STT / TTS",
            Confidence = "high", Specificity = 75,
            StatusCode = 200,
            PortHint = 8000
        },

        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: detect 7 voice services (Speaches, whisper.cpp, OpenedAI-Speech, F5-TTS, GPT-SoVITS, XTTS, Coqui)"
```

---

## Task 6: Add Image Gen additions (Forge, Fooocus-API)

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Insert Image Gen additions section**

Use Edit with this `old_string`:

```csharp
        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

Replace with:

```csharp
        // ═══════════════════════════════════════════
        // IMAGE GENERATION — v0.3.0 ADDITIONS
        // ═══════════════════════════════════════════

        // SD WebUI Forge — /sdapi/v1/options has forge_-prefixed keys (A1111 lacks these)
        new()
        {
            Path = "/sdapi/v1/options", ServiceName = "SD WebUI Forge", Category = "Image Gen",
            Confidence = "high", Specificity = 92,
            BodyContains = "forge_unet_storage_dtype",
            PortHint = 7861
        },
        // Fooocus-API — /ping returns "pong"
        new()
        {
            Path = "/ping", ServiceName = "Fooocus-API", Category = "Image Gen",
            Confidence = "high", Specificity = 85,
            BodyContains = "pong",
            PortHint = 8888
        },

        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: detect SD WebUI Forge and Fooocus-API"
```

---

## Task 7: Add Video Gen probes (SwarmUI, HunyuanVideo)

Note: SwarmUI requires POST. The current probe engine only does GET — so we use a tolerant GET probe matching the 405-or-200 status range. SwarmUI's `/API/GetNewSession` endpoint returns 405 for GET (Method Not Allowed). The HTML title check on `/` provides a backup.

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Insert Video Gen section**

Use Edit with this `old_string`:

```csharp
        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

Replace with:

```csharp
        // ═══════════════════════════════════════════
        // VIDEO GENERATION — v0.3.0
        // ═══════════════════════════════════════════

        // SwarmUI — root HTML title check (POST-only API endpoints not GET-able)
        new()
        {
            Path = "/", ServiceName = "SwarmUI", Category = "Video Gen",
            Confidence = "high", Specificity = 88,
            BodyContains = "SwarmUI",
            PortHint = 7801
        },
        // HunyuanVideo — Gradio app with HunyuanVideo in title (port 8081)
        new()
        {
            Path = "/", ServiceName = "HunyuanVideo", Category = "Video Gen",
            Confidence = "high", Specificity = 80,
            BodyContains = "HunyuanVideo",
            PortHint = 8081
        },

        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: detect SwarmUI and HunyuanVideo"
```

---

## Task 8: Add Agent Platform probes (5 services)

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Insert Agent Platforms section**

Use Edit with this `old_string`:

```csharp
        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

Replace with:

```csharp
        // ═══════════════════════════════════════════
        // AGENT PLATFORMS — v0.3.0
        // ═══════════════════════════════════════════

        // AutoGen Studio — /api/version returns autogenstudio version
        new()
        {
            Path = "/api/version", ServiceName = "AutoGen Studio", Category = "Agent Platform",
            Confidence = "high", Specificity = 88,
            BodyContains = "autogenstudio",
            PortHint = 8081
        },
        // Letta (formerly MemGPT) — port 8283 + /v1/health/
        new()
        {
            Path = "/v1/health/", ServiceName = "Letta", Category = "Agent Platform",
            Confidence = "high", Specificity = 92,
            BodyContains = "version",
            PortHint = 8283
        },
        // OpenHands (formerly OpenDevin) — /api/options/config has FEATURE_FLAGS
        new()
        {
            Path = "/api/options/config", ServiceName = "OpenHands", Category = "Agent Platform",
            Confidence = "high", Specificity = 90,
            BodyContains = "FEATURE_FLAGS",
            PortHint = 3000
        },
        // CrewAI Studio — Streamlit app with CrewAI Studio in HTML title
        new()
        {
            Path = "/", ServiceName = "CrewAI Studio", Category = "Agent Platform",
            Confidence = "high", Specificity = 82,
            BodyContains = "CrewAI Studio",
            PortHint = 8501
        },
        // Langflow — /health_check returns chat_ready field (distinct from Flowise)
        new()
        {
            Path = "/health_check", ServiceName = "Langflow", Category = "Agent Platform",
            Confidence = "high", Specificity = 90,
            BodyContains = "chat_ready",
            PortHint = 7860
        },

        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: detect AutoGen Studio, Letta, OpenHands, CrewAI Studio, Langflow"
```

---

## Task 9: Add RAG Platform probes (7 services)

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Insert RAG Platforms section**

Use Edit with this `old_string`:

```csharp
        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

Replace with:

```csharp
        // ═══════════════════════════════════════════
        // RAG PLATFORMS — v0.3.0
        // ═══════════════════════════════════════════

        // Onyx (formerly Danswer) — root HTML title contains "Onyx"
        new()
        {
            Path = "/", ServiceName = "Onyx", Category = "RAG Platform",
            Confidence = "medium", Specificity = 75,
            BodyContains = "Onyx",
            PortHint = 3000
        },
        // R2R (SciPhi) — port 7272 + /v3/health
        new()
        {
            Path = "/v3/health", ServiceName = "R2R", Category = "RAG Platform",
            Confidence = "high", Specificity = 88,
            BodyContains = "ok",
            PortHint = 7272
        },
        // kotaemon — Gradio app with "kotaemon" in HTML
        new()
        {
            Path = "/", ServiceName = "kotaemon", Category = "RAG Platform",
            Confidence = "high", Specificity = 82,
            BodyContains = "kotaemon",
            PortHint = 7860
        },
        // RAGFlow — /v1/system/version returns build info containing "RAGFlow"
        new()
        {
            Path = "/v1/system/version", ServiceName = "RAGFlow", Category = "RAG Platform",
            Confidence = "high", Specificity = 90,
            BodyContains = "RAGFlow"
        },
        // Quivr — backend port 5050 + /healthz
        new()
        {
            Path = "/healthz", ServiceName = "Quivr", Category = "RAG Platform",
            Confidence = "high", Specificity = 80,
            StatusCode = 200,
            PortHint = 5050
        },
        // Verba (Weaviate's RAG) — /api/health returns deployments key
        new()
        {
            Path = "/api/health", ServiceName = "Verba", Category = "RAG Platform",
            Confidence = "high", Specificity = 82,
            BodyContains = "deployments",
            PortHint = 8000
        },
        // Khoj — port 42110 distinctive + /api/health
        new()
        {
            Path = "/api/health", ServiceName = "Khoj", Category = "RAG Platform",
            Confidence = "high", Specificity = 90,
            StatusCode = 200,
            PortHint = 42110
        },

        // ═══════════════════════════════════════════
        // GENERIC / FALLBACK (lowest specificity)
        // ═══════════════════════════════════════════
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: detect Onyx, R2R, kotaemon, RAGFlow, Quivr, Verba, Khoj"
```

---

## Task 10: Extend `AiDockerPatterns[]` with new image patterns

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Replace `AiDockerPatterns` array literal**

Use Edit with this `old_string`:

```csharp
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
```

Replace with:

```csharp
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
        "sillytavern", "n8n", "llamafile", "agrus",
        // v0.3.0 additions
        "speaches", "whisper-cpp", "openedai-speech", "xtts",
        "gpt-sovits", "f5-tts", "swarmui", "forge", "fooocus",
        "autogen-studio", "letta", "openhands", "crewai", "langflow",
        "onyx", "r2r", "kotaemon", "ragflow", "quivr", "verba", "khoj",
        "text-embeddings-inference", "tei", "infinity",
        "nim", "dynamo", "openllm", "mlx", "aphrodite",
        "hunyuanvideo", "wan2", "cogvideo"
    ];
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: extend Docker AI image patterns with v0.3.0 services"
```

---

## Task 11: Add detail extractors for new services

Adds `TryExtractDetails` switch arms for services where surfacing identifying info is cheap. Skipped: Infinity (no value-add), Khoj (opaque /api/health), OpenHands (opaque), Onyx (HTML title only), CrewAI Studio (Streamlit), HunyuanVideo (Gradio fallback), kotaemon (Gradio), Quivr, Coqui XTTS Streaming, F5-TTS, OpenedAI-Speech, XTTS-API-Server, Fooocus-API, whisper.cpp, GPT-SoVITS, NIM, Dynamo, Aphrodite — these report category + service name only, which matches the existing pattern for several v0.2.x services (e.g. KServe, MindsDB).

Adding extractors for: HF TEI, MLX-LM, Forge, SwarmUI (no payload — return empty), Letta, R2R, Verba, RAGFlow, Speaches, llamafile, AutoGen Studio, Langflow.

**Files:**
- Modify: `AgrusScanner/Services/AiServiceProber.cs`

- [ ] **Step 1: Add extractor arms inside `TryExtractDetails`**

Find the existing block that ends the LLM extraction arms (just before `// Image generation`):

Use Edit with this `old_string`:

```csharp
                "LocalAI" when root.TryGetProperty("data", out var localData) =>
                    FormatModelNames(localData),

                // Image generation
```

Replace with:

```csharp
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
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors. (LINQ `Count()` on `EnumerateObject()` requires `using System.Linq;` which is already a global using in this project — verify by checking the build output.)

- [ ] **Step 3: Commit**

```bash
git add AgrusScanner/Services/AiServiceProber.cs
git commit -m "feat: detail extractors for v0.3.0 services (TEI, MLX, Letta, R2R, Verba, RAGFlow, Forge, others)"
```

---

## Task 12: Update README.md

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update AI Detection Categories table**

Use Edit with this `old_string`:

```markdown
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
```

Replace with:

```markdown
## AI Detection Categories

| Category | Services Detected |
|----------|-------------------|
| **LLM** | Ollama, vLLM, HF TGI, llama.cpp, KoboldCpp, LM Studio, LiteLLM, Jan.ai, GPT4All, LocalAI, FastChat, Tabby, Xinference, SGLang, text-generation-webui, NVIDIA NIM, NVIDIA Dynamo, OpenLLM, MLX-LM, llamafile, Aphrodite Engine |
| **Image Gen** | Stable Diffusion (A1111), ComfyUI, InvokeAI, SD WebUI Forge, Fooocus-API |
| **Video Gen** | SwarmUI, HunyuanVideo |
| **Voice / STT / TTS** | Speaches, whisper.cpp, OpenedAI-Speech, F5-TTS, GPT-SoVITS, XTTS-API-Server, Coqui XTTS Streaming |
| **ML Platform** | NVIDIA Triton, TorchServe, TensorFlow Serving, MLflow, Ray Serve, BentoML, KServe, MindsDB |
| **AI Platform** | Open WebUI, AnythingLLM, LibreChat, Flowise, Dify, SillyTavern, n8n, PrivateGPT, Gradio apps |
| **Agent Platform** | AutoGen Studio, Letta, OpenHands, CrewAI Studio, Langflow |
| **RAG Platform** | Onyx, R2R, kotaemon, RAGFlow, Quivr, Verba, Khoj |
| **Embeddings** | HF Text Embeddings Inference (TEI), Infinity |
| **Vector DB** | Qdrant, ChromaDB, Weaviate, Milvus |
| **MCP Server** | Agrus Scanner MCP |
| **GPU Infra** | NVIDIA DCGM Exporter, Triton Metrics, TorchServe Metrics |
| **Container** | Docker API with 70+ AI image pattern matches |

Detection goes beyond port scanning - the prober queries service-specific API endpoints, extracts model names, versions, GPU info, and container details.
```

- [ ] **Step 2: Update the AI Scan port count in usage section**

Use Edit with this `old_string`:

```markdown
   - **AI Scan** - 28 AI/ML-specific ports with service probing
```

Replace with:

```markdown
   - **AI Scan** - 38 AI/ML-specific ports with service probing
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: refresh AI detection categories and port count for v0.3.0"
```

---

## Task 13: Update USER_GUIDE.md and user-guide.html

**Files:**
- Modify: `docs/USER_GUIDE.md`
- Modify: `docs/user-guide.html`

- [ ] **Step 1: Read both files to locate every count and category reference**

Run: `Read docs/USER_GUIDE.md` (full file) and `Read docs/user-guide.html` (full file).
Map every occurrence of `45+`, `45 detection`, `28`, category-count badges (`12 services detected`, `2 services detected`, etc.), and the AI Ports table.

- [ ] **Step 2: Update USER_GUIDE.md `45+` headline references**

Use Edit (replace_all = false, fix one at a time):

`old_string`: `**AI/ML services** via HTTP endpoint probing (45+ detection signatures)`
`new_string`: `**AI/ML services** via HTTP endpoint probing (90+ detection signatures across 12 categories)`

`old_string`: `It matches responses against 45 detection signatures to identify AI/ML services.`
`new_string`: `It matches responses against 90+ detection signatures spanning 12 categories to identify AI/ML services.`

- [ ] **Step 3: Update USER_GUIDE.md AI Ports table — append new rows**

Find the AI Ports table (currently 27 rows starting near line 130). Use Edit to insert new rows before the table's closing newline. Use this `old_string` (last existing row + table terminator — adapt to actual file content):

After locating the table, add these rows in port-numeric order — ports already present skip. New rows to insert:

```markdown
| 2242 | Aphrodite Engine | LLM |
| 5050 | Quivr | RAG Platform |
| 7272 | R2R | RAG Platform |
| 7801 | SwarmUI | Video Gen |
| 7861 | SD WebUI Forge | Image Gen |
| 7865 | Fooocus | Image Gen |
| 7997 | Infinity | Embeddings |
| 8020 | XTTS-API-Server | Voice / STT / TTS |
| 8283 | Letta | Agent Platform |
| 9880 | GPT-SoVITS | Voice / STT / TTS |
| 42110 | Khoj | RAG Platform |
```

If the table currently ends with a row like `| 21002 | FastChat Worker | LLM |` and a blank line, insert the new rows immediately above the blank line, sorted by port number into the existing table order if it's port-sorted, otherwise append at the bottom.

- [ ] **Step 4: Update USER_GUIDE.md service-detection table — append new sections**

Find the per-category service tables (line ~175). For each new category (Voice / STT / TTS, Video Gen, Agent Platform, RAG Platform, Embeddings) append a new table section. Existing categories (LLM, Image Gen, AI Platform) gain new rows.

For LLM, append rows:
```markdown
| [NVIDIA NIM](https://docs.nvidia.com/nim/) | 8000 | `/v1/metadata` returns NIM-specific JSON |
| [NVIDIA Dynamo](https://github.com/ai-dynamo/dynamo) | 8000 | `/openapi.json` contains `dynamo` |
| [OpenLLM](https://github.com/bentoml/OpenLLM) | 3000 | `/readyz` + root HTML containing `OpenLLM` |
| [MLX-LM](https://github.com/ml-explore/mlx-lm) | 8080 | `/v1/models` lists `mlx-community/...` models |
| [llamafile](https://github.com/Mozilla-Ocho/llamafile) | 8080 | Root HTML contains `llamafile` |
| [Aphrodite Engine](https://github.com/aphrodite-engine/aphrodite-engine) | 2242 | `/health` on port 2242 |
```

For Image Gen, append:
```markdown
| [SD WebUI Forge](https://github.com/lllyasviel/stable-diffusion-webui-forge) | 7861 | `/sdapi/v1/options` has `forge_*` keys |
| [Fooocus-API](https://github.com/mrhan1993/Fooocus-API) | 8888 | `/ping` returns `pong` |
```

For new sections, append blocks like:

```markdown
### Voice / STT / TTS (7 services detected)

| Service | Port | Detection |
|---------|------|-----------|
| [Speaches](https://github.com/speaches-ai/speaches) | 8000 | `/v1/models` lists `Systran/faster-whisper-*` |
| [whisper.cpp server](https://github.com/ggml-org/whisper.cpp) | 8080 | `/inference` returns 400 with distinctive error |
| [OpenedAI-Speech](https://github.com/matatonic/openedai-speech) | 8000 | `/v1/audio/voices` returns canonical voice list |
| [F5-TTS](https://github.com/SWivid/F5-TTS) | 8000 | `/tts-status/{id}` returns JSON with `task_id` |
| [GPT-SoVITS](https://github.com/RVC-Boss/GPT-SoVITS) | 9880 | Port 9880 + `/control` returns 400 |
| [XTTS-API-Server](https://github.com/daswer123/xtts-api-server) | 8020 | `/speakers` on port 8020 |
| [Coqui XTTS Streaming](https://github.com/coqui-ai/xtts-streaming-server) | 8000 | `/studio_speakers` returns 200 |

### Video Gen (2 services detected)

| Service | Port | Detection |
|---------|------|-----------|
| [SwarmUI](https://github.com/mcmonkeyprojects/SwarmUI) | 7801 | Root HTML title contains `SwarmUI` |
| [HunyuanVideo](https://github.com/Tencent-Hunyuan/HunyuanVideo) | 8081 | Gradio app with `HunyuanVideo` in HTML |

### Agent Platform (5 services detected)

| Service | Port | Detection |
|---------|------|-----------|
| [AutoGen Studio](https://microsoft.github.io/autogen/) | 8081 | `/api/version` contains `autogenstudio` |
| [Letta](https://github.com/letta-ai/letta) | 8283 | `/v1/health/` returns version |
| [OpenHands](https://github.com/All-Hands-AI/OpenHands) | 3000 | `/api/options/config` contains `FEATURE_FLAGS` |
| [CrewAI Studio](https://github.com/strnad/CrewAI-Studio) | 8501 | Root HTML contains `CrewAI Studio` |
| [Langflow](https://github.com/langflow-ai/langflow) | 7860 | `/health_check` contains `chat_ready` |

### RAG Platform (7 services detected)

| Service | Port | Detection |
|---------|------|-----------|
| [Onyx (formerly Danswer)](https://github.com/onyx-dot-app/onyx) | 3000 | Root HTML title contains `Onyx` |
| [R2R](https://github.com/SciPhi-AI/R2R) | 7272 | `/v3/health` on port 7272 |
| [kotaemon](https://github.com/Cinnamon/kotaemon) | 7860 | Root HTML contains `kotaemon` |
| [RAGFlow](https://github.com/infiniflow/ragflow) | 80 | `/v1/system/version` contains `RAGFlow` |
| [Quivr](https://github.com/QuivrHQ/quivr) | 5050 | Backend `/healthz` on port 5050 |
| [Verba](https://github.com/weaviate/Verba) | 8000 | `/api/health` returns `deployments` key |
| [Khoj](https://github.com/khoj-ai/khoj) | 42110 | `/api/health` on port 42110 |

### Embeddings (2 services detected)

| Service | Port | Detection |
|---------|------|-----------|
| [HF Text Embeddings Inference](https://github.com/huggingface/text-embeddings-inference) | 8080 | `/info` has `auto_truncate` key |
| [Infinity](https://github.com/michaelfeil/infinity) | 7997 | `/health` returns `unix` timestamp |
```

- [ ] **Step 5: Update user-guide.html**

The HTML file is hand-synced from USER_GUIDE.md per project history (commit `fa50fa6 docs: sync HTML documentation with Deep AI Scan feature`). Mirror the same edits:

1. Hero chip on line 503: `45+ AI Signatures` → `90+ AI Signatures`
2. Intro paragraph line 514: `(45+ detection signatures)` → `(90+ detection signatures across 12 categories)`
3. Workflow paragraph line 566: `45 detection signatures` → `90+ detection signatures`
4. AI Ports table starting line ~682: append the same 11 new port rows from Step 3
5. Per-category service tables starting line ~724: update LLM and Image Gen counts and rows; add 5 new category sections matching Step 4's markdown structure but in HTML
6. Update each `category-count` span with new totals — final tallies: LLM = 18, Image Gen = 4, Voice/STT/TTS = 7, Video Gen = 2, Agent Platform = 5, RAG Platform = 7, Embeddings = 2, ML Platforms = 8 (unchanged), AI Platforms = 9 (added SillyTavern, n8n, PrivateGPT to existing 5 — wait, those were in the docs already; verify), GPU Infrastructure = 3 (unchanged), Container = 65+ image patterns (was 33)

Use the existing HTML category-section pattern as a template:

```html
<div class="category-header" id="voice-tts">
  <span class="category-badge badge-voice">Voice / STT / TTS</span>
  <span class="category-count">7 services detected</span>
</div>
<table>
  <tr><th>Service</th><th>Port</th><th>Detection</th></tr>
  <tr><td><a href="https://github.com/speaches-ai/speaches">Speaches</a></td><td><code>8000</code></td><td><code>/v1/models</code> lists <code>Systran/faster-whisper-*</code></td></tr>
  <!-- ...remaining rows... -->
</table>
```

Add new badge classes (`badge-voice`, `badge-video`, `badge-agent`, `badge-rag`, `badge-embeddings`) to the existing `<style>` block — copy the existing `badge-llm` block and adjust the color. The exact colors are at the engineer's discretion; use any distinct hue from the existing palette (orange/blue/green/purple/teal — pick five unused ones).

Add navigation entries near line 475 alongside `<a href="#llm-services">LLM Services</a>` for the 5 new category anchors.

- [ ] **Step 6: Commit**

```bash
git add docs/USER_GUIDE.md docs/user-guide.html
git commit -m "docs: refresh user guide for v0.3.0 detection coverage (90+ signatures, 5 new categories)"
```

---

## Task 14: Update DEVELOPER.md and developer.html

**Files:**
- Modify: `docs/DEVELOPER.md`
- Modify: `docs/developer.html`

- [ ] **Step 1: Update DEVELOPER.md service-count breakdown table**

Find the line that currently reads (DEVELOPER.md around line 209):

```
Ollama (2), vLLM, HF TGI, llama.cpp (2), KoboldCpp (2), LM Studio, LiteLLM (2), Jan.ai, GPT4All, LocalAI, FastChat (2)
```

This is one row of a table where category=LLM, count appears in adjacent columns. Use Edit with the surrounding table context as `old_string` and update the LLM row's count and service list. Add new rows for the 5 new categories.

The minimal updated LLM row content:

```
Ollama (3), vLLM (2), HF TGI, llama.cpp (2), KoboldCpp (2), LM Studio, LiteLLM (2), Jan.ai, GPT4All, LocalAI (3), FastChat (2), Xinference, SGLang, text-generation-webui, NIM, Dynamo, OpenLLM (2), MLX-LM, llamafile, Aphrodite
```

(Counts in parentheses indicate multiple probe entries per service.)

Add new rows for: Voice / STT / TTS, Video Gen, Agent Platform, RAG Platform, Embeddings — matching the existing table format. Verify the existing table structure first by reading the file; if it's a Markdown table with columns like `| Category | Probes | Services | Service Names |`, populate accordingly. If it's a different layout, mirror that layout.

- [ ] **Step 2: Update DEVELOPER.md `Place it in the array ordered by category` paragraph**

Find around line 397. Verify the existing instructions about probe ordering still hold (they do — engine still uses specificity scoring; ordering is for readability only). No content change needed unless the surrounding text references specific category counts that are now stale.

- [ ] **Step 3: Update developer.html service-count breakdown table**

Mirror Step 1 in HTML form. Around line 619 there's a `<tr>` with the LLM row. Update its count column and service-name column to match the new content. Append `<tr>` entries for the 5 new categories.

Existing row template (use as model for the 5 new rows):

```html
<tr><td><span class="tag tag-purple">LLM</span></td><td>16</td><td>12</td><td>Ollama (2), vLLM, HF TGI, ...</td></tr>
```

**Counting convention:** Read the existing `Probes[]` array and count entries where `Category == "LLM"` for the probe column, and unique `ServiceName` values within that category for the services column. Do not trust the placeholder counts below — count from source. Approximate target after this PR: ~33 probe rows / ~21 unique services in the LLM row. Apply the same per-category recount for every row.

Sample updated LLM row (replace counts after recounting):

```html
<tr><td><span class="tag tag-purple">LLM</span></td><td>[probes]</td><td>[services]</td><td>Ollama (3), vLLM (2), HF TGI, llama.cpp (2), KoboldCpp (2), LM Studio, LiteLLM (2), Jan.ai, GPT4All, LocalAI (3), FastChat (2), Tabby, Xinference, SGLang, text-generation-webui, NIM, Dynamo, OpenLLM (2), MLX-LM, llamafile, Aphrodite</td></tr>
```

For new categories, mirror the tag class pattern. Add new tag classes to the `<style>` block if needed (`tag-voice`, `tag-video`, `tag-agent`, `tag-rag`, `tag-embeddings`) following the existing `tag-purple` definition pattern.

- [ ] **Step 4: Commit**

```bash
git add docs/DEVELOPER.md docs/developer.html
git commit -m "docs: refresh developer guide service breakdown for v0.3.0"
```

---

## Task 15: Update SKILL.md

**Files:**
- Modify: `.claude/skills/agrus-scanner/SKILL.md`

- [ ] **Step 1: Update service count and category list**

Use Edit with this `old_string`:

```markdown
The scanner detects 45+ AI/ML services across these categories:
- **LLM**: Ollama, vLLM, HuggingFace TGI, llama.cpp, KoboldCpp, LM Studio, LiteLLM, Jan.ai, GPT4All, LocalAI, FastChat, Tabby
- **Image Gen**: Stable Diffusion (A1111), ComfyUI
- **ML Platforms**: NVIDIA Triton, TorchServe, TensorFlow Serving, MLflow, Ray Serve, BentoML, KServe, MindsDB
- **AI UIs**: Open WebUI, AnythingLLM, LibreChat, Flowise, Dify
- **GPU/Infra**: NVIDIA DCGM, Docker API with AI container detection
```

Replace with:

```markdown
The scanner detects 60+ AI/ML services across these categories:
- **LLM**: Ollama, vLLM, HuggingFace TGI, llama.cpp, KoboldCpp, LM Studio, LiteLLM, Jan.ai, GPT4All, LocalAI, FastChat, Tabby, Xinference, SGLang, text-generation-webui, NVIDIA NIM, NVIDIA Dynamo, OpenLLM, MLX-LM, llamafile, Aphrodite Engine
- **Image Gen**: Stable Diffusion (A1111), ComfyUI, InvokeAI, SD WebUI Forge, Fooocus-API
- **Video Gen**: SwarmUI, HunyuanVideo
- **Voice / STT / TTS**: Speaches, whisper.cpp, OpenedAI-Speech, F5-TTS, GPT-SoVITS, XTTS-API-Server, Coqui XTTS Streaming
- **ML Platforms**: NVIDIA Triton, TorchServe, TensorFlow Serving, MLflow, Ray Serve, BentoML, KServe, MindsDB
- **AI UIs**: Open WebUI, AnythingLLM, LibreChat, Flowise, Dify, SillyTavern, n8n, PrivateGPT
- **Agent Platforms**: AutoGen Studio, Letta, OpenHands, CrewAI Studio, Langflow
- **RAG Platforms**: Onyx, R2R, kotaemon, RAGFlow, Quivr, Verba, Khoj
- **Embeddings**: HF Text Embeddings Inference (TEI), Infinity
- **Vector DB**: Qdrant, ChromaDB, Weaviate, Milvus
- **GPU/Infra**: NVIDIA DCGM, Docker API with 70+ AI image pattern matches
```

- [ ] **Step 2: Update preset port count if mentioned**

Use Edit with this `old_string`:

```markdown
- `preset` — Port preset: `quick` (6 ports), `common` (22 ports), `extended` (58 ports), `ai` (28 AI/ML ports), `deep-ai` (all 65535 ports, full AI probing), `none` (ping only). Default: `quick`
```

Replace with:

```markdown
- `preset` — Port preset: `quick` (6 ports), `common` (22 ports), `extended` (58 ports), `ai` (38 AI/ML ports), `deep-ai` (all 65535 ports, full AI probing), `none` (ping only). Default: `quick`
```

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/agrus-scanner/SKILL.md
git commit -m "docs: update agrus-scanner skill with v0.3.0 categories and ports"
```

---

## Task 16: Build, installer, smoke test

This is the project's release process per `CLAUDE.md`.

**Files:** none modified in this task — only build artifacts.

- [ ] **Step 1: Clean build of the solution**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors and no new warnings beyond what existed before this PR.

- [ ] **Step 2: Rebuild the installer**

Run: `.\build-installer.ps1`
Expected: completes successfully, `Installer\bin\Release\AgrusScanner-Setup.msi` exists and has a recent modification time. If the script outputs the MSI path, capture it.

- [ ] **Step 3: Smoke-test `list_presets`**

Start the MCP server: launch `AgrusScanner.exe --mcp-only` (or use the GUI; either path serves the MCP). With the MCP available, invoke the `list_presets` tool from any MCP client (e.g. Claude Desktop pointed at the running scanner). Verify the response contains:

- `ai` preset with `port_count: 38`
- The port list contains `2242, 5050, 7272, 7801, 7861, 7865, 7997, 8020, 8283, 9880, 42110`

If the user has no MCP client handy, alternative verification: open the GUI, click the AI preset radio, look at the rendered port count chip in the header. It should display `38` (or whatever UI label matches the preset port count).

- [ ] **Step 4: Smoke-test against a known service (optional but strongly recommended)**

If the user has any of these running on the LAN, probe it:
- Ollama on `:11434` — should be detected as LLM with model list
- ComfyUI on `:8188` — should be detected as Image Gen with system info
- Open WebUI on `:8080` or `:3000` — should be detected as AI Platform

If the user has access to a new-category service (e.g. SwarmUI on `:7801`, Letta on `:8283`), probe that too. Report any failures.

- [ ] **Step 5: Verify no regressions in existing detections**

Run a `probe_host` against the same set of services that worked before this PR (e.g. local Ollama). Confirm:
- Service name unchanged
- Category unchanged
- Detail string still extracted

- [ ] **Step 6: Final commit (release-ready)**

If all steps pass, no commit is needed at this point — every prior task already committed. The branch is ready to push.

If a release is being cut:

```bash
# Tag and push (only if user requests release)
git push origin master
# GitHub release: attach Installer\bin\Release\AgrusScanner-Setup.msi per CLAUDE.md
```

---

## Spec coverage check (self-review)

Mapping each spec section to a task:

- 31 new logical services / 32 probe rows → Tasks 3 (LLM 7 rows for 6 services, OpenLLM ×2), 4 (Embeddings 2), 5 (Voice 7), 6 (Image 2), 7 (Video 2), 8 (Agent 5), 9 (RAG 7) = 32 rows. Matches spec ("OpenLLM is one logical service implemented as two probe rows").
- 5 refreshes → Task 1
- 11 new ports → Task 2
- New AiDockerPatterns → Task 10
- TryExtractDetails arms → Task 11
- README refresh → Task 12
- USER_GUIDE.md + user-guide.html → Task 13
- DEVELOPER.md + developer.html → Task 14
- SKILL.md → Task 15
- Build + installer + smoke test → Task 16

All spec requirements covered.

## Risks called out in spec

- **Onyx false-positive surface** (HTML title `Onyx`): mitigated by port hint 3000. Specificity 75 is intentionally below OpenHands (90) and OpenLLM secondary (88) so service-specific paths win when they match.
- **Probe ordering on shared ports**: addressed by specificity scoring — engine picks highest. Forge (92) > A1111 (90), TEI (`auto_truncate` 88) ≠ TGI (`model_id` 88), but TGI's path `/info` produces a different body shape so collision is benign.
- **No tests**: as documented in the plan preamble, building xUnit + HttpClient mocks for probe-row data is out of scope. Manual verification (Task 16) is the gate.
