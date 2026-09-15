# Agrus Scanner — MCP Server Detection Design

Companion to `2026-09-15-signature-feed-design.md` ("MCP detection" section). Goal: detect any
Model Context Protocol server reachable over HTTP, label it, and pull `serverInfo` for display,
using the catalog schema (plus one small addition) rather than per-service C# code.

## 1. Wire behavior (what a scanner will actually see)

MCP HTTP servers exist in three generations. All three are live on real networks today.

| Era | Protocol versions | Handshake | Endpoint shape |
|-----|-------------------|-----------|----------------|
| Legacy HTTP+SSE | `2024-11-05` | `GET /sse` opens stream; first event is `event: endpoint` with the POST URL (e.g. `/messages?sessionId=...`) | Two endpoints, deprecated since 2025-03-26 |
| Streamable HTTP, session-based | `2025-03-26`, `2025-06-18`, `2025-11-25` | `POST /mcp` with JSON-RPC `initialize`; server MAY return `Mcp-Session-Id` | Single endpoint, POST + optional GET/DELETE |
| Streamable HTTP, stateless | `2026-07-28` (current) | No handshake. Every POST carries version in `_meta` and `MCP-Protocol-Version` header; `server/discover` is mandatory | Single POST-only endpoint; GET/DELETE return 405 |

Sources: [2024-11-05 transports](https://modelcontextprotocol.io/specification/2024-11-05/basic/transports),
[2025-06-18 transports](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports),
[2025-06-18 lifecycle](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle),
[2026-07-28 streamable-http](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http),
[2026-07-28 changelog](https://modelcontextprotocol.io/specification/2026-07-28/changelog),
[versioning](https://modelcontextprotocol.io/specification/versioning) ("The current protocol version is 2026-07-28").

**Session-era Streamable HTTP (2025-03-26 .. 2025-11-25), normative rules the probes rely on:**

- POST: client MUST send `Accept: application/json, text/event-stream`; body is one JSON-RPC message; server returns `Content-Type: application/json` (single object) **or** `text/event-stream` (SSE, response arrives as a `data:` line). Client MUST support both.
- GET: client MUST send `Accept: text/event-stream`; server MUST return `text/event-stream` or **405**.
- `Mcp-Session-Id`: server MAY set it on the `InitializeResult` response; requests without it (other than initialize) SHOULD get **400**; expired sessions get **404**; client SHOULD `DELETE` the endpoint with the header to end the session; server MAY answer **405**.
- `MCP-Protocol-Version` header is required on requests *after* initialize; if absent the server SHOULD assume `2025-03-26`. Invalid/unsupported value MUST give **400**.

**Stateless era (2026-07-28):** `MCP-Protocol-Version` and `Mcp-Method` headers are REQUIRED on every POST and MUST match the body `_meta`, else **400** with JSON-RPC `-32020 HeaderMismatch`. Unsupported version gives **400** with `-32022 UnsupportedProtocolVersionError` (`data.supported: [...]`). Unknown method gives **404** with `-32601`. GET/DELETE give **405**; `Mcp-Session-Id` is ignored. Legacy clients sending `initialize` to a modern-only server get a 400 (HTTP) whose error SHOULD name the supported versions.

**Minimal initialize request (legacy era, what we send):**

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"agrus-scanner","version":"0.4.0"}}}
```

Sample response (JSON form; SSE form is the same object on a `data:` line):

```json
{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{"tools":{"listChanged":true},"resources":{"subscribe":true,"listChanged":true},"prompts":{"listChanged":true},"logging":{}},"serverInfo":{"name":"ExampleServer","title":"Example Server Display Name","version":"1.0.0"},"instructions":"Optional instructions for the client"}}
```

**Minimal discover request (modern era):**

```json
{"jsonrpc":"2.0","id":1,"method":"server/discover","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientInfo":{"name":"agrus-scanner","version":"0.4.0"},"io.modelcontextprotocol/clientCapabilities":{}}}}
```

Response carries `result.supportedVersions`, `result.capabilities`, and
`result._meta["io.modelcontextprotocol/serverInfo"].{name,version}` ([server/discover](https://modelcontextprotocol.io/specification/2026-07-28/server/discover)).

**Legacy HTTP+SSE:** `GET /sse` with `Accept: text/event-stream`; the server MUST immediately send `event: endpoint` whose `data:` is the POST URI. The stream never closes on its own, so the engine must read only the first event (section 7).

## 2. Fingerprints that separate MCP from generic JSON-RPC / SSE

Ranked by cost (cheapest first). Strings are exact SDK output and are shared across the reference SDKs.

| Signal | Request needed | What comes back | Source |
|--------|----------------|-----------------|--------|
| **406 Accept rejection** | Plain `GET /mcp` (no Accept) — the engine already does this | `406`, body `{"jsonrpc":"2.0","error":{"code":-32000,"message":"Not Acceptable: Client must accept text/event-stream"},"id":null}` | TS SDK [streamableHttp.ts](https://github.com/modelcontextprotocol/typescript-sdk/blob/main/packages/server/src/server/streamableHttp.ts); Python SDK [streamable_http.py](https://github.com/modelcontextprotocol/python-sdk/blob/main/src/mcp/server/streamable_http.py); C# SDK [StreamableHttpHandler.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/src/ModelContextProtocol.AspNetCore/StreamableHttpHandler.cs) |
| POST without both Accept types | `POST /mcp` | `406` "Not Acceptable: Client must accept both application/json and text/event-stream" | same three SDKs |
| Session-required rejection | `POST` non-initialize without session | `400` "Bad Request: Mcp-Session-Id header is required" (TS), "Bad Request: Missing session ID" (Python), "Bad Request: Server not initialized" (TS stateful before init) | same |
| Stale session | any request with bogus `Mcp-Session-Id` | `404`, `-32001` "Session not found" (TS/C#), "Not Found: Invalid or expired session ID" (Python) | same |
| **initialize result** | `POST initialize` | `result.protocolVersion` + `result.serverInfo` + `result.capabilities`; response header `Mcp-Session-Id` (stateful servers) | lifecycle spec |
| **discover result** | `POST server/discover` | `result.supportedVersions` + `_meta["io.modelcontextprotocol/serverInfo"]` | 2026-07-28 spec |
| Modern-era rejections | any POST | `-32020` HeaderMismatch, `-32022` UnsupportedProtocolVersion (`data.supported`), `404` + `-32601` | 2026-07-28 streamable-http |
| Legacy SSE | `GET /sse` | first bytes `event: endpoint` | 2024-11-05 transports |

`protocolVersion` in `YYYY-MM-DD` form, `serverInfo`, and `Mcp-Session-Id` do not occur in non-MCP JSON-RPC
services, so any one of them is decisive. The `-32000` error codes are implementation-defined and only count
together with the message text.

## 3. Probe catalog entries (ordered by specificity, highest first)

Schema is the spec's `ProbeDefinition` + optional `method`, `acceptHeader`, `contentType`, `body`, plus one
new optional `extraHeaders` map (section 7). `serviceName` is `"MCP Server"` for generic hits; detail
extraction fills in the real name. Bodies are shown unescaped for readability; the catalog stores them as strings.

```json
[
  { "path": "/mcp", "method": "POST", "serviceName": "MCP Server", "category": "MCP Server",
    "confidence": "high", "specificity": 96,
    "acceptHeader": "application/json, text/event-stream", "contentType": "application/json",
    "body": "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"agrus-scanner\",\"version\":\"0.4.0\"}}}",
    "bodyContains": "\"serverInfo\"" },

  { "path": "/mcp", "method": "POST", "serviceName": "MCP Server", "category": "MCP Server",
    "confidence": "high", "specificity": 95,
    "acceptHeader": "application/json, text/event-stream", "contentType": "application/json",
    "extraHeaders": { "MCP-Protocol-Version": "2026-07-28", "Mcp-Method": "server/discover" },
    "body": "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"server/discover\",\"params\":{\"_meta\":{\"io.modelcontextprotocol/protocolVersion\":\"2026-07-28\",\"io.modelcontextprotocol/clientInfo\":{\"name\":\"agrus-scanner\",\"version\":\"0.4.0\"},\"io.modelcontextprotocol/clientCapabilities\":{}}}}",
    "bodyContains": "\"supportedVersions\"" },

  { "path": "/", "method": "POST", "serviceName": "MCP Server", "category": "MCP Server",
    "confidence": "high", "specificity": 90,
    "acceptHeader": "application/json, text/event-stream", "contentType": "application/json",
    "body": "<same initialize body>", "bodyContains": "\"serverInfo\"" },

  { "path": "/sse", "method": "GET", "serviceName": "MCP Server (HTTP+SSE)", "category": "MCP Server",
    "confidence": "high", "specificity": 88,
    "acceptHeader": "text/event-stream", "bodyContains": "event: endpoint" },

  { "path": "/mcp", "method": "POST", "serviceName": "MCP Server", "category": "MCP Server",
    "confidence": "medium", "specificity": 84,
    "acceptHeader": "application/json, text/event-stream", "contentType": "application/json",
    "body": "<same initialize body>", "bodyContains": "\"supported\"" },

  { "path": "/mcp", "serviceName": "MCP Server", "category": "MCP Server",
    "confidence": "medium", "specificity": 80,
    "bodyContains": "must accept text/event-stream" },

  { "path": "/", "serviceName": "MCP Server", "category": "MCP Server",
    "confidence": "medium", "specificity": 78,
    "bodyContains": "must accept text/event-stream" },

  { "path": "/api/mcp", "serviceName": "Home Assistant MCP", "category": "MCP Server",
    "confidence": "medium", "specificity": 75, "statusCode": 401, "portHint": 8123 },

  { "path": "/status", "serviceName": "mcp-proxy", "category": "MCP Server",
    "confidence": "medium", "specificity": 70, "bodyContains": "api_last_activity" },

  { "path": "/", "serviceName": "MCP Inspector", "category": "MCP Server",
    "confidence": "medium", "specificity": 70, "bodyContains": "MCP Inspector", "portHint": 6274 },

  { "path": "/messages", "method": "POST", "serviceName": "MCP Server (HTTP+SSE)", "category": "MCP Server",
    "confidence": "low", "specificity": 40,
    "contentType": "application/json", "body": "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}",
    "bodyContains": "session" }
]
```

Notes on the ordering:

- Entries 1-2 give details for free; they are the only ones worth a POST on every port. Entry 3 covers
  `MapMcp()` at root (C# SDK default) and FastMCP mounted at `/`.
- Entry 5 catches a modern-only server rejecting `initialize` with `UnsupportedProtocolVersionError` (`data.supported`), giving a version list but no name.
- Entries 6-7 are GET-only and cost nothing extra; they are the fallback when a POST is blocked by a proxy or auth.
- The existing `Agrus Scanner MCP` probe (`GET /mcp`, `bodyContains: "agrus-scanner"`) almost certainly never fires: a bare GET to the C# SDK returns the 406 body, not `serverInfo`. Retire it once entry 1 lands (entry 1 returns `serverInfo.name = "agrus-scanner"` and displays it).
- Docker MCP Gateway serves `/sse` and `/mcp` on 8811; it needs no dedicated entry, only the port.
- n8n's MCP Trigger path is random per workflow (`/mcp/<random>`), not probeable; rely on the existing n8n signature. Keep the `/messages` entry at low confidence: it only ever wins when nothing else matches.
- `mcp-proxy /status` and `MCP Inspector` body strings are best-effort and must be confirmed against a live instance before the catalog ships (section 8).

## 4. Default ports and AI-port preset changes

| Tool | Port | Path(s) | Source |
|------|------|---------|--------|
| FastMCP (Python) | 8000 | `/mcp` (SSE mode `/sse`) | [gofastmcp.com/deployment/http](https://gofastmcp.com/deployment/http) |
| Supergateway | 8000 | `/sse` + `/message`, streamable `/mcp` | [README](https://github.com/supercorp-ai/supergateway/blob/main/README.md) |
| mcp-proxy (sparfenyuk) | random unless `--port`; docs use 8080 | `/sse`, `/mcp`, `/servers/<name>/sse`, `/status` | [README](https://github.com/sparfenyuk/mcp-proxy/blob/main/README.md) |
| Playwright MCP | 8931 in docs only; `--port` has no default (stdio) | `/mcp`, `/sse` | [playwright#42688](https://github.com/microsoft/playwright/issues/42688), [playwright-mcp](https://github.com/microsoft/playwright-mcp) |
| MCP Inspector | 6274 UI, 6277 proxy (localhost-bound, bearer token by default) | `/` | [inspector README](https://github.com/modelcontextprotocol/inspector), [issue #724](https://github.com/modelcontextprotocol/inspector/issues/724) |
| Docker MCP Gateway | 8811 | `/sse`, `/mcp` (`--transport sse|streaming`) | [testcontainers dockermcpgateway](https://golang.testcontainers.org/modules/dockermcpgateway/), [docker/mcp-gateway](https://github.com/docker/mcp-gateway) |
| n8n MCP Server Trigger | 5678 (n8n) | `/mcp/<random>` | [n8n docs](https://docs.n8n.io/integrations/builtin/core-nodes/n8n-nodes-langchain.mcptrigger/) |
| Home Assistant MCP Server | 8123 (HA) | `/api/mcp`, 401 without token | [home-assistant.io/integrations/mcp_server](https://www.home-assistant.io/integrations/mcp_server/) |
| Agrus Scanner | 8999 | `/mcp` (localhost only) | `AgrusScanner/Mcp/McpHostManager.cs` |
| TS/C# SDK examples | 3000 / 5000 | `/mcp` | SDK samples |

Already in `ScanConfig.AiPorts`: 3000, 5000, 8000, 8080. **Add:** 8811, 8931, 6274, 6277, 8999.
**Add 8123 only with the `portHint`-gated Home Assistant entry** so a HA box does not get the full 110-probe sweep on a
port that is never an LLM host. Do not add 5678 for MCP reasons (n8n is already covered by its own signature).

## 5. Safety: sessions, cleanup, timeouts

- `initialize` on a stateful legacy server (TS SDK with `sessionIdGenerator`, Python default, C# default) allocates
  a session object held until DELETE or the server's idle timeout. It does not run tools and has no side effects
  beyond memory. Modern (2026-07-28) and stateless-mode servers allocate nothing.
- **Do not send `notifications/initialized`.** It only tells the server we intend to keep talking; a scanner does not.
- **Do send `DELETE <path>` with `Mcp-Session-Id`** when the initialize response carried the header. One extra request,
  only on a hit; ignore the result (405 is legal). This is the spec's own "client SHOULD" cleanup and stops us
  leaving half-open sessions on every scanned host. Do it fire-and-forget with a 1 s timeout.
- Never send `tools/call`, `tools/list`, or `resources/read`. Detection stops at `initialize` / `server/discover`.
- Send no `Origin` header (servers MUST validate it when present; absent is universally accepted). Keep
  `User-Agent: AgrusScanner/1.0` and `clientInfo.name = "agrus-scanner"` so operators can attribute the hit in logs.
- Timeouts: keep the 3 s `HttpClient` timeout for headers; cap SSE body reads at **1.5 s or 64 KB or first
  complete event**, whichever first (section 7). Worst case per port for the MCP set is 2 POSTs + 1 SSE GET,
  roughly +1 s over today on a host with nothing listening on those paths.
- `MaxResponseContentBufferSize` (1 MB) already bounds JSON responses; the SSE reader must apply its own cap
  because `ResponseHeadersRead` bypasses it.

## 6. Detail extraction and display

`TryExtractDetails("MCP Server", ...)` reads, in this order of preference:

1. `result.serverInfo.name`, `result.serverInfo.version`, `result.protocolVersion`, keys of `result.capabilities` (legacy).
2. `result._meta["io.modelcontextprotocol/serverInfo"].{name,version}`, `result.supportedVersions[0]`, keys of `result.capabilities` (modern).
3. `error.data.supported` (modern server rejecting initialize) -> `"MCP " + versions.join("/")`.

Capabilities are reduced to the three user-facing ones: `tools`, `resources`, `prompts` (ignore `logging`,
`completions`, `experimental`, `extensions`). Display format, one line, mirrors the existing `v{x}` style:

```
{name} v{version} · {tools, resources, prompts} · {protocolVersion}
agrus-scanner v0.2.2 · tools · 2025-06-18
playwright-mcp v0.0.41 · tools, resources · 2025-11-25
ExampleServer v1.0.0 · tools, resources · 2026-07-28
```

Omit any segment that is missing; if only the 406 fingerprint matched, `Details` is `"Streamable HTTP (no session)"`.
`serverInfo` is self-reported and unverified (spec note on `server/discover`); treat it as display text only,
truncate name to 40 chars, and strip control characters before it reaches the grid.

## 7. Engine changes beyond the four spec fields

1. **`extraHeaders`** (optional `Dictionary<string,string>`, default null) on `ProbeDefinition`. Needed because
   2026-07-28 rejects any POST lacking `MCP-Protocol-Version` and `Mcp-Method` (400 `-32020`), and both must
   match the body. `acceptHeader` alone cannot express this. Catalog invariant: header names must be token
   characters; values must be visible ASCII.
2. **SSE-aware body read.** When `acceptHeader` contains `text/event-stream`, send with
   `HttpCompletionOption.ResponseHeadersRead`. If the response `Content-Type` is `text/event-stream`, read lines
   until the first blank line after a `data:` line (one complete event), or 64 KB, or 1.5 s, then dispose the
   response (which aborts the socket). Concatenate the `data:` payloads of that event and treat the result as
   `body` for `bodyContains` and detail extraction. Without this, `GET /sse` (stream never closes) times out and
   throws, and SSE-form `initialize` replies from the TS/C# SDKs (SSE by default: `enableJsonResponse=false`;
   C# handler has no JSON option) would be matched against raw `event:`/`data:` framing instead of JSON.
   For `application/json` responses keep today's `ResponseContentRead` path.
3. **Session cleanup hook.** After a successful match on a POST probe, if the response has `Mcp-Session-Id`,
   fire `DELETE` to the same URL with that header (section 5). Lives in `ProbeAsync`, not in the catalog.
4. **`NeedsDetailExtraction`** gains `"MCP Server"` so the status-code-only Home Assistant entry still reads
   the body harmlessly, and `TryExtractDetails` gains the three-way branch from section 6.
5. **Scheme selection.** Unchanged (`http` except 8443/2376). Note Agrus's own server binds `localhost` and
   rejects non-localhost `Host` headers with 403, so it is only discoverable when scanning `127.0.0.1`.

No change to `ProbeCatalogTests` invariants beyond validating `extraHeaders` and that every entry with
`method: "POST"` also sets `contentType` and `body`.

## 8. Test plan

**Unit (no network):** feed canned responses through the SSE reader and `TryExtractDetails`:
JSON initialize result, SSE-framed initialize result (`event: message` / `data: {...}`), `event: endpoint` first
event, 406 body, `-32022` error with `data.supported`, modern `server/discover` result. Assert display strings from
section 6 and that a never-closing SSE stream returns within 1.5 s.

**Integration, in-process (CI-safe):** the test project already has access to `ModelContextProtocol.AspNetCore`
(0.8.0-preview.1, legacy era: `initialize`, SSE responses, `Mcp-Session-Id`, 406/400/404 strings above). Host a
`WebApplication` on `127.0.0.1:0` in a fixture, `MapMcp("/mcp")`, and run `ProbeAsync` against it. This validates
entries 1, 6, the DELETE cleanup, and detail extraction end to end. Do **not** rely on the app's own port 8999
instance: it needs the WPF app running, is localhost-only, and hard-codes `Host` validation, so it is fine for a
manual smoke test but not for CI.

**What the C# SDK cannot cover:** legacy HTTP+SSE (`/sse` + `event: endpoint`) and 2026-07-28 `server/discover`.
Both wire formats are fully specified and trivial, so add two `HttpListener` fakes in the test project (about
30 lines each): one that writes `event: endpoint\ndata: /messages?sessionId=x\n\n` and holds the connection open,
one that answers `server/discover` with the section 1 sample and returns 405 for GET/DELETE.

**Optional live matrix (local, or a nightly CI job with Node):**
`npx @playwright/mcp@latest --port 8931`, `npx supergateway --stdio "npx @modelcontextprotocol/server-everything" --port 8000`,
`npx @modelcontextprotocol/inspector` (6274/6277), `docker mcp gateway run --transport streaming --port 8811`.
Use this run to confirm the two best-effort strings (`mcp-proxy /status`, `MCP Inspector` UI title) before
they ship in the catalog, and to record real `serverInfo` values for the display tests. Upgrading the
`ModelContextProtocol` package to 2.x later gives a genuine 2026-07-28 server for the in-process fixture; at that
point drop the discover fake.
