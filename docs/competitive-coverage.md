# Competitive Coverage — comparable scanners and what they detect

**Updated:** 2026-10-13
**Purpose:** Reference list for the weekly update routine. These are the tools that do the same job as Agrus Scanner (active network probing for AI/MCP services) *and* publish their detection lists, so we can diff our catalog against theirs on a schedule.

## 1. Comparable tools with published signatures

| Tool | Vendor / author | Type | Where the signatures live |
|---|---|---|---|
| **Julius** | Praetorian (open source, Apache-2.0) | Active HTTP fingerprinter, 64 services | `probes/*.yaml` — https://github.com/praetorian-inc/julius |
| **AIMap** | Bishop Fox (open source) | Shodan + live HTTP probing, Nuclei templates | `templates/*.yaml`, `backend/app/services/live_probe.py` — https://github.com/BishopFox/aimap |
| **nmap-ai-recon** (`llm-info`, `mcp-info`, `mcp-enum` NSE) | NCC Group / insidetrust (submitted to nmap-dev, Q2 2026) | Nmap scripts | https://github.com/insidetrust/nmap-ai-recon |
| **shadow-mcp-scanner** | themsquared (open source) | MCP-only, read-only (`server/discover`, `initialize`, `GET /sse`, `tools/list`) | https://github.com/themsquared/shadow-mcp-scanner |
| **MCP-Scanner** | Knostic (open source) | Shodan filters + HTTP/SSE verification for MCP | `shodan_filters.json` — https://github.com/knostic/MCP-Scanner |
| **Ollama Scanner** | Derk van der Woude (blog + script) | Multi-port fingerprinter, honeypot detection | https://derkvanderwoude.medium.com/ollama-scanner-43f2270bb63f |

### Not comparable (no published signature list)

Noma Security, Lasso Security, Cisco AI Defense, Protect AI, Zenity, Nightfall, Harmonic, Nudge Security. These discover AI use through SaaS/API traffic, EDR/MDM agents, cloud-provider integrations or code repos, not by probing LAN hosts, and none of them publishes a per-service fingerprint list. runZero has AI-related fingerprints (e.g. the Ollama CVE-2026-7482 query) but the fingerprint database is not public. Skip these in the weekly check; revisit only if one publishes a list.

## 2. What they look for

### Julius (64 probes)
Self-hosted servers: Ollama, vLLM, SGLang, LocalAI, llama.cpp, HF TGI, NVIDIA NIM, NVIDIA TensorRT-LLM, NVIDIA Triton, BentoML, Ray Serve, Aphrodite Engine, Baseten Truss, DeepSpeed-MII, FastChat, GPT4All, Gradio, Jan, KoboldCpp, LM Studio, MLC LLM, Petals, PowerInfer, TabbyAPI, text-generation-webui.
Gateways/proxies: LiteLLM, Bifrost, Envoy AI Gateway, Helicone, Kong AI Gateway, OmniRoute, Portkey AI Gateway, TensorZero.
UIs/RAG/orchestration: AnythingLLM, AstrBot, BetterChatGPT, Dify, Flowise, h2oGPT, HuggingFace Chat UI, Langflow, LibreChat, LobeHub, NextChat, Onyx, OpenClaw, Open WebUI, PrivateGPT, Quivr, RAGFlow, SillyTavern.
MCP: generic MCP server. Generic: OpenAI-compatible.
Cloud (not network-probeable, ignore): AWS Bedrock, Azure OpenAI, Cloudflare AI Gateway, Databricks, Fireworks, Vertex AI, Groq, Modal, Replicate, Salesforce Einstein, Together AI.

### AIMap
MCP, Ollama (`/` → "Ollama is running"), vLLM (`/version`), LiteLLM (`/health`), LocalAI, LangServe/LangChain, OpenClaw/Clawdbot (18789), Open WebUI, LibreChat, Gradio, Streamlit, ComfyUI, Stable Diffusion, HF TGI, generic `/v1/models` with `WWW-Authenticate` classification. Planned: Triton health/model-repository, llama.cpp `/slots`.

### nmap-ai-recon
Ollama (`/api/tags`), vLLM, SGLang, LiteLLM, LocalAI, text-generation-webui (OpenAI-compatible), HF TGI, HF TEI, llama.cpp (`/props`), KoboldCpp (`/api/extra/version`), Triton/KServe v2, TorchServe, Anthropic API (`/v1/messages`), LM Studio. UIs: Open WebUI (`/api/config`), LibreChat, NextChat, LobeChat, Flowise, AnythingLLM. MCP: Streamable HTTP on ~15 paths incl. `/mcp`, `/`, `/api/mcp`; legacy `/sse`.

### shadow-mcp-scanner / Knostic MCP-Scanner
MCP Streamable HTTP (2026-07-28 `server/discover` with `MCP-Protocol-Version` + `Mcp-Method` headers, and `initialize` fallback), legacy HTTP+SSE `GET /sse`. Posture: OPEN / PROTECTED (bare Bearer) / PROTECTED (RFC 9728 `resource_metadata`).

## 3. Gap list as of 2026-10-13 (in a comparable tool, not in our catalog)

Our catalog: 115 probes / 91 services. MCP coverage is already at parity or better (we probe `server/discover`, `initialize`, root mount, `/sse`, `/messages`, the 406 fingerprint and Home Assistant).

| Service | Seen in | Default port | Notes |
|---|---|---|---|
| NVIDIA TensorRT-LLM (standalone server) | Julius | 8000 | Check whether our Triton/NIM probes already match the `trtllm-serve` OpenAI endpoint |
| Baseten Truss | Julius | 8080 | `/v1/models/model` style |
| DeepSpeed-MII | Julius | 28080 | |
| MLC LLM | Julius | 8000 | OpenAI-compatible, `/v1/models` |
| Petals | Julius | 5000 | Health page |
| PowerInfer | Julius | 8080 | llama.cpp fork, may match llama.cpp probe |
| Bifrost | Julius | 8080 | AI gateway |
| Envoy AI Gateway | Julius | 80 | |
| Helicone | Julius | 8585 | |
| Kong AI Gateway | Julius | 8001 | Admin API |
| OmniRoute | Julius | 20128 | |
| Portkey AI Gateway | Julius | 8787 | |
| TensorZero | Julius | 3000 | |
| AstrBot | Julius | 6185 | |
| BetterChatGPT | Julius | 3000 | |
| h2oGPT | Julius | 7860 | Gradio-based, may collide with Gradio probe |
| HuggingFace Chat UI | Julius | 3000 | |
| LobeHub / LobeChat | Julius, nmap-ai-recon | 3210 | |
| NextChat | Julius, nmap-ai-recon | 3000 | |
| LangServe | AIMap | 8000 | In `dockerPatterns` but no HTTP probe |
| Streamlit | AIMap | 8501 | Port already in `aiPorts`; no probe |

Also worth adding for parity: a `/slots` or `/props` probe for llama.cpp if ours only uses `/health`, and posture classification of `WWW-Authenticate` on 401 (RFC 9728 `resource_metadata` vs bare Bearer) for MCP results.

## 4. How the weekly routine uses this

See the "Competitive coverage check" section in the routine instructions. Summary: once a month, re-read the signature sources in section 1, diff against `signatures/catalog.json`, add only services that expose an unauthenticated HTTP endpoint, respect the ~10-probe cap, and update section 3 of this file with what was added or what remains.
