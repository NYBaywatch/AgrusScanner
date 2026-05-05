# AI Detection Refresh — Design

**Date:** 2026-05-05
**Target release:** v0.3.0
**Status:** Approved (brainstorm), pending implementation plan

## Goal

Update Agrus Scanner's AI/ML service detection to cover 31 self-hostable services that have become mainstream since the last meaningful detection update on 2026-02-23, plus refresh 5 existing probes whose canonical endpoints or auth surfaces changed.

## Background

The current detection set ships 56 probe definitions covering ~28 distinct services across 8 categories: LLM, Image Gen, ML Platform, AI Platform, Vector DB, MCP Server, GPU Infra, Container. The probe engine in `AgrusScanner/Services/AiServiceProber.cs` is sound: specificity scoring, port-hint gating, body/header/status matching, per-service JSON detail extraction. Adding probes is a matter of appending entries — no architecture change required.

Roughly 10 weeks have passed since the comprehensive detection set was added (commit `eb3a9d9`, 2026-02-10). Several categories have matured enough to warrant first-class coverage (voice/video gen, agents, RAG, embeddings), and a handful of existing probes have known issues against current vendor builds.

## Scope

**In scope**
- 31 new probe definitions across 5 new categories and 3 existing categories
- 5 refresh adjustments to existing probes
- 11 new ports added to `AiPorts[]`
- New entries in `AiDockerPatterns[]` for new services
- New `TryExtractDetails` switch arms for services worth surfacing identifying info
- README service-list refresh
- HTML docs refresh if they list services

**Out of scope**
- Architectural changes to the probe engine
- New scan presets (no `voice`, `video`, `agent`, etc.)
- New MCP tools
- UI changes (settings panel, results panel)
- `ScanConfig` schema additions

## Approach

**Approach 1 — drop-in expansion.** Pure data extension to existing structures. Single PR, single release.

Rejected alternatives:
- *Refactor + expand* — split `Probes[]` into per-category files. Doesn't earn its weight at this size; mixes feature work with refactor noise.
- *Per-category presets (`voice`, `video`, `agent`, `rag`)* — adds UX surface for marginal benefit; the existing `ai` preset already covers everything since the engine fires port-hinted probes only on their hint port.

**Port-list strategy: 1a — expand `AiPorts[]` from 27 to 38.** Only 11 of the new services use ports not already in `AiPorts`. Wall-clock impact per host is sub-linear because `PortScanner` runs at concurrency=64 and 38 ports still fit one parallel wave. Rejected: keeping `ai` narrow and adding `ai-extended` — the 11-port delta doesn't justify a new preset.

## New probes — 31 entries

**Category breakdown:** Voice 7 + Image Gen additions 2 + Image/Video Gen 2 + Agent 5 + RAG 7 + Embeddings 2 + LLM additions 6 = **31**. (OpenLLM is one logical service implemented as two probe rows for disambiguation.)

### Voice / STT / TTS (7)

| Service | Path | Marker | Port hint | Notes |
|---|---|---|---|---|
| Speaches (faster-whisper-server fork) | `/v1/models` | `Systran/faster-whisper` | — | OpenAI-compatible. Body lists Whisper model IDs distinguishing from generic OpenAI proxies. |
| whisper.cpp server | `/inference` | status 400 + body `no inference task` | 8080 | GET on `/inference` returns distinctive 400 from whisper.cpp httplib server. |
| OpenedAI-Speech | `/v1/audio/voices` | `alloy` | 8000 | Returns canonical OpenAI voice list (alloy/echo/fable/onyx/nova/shimmer). |
| F5-TTS server | `/tts-status/dummy` | `task_id` | 8000 | FastAPI; `/tts-status/{id}` returns JSON regardless of id validity. |
| GPT-SoVITS | `/control?command=ping` | status 400 (no body marker) | 9880 | Port 9880 is highly distinctive. |
| XTTS-API-Server | `/speakers` | status 200 (array body) | 8020 | FastAPI wrapper around Coqui XTTSv2. |
| Coqui XTTS Streaming | `/studio_speakers` | status 200 | 8000 | Coqui shut down 2024; image still widely deployed. |

### Image Gen — additions (2)

| Service | Path | Marker | Port hint | Notes |
|---|---|---|---|---|
| SD WebUI Forge | `/sdapi/v1/options` | `forge_unet_storage_dtype` | 7861 | Higher specificity than A1111's `sd_model_checkpoint` so it wins when both could match. |
| Fooocus-API | `/ping` | `pong` | 8888 | Plain Fooocus (Gradio on 7865) covered by existing Gradio fallback. |

### Image / Video Gen (2)

| Service | Path | Marker | Port hint | Notes |
|---|---|---|---|---|
| SwarmUI | `/API/GetNewSession` (POST) | `session_id` | 7801 | POST with empty JSON body. GET 405s. |
| HunyuanVideo (Gradio subtype) | `/` | `HunyuanVideo` | 8081 | Title-based subtype; would otherwise fall through to generic Gradio probe. |

### Agent Platform (5)

| Service | Path | Marker | Port hint | Notes |
|---|---|---|---|---|
| AutoGen Studio | `/api/version` | `autogenstudio` | 8081 | Microsoft autogenstudio. Shares port 8081 with HunyuanVideo — different paths. |
| Letta (formerly MemGPT) | `/v1/health/` | `version` | 8283 | Port 8283 is highly distinctive. |
| OpenHands (formerly OpenDevin) | `/api/options/config` | `FEATURE_FLAGS` | 3000 | Single Docker container exposes both backend+frontend on 3000. |
| CrewAI Studio | `/` | `CrewAI Studio` | 8501 | Streamlit app — body title check required. |
| Langflow | `/health_check` | `chat_ready` | 7860 | Distinct from Flowise. Port 7860 collides with A1111/Gradio so body check is mandatory. |

### RAG Platform (7)

| Service | Path | Marker | Port hint | Notes |
|---|---|---|---|---|
| Onyx (formerly Danswer) | `/` | `Onyx` (HTML title) | 3000 | Some legacy installs still self-identify as `Danswer`. |
| R2R (SciPhi) | `/v3/health` | `ok` | 7272 | Port 7272 distinctive. v2 also responds for legacy. |
| kotaemon | `/` | `kotaemon` | 7860 | Cinnamon. Gradio app — title check distinguishes from other Gradio apps. |
| RAGFlow | `/v1/system/version` | `RAGFlow` | — | Default web port 80 (nginx); use no port hint, fire on any open port. |
| Quivr | `/healthz` | status 200 (no body) | 5050 | Backend port 5050 is the discriminator (frontend on 3000 collides). |
| Verba | `/api/health` | `deployments` | 8000 | Weaviate's RAG. Body has `deployments` key (Weaviate connection state). |
| Khoj | `/api/health` | status 200 | 42110 | Port 42110 highly distinctive. |

### Embeddings / Reranker (2)

| Service | Path | Marker | Port hint | Notes |
|---|---|---|---|---|
| HF Text Embeddings Inference (TEI) | `/info` | `auto_truncate` | — | TGI lacks `auto_truncate` key — clean disambiguation from TGI which also uses `/info`. |
| Infinity (michaelfeil) | `/health` | `unix` | 7997 | Returns `{"unix": <ts>}` — distinctive `unix` key. Port 7997 unique. |

### LLM Serving — additions (6)

| Service | Path | Marker | Port hint | Notes |
|---|---|---|---|---|
| NVIDIA NIM | `/v1/metadata` | `version` (NIM-specific JSON shape) | 8000 | Pair with `/v1/models` echoing `meta/llama-3.1-8b-instruct` style NIM model IDs. |
| NVIDIA Dynamo | `/openapi.json` | `dynamo` | 8000 | New project (NVIDIA, 2025); coverage may be sparse. |
| OpenLLM (BentoML) — primary | `/readyz` | status 200 | 3000 | Two probe entries to disambiguate from other 3000-bound services. |
| OpenLLM (BentoML) — secondary | `/` | `OpenLLM` (HTML title) | 3000 | Higher specificity than primary; both can match. |
| MLX-LM server (Apple) | `/v1/models` | `mlx-community` | 8080 | macOS-only. No `/health` — 404 on `/` is normal. |
| llamafile | `/` | `llamafile` | 8080 | Mozilla. Embeds customized llama.cpp; same `/props` also responds. |
| Aphrodite Engine | `/health` | status 200 | 2242 | PygmalionAI. Port 2242 itself is the strongest signal. |

## Refreshes — 5 existing probes

1. **Ollama** — add `/api/version` (status 200 + body `version`) as a fallback probe with specificity ~85. Catches hosts that pulled zero models where `/api/tags` returns `{"models":[]}` and the `models` body marker won't match.
2. **LocalAI** — add `/p2p/token` (status 200) as a v3-specific tiebreaker. Returns plaintext token string unique to LocalAI v3+. Existing probes stay.
3. **vLLM** — promote `/metrics` body marker `vllm:` to high-specificity (~88) probe. Some `/health` regressions in late 2025 builds; `/metrics` with the `vllm:` prefix is a stable strong signal. Keep `/version` probe.
4. **Open WebUI** — replace fragile `/api/config` heuristic with `/manifest.json` containing `"Open WebUI"`; add `/health` as a 200-status fallback. Stricter SSO-only configs were breaking the original probe.
5. **Dify** — keep `/console/api/setup`; add `/console/api/version` 404-tolerant fallback for reverse-proxied deployments where the backend is mounted at `/api/...`.

No-change items confirmed: TabbyML (`/v1/health` still canonical), NVIDIA Triton (V2 health stable), ComfyUI (`/system_stats` stable), Stable Diffusion A1111 (`/sdapi/v1/*` stable).

## Port additions

`AiPorts[]` grows from 27 → 38. New entries: `2242, 5050, 7272, 7801, 7861, 7865, 7997, 8020, 8283, 9880, 42110`.

The remaining new services live on ports already in `AiPorts`: 8000, 8080, 7860, 3000, 8081, 8888.

## Detail extractors — `TryExtractDetails` additions

For services where surfacing identifying info is cheap (~3 lines per arm) and useful in scan output:

- HF TEI — extract `model_id`, `model_dtype`
- Infinity — omitted (no value-add field on `/health` response beyond the `unix` marker)
- NVIDIA NIM — extract model id from `/v1/models` data array
- NVIDIA Dynamo — extract version from `/openapi.json` `info.version`
- MLX-LM — extract `mlx-community/...` model id from `/v1/models`
- Aphrodite — extract served model id from `/v1/models`
- Letta — extract version from `/v1/health/`
- R2R — extract version from `/v3/health` if present
- Verba — extract deployment count
- Forge — extract `sd_model_checkpoint`
- SwarmUI — return `"session ack"` (no useful payload data)
- Speaches — extract whisper model id
- llamafile — extract version from server header

Extractors are best-effort; failure falls back to empty string per the existing pattern.

## Docker image patterns — `AiDockerPatterns[]` additions

`speaches`, `whisper-cpp`, `openedai-speech`, `xtts`, `gpt-sovits`, `f5-tts`, `swarmui`, `forge`, `fooocus`, `autogen-studio`, `letta`, `openhands`, `crewai`, `langflow`, `onyx`, `r2r`, `kotaemon`, `ragflow`, `quivr`, `verba`, `khoj`, `text-embeddings-inference`, `tei`, `infinity`, `nim`, `dynamo`, `openllm`, `mlx`, `llamafile`, `aphrodite`, `hunyuanvideo`, `wan2`, `cogvideo`.

## Probe-collision resolution

These ports host multiple candidate services. Resolution relies on body markers + specificity ordering — no new logic.

- **Port 8000** — NIM, Dynamo, OpenedAI-Speech, F5-TTS, Coqui-Streaming, Verba, ChromaDB. All use distinct paths or markers; no false-positive overlap expected.
- **Port 3000** — Onyx, OpenHands, OpenLLM, BentoML, Quivr-frontend. Different paths. Onyx HTML title check is the loosest; specificity tuned so service-specific paths win when present.
- **Port 7860** — Langflow, kotaemon, A1111, generic Gradio fallback. Langflow + kotaemon body markers are unique strings; falls through to Gradio if neither matches.
- **Port 8080** — TGI, TEI, MLX-LM, llamafile, whisper.cpp, Open WebUI default. TEI/TGI distinguished via `auto_truncate` key (TEI-only); others via service-name strings.
- **Port 8081** — AutoGen Studio, HunyuanVideo, TorchServe Metrics. Different paths and markers.

## Files touched

- `AgrusScanner/Services/AiServiceProber.cs` — append probes, extend `AiDockerPatterns[]`, add detail-extractor arms
- `AgrusScanner/Models/ScanConfig.cs` — extend `AiPorts[]`
- `README.md` — refresh service-list table (line 44) and example output (line 70); update `45+` headline marker
- `docs/USER_GUIDE.md` — refresh `45+ detection signatures` lines (29, 59), AI Ports table (line 130), service-detection table (line 175)
- `docs/user-guide.html` — refresh hero chip, intro counts, port table, per-category service tables, and per-category counts (LLM 12, Image Gen 2, ML Platforms 8, AI Platforms 5, GPU Infra 3, Container 33, plus new category sections)
- `docs/DEVELOPER.md` — refresh service-count breakdown table (line 209)
- `docs/developer.html` — refresh service-count breakdown table (line 619)
- `.claude/skills/agrus-scanner/SKILL.md` — refresh `45+` count and add new categories to the AI Services Detected list

## Verification plan

- `dotnet build` clean compile, no warnings introduced
- `.\build-installer.ps1` rebuilds the MSI per project release process (CLAUDE.md)
- `list_presets` MCP tool reports `ai` with `port_count: 38`
- Manual smoke test on a host running at least one of: Ollama, A1111, ComfyUI, Open WebUI — verify category labels, detail strings, no regressions
- Optional: stand up Speaches or whisper.cpp in a container and confirm new probe fires

## Risks & non-goals

**Excluded candidates** (documented for future reference):
- *Wyoming Piper* — TCP binary protocol, not HTTP. Out of scope without a banner-grab feature.
- *FastEmbed* — library, not a server.
- *Jina embeddings server* — maintenance mode (no commits in 2026); coverage subsumed by TEI.
- *AnimateDiff standalone* — now an extension of A1111/Forge/ComfyUI.
- *AgentGPT* — abandoned (no commits since 2024).
- *Mochi 1* — borderline-abandoned (no 2026 commits); detected as Gradio fallback if encountered.

**Known false-positive surfaces:**
- Onyx body marker `Onyx` on root HTML — generic word; port hint 3000 narrows scope. Flagged for review if reports come in.
- kotaemon and Langflow share port 7860 with each other and with several Gradio apps — body markers are unique strings, but defensive ordering by specificity is required.

**Performance impact:**
- 38-port `ai` preset adds 11 TCP probes per host vs. 27. At concurrency=64 and 2000 ms timeout, all 38 fit one wave; per-host wall-clock change is below the noise floor of a typical LAN scan.

## Out-of-scope follow-ups (not addressed here)

- Wyoming-protocol banner-grab support (would unlock Piper, faster-whisper, and other home-assistant TTS/STT)
- Per-category scan presets (`voice`, `video`, `agent`, `rag`) — revisit if users request narrower targeting
- A confidence-threshold filter in the UI — currently every match shows; could let users filter to high-only

## Sources consulted (research, 2026-05-05)

speaches-ai/speaches; ggml-org/whisper.cpp; matatonic/openedai-speech; SWivid/F5-TTS; RVC-Boss/GPT-SoVITS; daswer123/xtts-api-server; coqui-ai/xtts-streaming-server; Tencent-Hunyuan/HunyuanVideo; mcmonkeyprojects/SwarmUI; lllyasviel/stable-diffusion-webui-forge; mrhan1993/Fooocus-API; microsoft/autogen; letta-ai/letta; All-Hands-AI/OpenHands; strnad/CrewAI-Studio; langflow-ai/langflow; onyx-dot-app/onyx; SciPhi-AI/R2R; Cinnamon/kotaemon; infiniflow/ragflow; QuivrHQ/quivr; weaviate/Verba; khoj-ai/khoj; huggingface/text-embeddings-inference; michaelfeil/infinity; NVIDIA NIM docs; ai-dynamo/dynamo; bentoml/OpenLLM; ml-explore/mlx-lm; Mozilla-Ocho/llamafile; aphrodite-engine/aphrodite-engine. Refresh validation against ollama/ollama, mudler/LocalAI v3.9.0 release notes, vLLM production metrics docs, Open WebUI docs, Dify api/controllers/console source.
