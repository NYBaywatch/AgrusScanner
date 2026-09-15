# Agrus Scanner — Signed Signature Feed

## Goal

Ship new detection signatures weekly without shipping a new MSI. The app keeps its engine (probing, detail extraction, UI) and pulls definitions (probes, AI ports, Docker image patterns) from a signed package, the way AV products separate engine from definitions.

The app must never load a definitions file it cannot cryptographically verify came from our build pipeline. Tampered, corrupted, or third-party files are rejected and the embedded baseline is used instead.

## What moves out of code

Today all three tables live in `AiServiceProber.cs` / `ScanConfig.cs` as C# arrays:

| Table | Today | Becomes |
|-------|-------|---------|
| `AiServiceProber.Probes` (99 `ProbeDefinition`) | C# array | `probes[]` in catalog |
| `AiServiceProber.AiDockerPatterns` | C# array | `dockerPatterns[]` in catalog |
| `ScanConfig.AiPorts` | C# array | `aiPorts[]` in catalog |

What stays in code: `TryExtractDetails` and `NeedsDetailExtraction` (per-service C# branches that pull model names, versions, GPU info). A new service added by signature alone is detected and labeled; rich details arrive with the next app release. `minAppVersion` in the package header prevents a feed from depending on extraction code an older app lacks.

## Package format: `.agsig`

Binary envelope built by CI, opened only by the app. Not intended to be human-edited.

```
offset  size  field
0       4     magic  "AGSG"
4       1     format version (1)
5       2     header length (LE)
7       n     header  — UTF-8 JSON:
                { "sigVersion": "2026.09.15.1",
                  "minAppVersion": "0.4.0",
                  "created": "2026-09-15T14:00:00Z",
                  "keyId": "k1",
                  "payloadLength": 12345,
                  "payloadSha256": "..." }
7+n     m     payload — gzip(JSON catalog), AES-256-GCM encrypted
                (12-byte nonce prefix, 16-byte tag suffix)
end-64  64    ECDSA P-256 / SHA-256 signature (IEEE P1363 r||s) over bytes [0 .. end-64)
```

**Catalog JSON (inside payload):**

```json
{
  "schemaVersion": 1,
  "probes": [
    { "path": "/", "serviceName": "Ollama", "category": "LLM",
      "confidence": "high", "specificity": 100,
      "statusCode": null, "bodyContains": "Ollama is running",
      "headerContains": null, "portHint": null }
  ],
  "aiPorts": [11434, 8000, ...],
  "dockerPatterns": ["ollama", "localai", ...]
}
```

Field names mirror `ProbeDefinition` one-to-one so the existing `ProbeCatalogTests` run unchanged against the decoded catalog.

## Trust model

| Layer | Purpose | Key location |
|-------|---------|--------------|
| ECDSA P-256 signature | Authenticity + integrity. The only layer that matters for tamper-proofing. Chosen over Ed25519 because it is built into .NET (no crypto dependency). | Private key: GitHub Actions secret `AGSIG_SIGNING_KEY`. Public key: `SignatureStore.PublicKeys["k1"]`. |
| AES-256-GCM | Opacity. Stops casual reading/editing on disk. Key ships in the binary so this is obfuscation, not secrecy. | `SignatureStore.AesKey` in the app; GitHub Actions secret `AGSIG_AES_KEY` for CI. |
| SHA-256 in header | Cheap corruption check before decrypt. | — |

**Load rules, in order. Any failure → discard file, log reason, use embedded baseline.**

1. Magic and format version match.
2. ECDSA signature verifies against a compiled-in public key (`keyId` selects it during rotation).
3. `minAppVersion` ≤ running app version.
4. `sigVersion` > currently loaded `sigVersion` (no downgrades from disk, prevents replay of an old feed).
5. Decrypt, verify payload SHA-256, inflate, deserialize.
6. Catalog passes the same invariants as `ProbeCatalogTests` at runtime: count ≥ embedded baseline count, no duplicate (path, serviceName, portHint), valid categories/confidence, specificity 1–100.

The embedded baseline is `signatures/catalog.json` compiled into the assembly as a plain embedded resource, not an `.agsig`. It is trusted because it is part of the Authenticode-signed binary, and this keeps the private key out of every developer build. It still goes through step 6 (the same `SignatureCatalog.Parse` + `Validate`), so there is one catalog parser. Only external files go through steps 1-5.

The app never deserializes a plain JSON catalog from disk. There is no "developer override" that skips verification in release builds.

## Update flow

1. On startup and every `SignatureCheckIntervalHours` (default 24), app calls `GET https://api.jpftech.com/agrus/update-check?v=0.4.0&os=win11&sig=2026.09.15.1`.
2. Lambda response gains two fields: `"sig_version": "2026.09.22.1"`, `"sig_url": "https://api.jpftech.com/agrus/sig/latest.agsig"`.
3. If `sig_version` > loaded version:
   - **Notify** mode: show banner "New signatures available (2026.09.22.1)" with Install button.
   - **Auto** mode: download to `%LocalAppData%\AgrusScanner\signatures.agsig.tmp`, verify (rules 1–6), atomically rename over `signatures.agsig`, hot-swap the in-memory catalog, show a short toast.
   - **Off**: no check for signatures (app-update check still governed by `CheckForUpdates`).
4. Download over HTTPS only. TLS is defense in depth; the ECDSA check is the actual gate, so a CDN or DNS compromise cannot inject signatures.

Status bar shows `App 0.4.0 · Signatures 2026.09.22.1` so users can see what they are running.

## Publishing pipeline

Signature source of truth stays human-editable in the repo: `signatures/catalog.json`. This replaces the C# arrays as the place new services are added.

`.github/workflows/signatures.yml` on push to `master` touching `signatures/**`:

1. `dotnet test` — `ProbeCatalogTests` load `signatures/catalog.json` and gate it.
2. `dotnet run --project AgrusScanner.SigTool -- pack signatures/catalog.json --version $(date +%Y.%m.%d).$RUN --min-app 0.4.0 --key env:AGSIG_SIGNING_KEY --aes env:AGSIG_AES_KEY --out latest.agsig` (secrets are passed as `env:NAME` so they never appear on a command line)
3. Upload `latest.agsig` and `latest.json` (`{ "sig_version": ..., "sha256": ... }`) to S3 behind `api.jpftech.com/agrus/sig/`. Lambda reads `latest.json` to answer the update check.
4. Also attach the `.agsig` to the next GitHub release for transparency.

`AgrusScanner.SigTool` is a small console project in the solution with `keygen`, `validate`, `pack`, `verify`, and `dump` (decode to JSON for diffing) commands. It compiles the envelope and catalog sources directly from the app project (linked files), so both sides agree byte for byte.

The app build embeds `signatures/catalog.json` as-is; `ProbeCatalogTests` gate it in CI and the app fails fast at startup if it is malformed.

## MCP detection (schema requirement for phase 1)

Today the only MCP probe is our own server (`/mcp` body contains `agrus-scanner`). It only fires on Agrus. Generic MCP servers are invisible because `ProbeDefinition` is GET-only, and MCP needs:

- **Streamable HTTP**: `POST /mcp` with `Accept: application/json, text/event-stream` and a JSON-RPC `initialize` body. Response contains `"protocolVersion"` and `"serverInfo"` (name + version — free detail extraction).
- **Legacy SSE**: `GET /sse` with `Accept: text/event-stream`. Response starts with `event: endpoint`.

Since the catalog schema is being frozen in phase 1, `ProbeDefinition` gains five optional fields now so MCP and any future POST-style service can be added by signature alone:

```
"method": "POST",                       // default GET
"acceptHeader": "application/json, text/event-stream",
"body": "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\", ...}",
"contentType": "application/json",
"headers": { "MCP-Protocol-Version": "2025-06-18" }
```

Full MCP design, including transport-era differences and a test plan, is in `2026-09-15-mcp-detection-design.md`.

Engine change: `ProbeAsync` honors `method`, `body`, `contentType`, `acceptHeader`. Detail extraction for `"MCP Server"` reads `result.serverInfo.name` / `.version`. Common paths to seed: `/mcp`, `/sse`, `/messages`. Common ports already in `AiPorts`: 3000, 8000, 8080; add 8931 (Playwright MCP) and 6274/6277 (MCP Inspector).

## Settings

`AppSettings.cs` additions:

```csharp
public SignatureUpdateMode SignatureUpdates { get; set; } = SignatureUpdateMode.Auto; // Off | Notify | Auto
public int SignatureCheckIntervalHours { get; set; } = 24;
```

Exposed in the settings panel next to the existing "Check for updates" toggle.

## Key management

- Generate one ECDSA P-256 keypair with `sigtool keygen`. Private key → GitHub Actions secret only, never in the repo, never on a dev machine after generation.
- Public key → `SignatureStore.PublicKeys` (keyed by `keyId`).
- Rotation: ship a new app version with both old and new public keys accepted for one release cycle, sign feeds with the new key, then drop the old key. Header gets an optional `keyId` field from day one so rotation is a no-op format-wise.
- AES key → `SignatureStore.AesKey` constant plus the `AGSIG_AES_KEY` secret, rotated only when the format version bumps.

## Files to Create/Modify

| File | Action |
|------|--------|
| `signatures/catalog.json` | Create — human-editable source of truth (migrated from C# arrays) |
| `AgrusScanner.SigTool/` | Create — console project: pack / verify / dump |
| `AgrusScanner/Models/ProbeDefinition.cs` | Create — moved out of AiServiceProber; the JSON contract |
| `AgrusScanner/Models/SignatureCatalog.cs` | Create — catalog model + `Validate()` invariants (shared with SigTool) |
| `AgrusScanner/Services/SignaturePackage.cs` | Create — `.agsig` envelope: pack, verify, decrypt (shared with SigTool) |
| `AgrusScanner/Services/SignatureStore.cs` | Create — embedded baseline, installed-package load, `TryInstall`, hot-swap |
| `AgrusScanner/Services/SignatureUpdater.cs` | Create — check, download, install, mode handling |
| `AgrusScanner/Services/AiServiceProber.cs` | Modify — read `Probes` / `AiDockerPatterns` from `SignatureCatalog` |
| `AgrusScanner/Models/ScanConfig.cs` | Modify — `AiPorts` from `SignatureCatalog` |
| `AgrusScanner/Models/AppSettings.cs` | Modify — `SignatureUpdates`, `SignatureCheckIntervalHours` |
| `AgrusScanner/Services/UpdateChecker.cs` | Modify — send `sig=`, parse `sig_version` / `sig_url` |
| `AgrusScanner/AgrusScanner.csproj` | Modify — embed `signatures/catalog.json`, version 0.4.0 |
| `AgrusScanner.Tests/SignaturePackageTests.cs` | Create — envelope round-trip, tamper, wrong-key, store-rejection tests |
| `.github/workflows/signatures.yml` | Create — test, pack, upload |
| `infra/agrus-update-lambda/lambda_function.py` | Modify — return `sig_version` / `sig_url` from S3 `latest.json` |
| `README.md` | Modify — document signature updates and settings |

## Phasing

1. **Catalog extraction** (own release, v0.4.0) — DONE 2026-09-15: `signatures/catalog.json`, SigTool, embedded baseline, `SignatureCatalog`, `SignaturePackage`, `SignatureStore`. App behaves identically; only the source of the tables changes. Startup already loads a verified `%LocalAppData%\AgrusScanner\signatures.agsig` if one exists, so phase 2 only has to download it.
2. **Feed**: updater, settings, Lambda change, workflow. Small once phase 1 is in.
3. **Nice to have**: signature changelog in the notify banner, "reset to embedded signatures" button.

## Tamper tests (must exist before phase 2 ships)

- Flip one byte in payload → rejected.
- Flip one byte in header → rejected.
- Valid package re-signed with a different key → rejected.
- Valid package with `minAppVersion` above running version → rejected.
- Valid older `sigVersion` than loaded → rejected.
- Valid package with a duplicate probe → rejected.
- Plain JSON dropped at the `.agsig` path → rejected.
- Corrupt file on disk at startup → app starts on embedded baseline without error dialog.
