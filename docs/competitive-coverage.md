# Competitive Coverage — comparable scanners and what they detect

**Updated:** 2026-11-01
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
| **AI-Infra-Guard** | Tencent Zhuque Lab (open source, MIT) | Active HTTP fingerprinter (160 YAML templates) + ~1,600 CVE checks + MCP/agent-skill inspection | `data/fingerprints/*.yaml` — https://github.com/Tencent/ai-infra-guard |

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

### AI-Infra-Guard (160 fingerprints)
Self-hosted servers largely overlapping our existing catalog (Ollama, Gradio, Jan, Triton, Ray, CrewAI, Dynamo, ChromaDB, etc. under its own naming) plus a long tail of niche/regional tools (Chinese RAG platforms, individual MCP server implementations) and dev tools that aren't AI-specific (Jupyter, TensorBoard, Marimo). Also inspects ~1,600 CVEs and MCP servers/agent skills — out of scope for us (signature-only, no CVE/skill inspection in this catalog).

## 3. Gap list as of 2026-11-01 (in a comparable tool, not in our catalog)

Our catalog: 126 probes / 102 services. MCP coverage is already at parity or better (we probe `server/discover`, `initialize`, root mount, `/sse`, `/messages`, the 406 fingerprint and Home Assistant).

**Added this pass** (verified against each service's own docs/source as self-hosted + unauthenticated-GET + distinctive): NVIDIA TensorRT-LLM (`trtllm-serve`), DeepSpeed-MII, MLC LLM, Portkey AI Gateway, Helicone, AstrBot, h2oGPT, LobeHub/LobeChat, NextChat. See `signatures/catalog.json` and the November 2026 README entry.

Remaining gap, checked and either held for a future pass or skipped with a verified reason:

| Service | Seen in | Default port | Notes |
|---|---|---|---|
| Baseten Truss | Julius | 8080 | Verified: unauthenticated `/openapi.json` title `"Baseten Inference Server"`. Not added — budget cap; good candidate for next pass |
| TensorZero | Julius | 3000 | Verified: unauthenticated Prometheus `/metrics` → `tensorzero_inferences_total`. Not added — budget cap; good candidate for next pass |
| HuggingFace Chat UI | Julius | 3000 | Verified: `/api/v2/public-config` → `PUBLIC_APP_ASSETS` field name is a more stable match than Julius's app-name string (operators rename `PUBLIC_APP_NAME`). Medium confidence; candidate for next pass |
| BetterChatGPT | Julius | 3000 | Verified: static root markup, no server auth. Lower priority (smaller/less active project); candidate for next pass |
| GPUStack | direct research (not in a comparable tool's list) | 80 | Verified: unauthenticated `/version` → `git_commit` field. Held back — port 80 is shared with too many ordinary web servers for comfort; revisit with a narrower match or a non-default-port heuristic |
| Bifrost | Julius | 8080 | Verified `/health` → `db_pings` on a stock image, but some third-party hosting templates (e.g. Zeabur) add a login on first boot. Needs one more direct check before shipping |
| Petals | Julius | 5000 | **Skip** — the self-hosted swarm worker (`petals.cli.run_server`) has no local web page; Julius's HTML match belongs to the centralized `chat.petals.dev`/`health.petals.dev` demo apps, not what a LAN host runs |
| PowerInfer | Julius | 8080 | **Skip** — functionally indistinguishable from llama.cpp (identical `Server` header and `/props` shape); already caught by our generic llama.cpp probe, so a dedicated signature would just relabel an existing hit |
| Envoy AI Gateway | Julius | 80 | **Skip** — official docs describe gateway→provider auth policy but never document an "open by default" client-facing posture; typically fronted by the operator's own ingress auth |
| Kong AI Gateway | Julius | 8001 | **Skip** — Kong's own docs confirm the Admin API listens on `127.0.0.1:8001` (loopback-only) by default since Kong 0.12.0; not reachable from a LAN scan on a correctly configured instance |
| OmniRoute | Julius | 20128 | **Skip** — OmniRoute's own security docs classify both endpoints Julius's probe needs as `LOCAL_ONLY`/require-login by default; the signature only fires on a deliberately misconfigured install |
| LangServe | AIMap | 8000 | **Skip** — deprecated by LangChain since Nov 2024 in favor of LangGraph Platform; the signature is also weak (generic `/invoke`/`/playground` route names, not a hardcoded string) |
| Streamlit | AIMap | 8501 | **Skip** — no distinctive unauthenticated body marker found in Streamlit's own docs; even AIMap only fingerprints it passively via Shodan title/favicon, never an active probe |

Also worth adding for parity: a `/slots` or `/props` probe for llama.cpp if ours only uses `/health`, and posture classification of `WWW-Authenticate` on 401 (RFC 9728 `resource_metadata` vs bare Bearer) for MCP results. (Note: our catalog already has both `/props` and `/slots` for llama.cpp, so that first item is done.)

Future candidates surfaced via the AI-Infra-Guard diff, not yet verified against upstream docs: Langfuse (self-hosted LLM observability), LightLLM, MaxKB, Pipecat (voice AI), Mem0 — Mem0's self-hosted server enables auth by default per its own docs, so it would likely be a **skip**, but worth a direct check — and new-api.

## 4. How the weekly routine uses this

See the "Competitive coverage check" section in the routine instructions. Summary: once a month, re-read the signature sources in section 1, diff against `signatures/catalog.json`, add only services that expose an unauthenticated HTTP endpoint, respect the ~10-probe cap, and update section 3 of this file with what was added or what remains.
