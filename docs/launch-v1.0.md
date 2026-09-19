# Agrus Scanner 1.0 launch — Hacker News, GitHub, winget, Microsoft Store

## Status (2026-09-18)

| Channel | Status | Link |
|---|---|---|
| winget | PR opened, awaiting Microsoft's automated validation + moderator merge (usually 1–5 days) | https://github.com/microsoft/winget-pkgs/pull/437102 |
| GitHub repo metadata | Done: description, homepage, 19 topics set | https://github.com/NYBaywatch/AgrusScanner |
| GitHub social preview | **Manual step** (no API): Settings → General → Social preview → upload `docs/screenshot.png` (1200×720 is fine; 1280×640 ideal) | |
| awesome-mcp-servers | PR opened (agent fast-track opted in) | https://github.com/punkpeye/awesome-mcp-servers/pull/14657 |
| awesome-security | PR opened | https://github.com/sbilly/awesome-security/pull/731 |
| awesome-windows | PR opened (maintainer is strict; may be rejected) | https://github.com/0PandaDEV/awesome-windows/pull/300 |
| Hacker News | Draft below; post yourself from your HN account | |
| Microsoft Store | Developer account created (free, storedeveloper.microsoft.com). App "Agrus Scanner" reserved; draft submission: Availability, Properties, Age ratings (Everyone/3+), Packages (R2 URL, x64, /qn) done. Remaining: upload 3 listing images (docs/store/*.png) via the file picker, Save, Submit. | https://partner.microsoft.com/en-US/dashboard/win32apps/b92b452c-7adb-4713-a73b-2f3f276d2853/overview |
| Downloads host | Live: `downloads.jpftech.com` Worker (infra/downloads-worker) in front of R2 bucket `agrus-downloads`; counts installer downloads (GitHub `signatures` release excluded) and signature-package downloads; `/stats.json`, `/badge.json`, `/badge-signatures.json`; `/signatures/*` proxies the GitHub feed (app uses it from 1.0.2) | https://downloads.jpftech.com/AgrusScanner-Setup-1.0.1.msi |
| jpftech.com/tools | Live: Tools page with download, checksum, winget line | https://jpftech.com/tools/ |

After the winget PR merges: `winget install Agrus.AgrusScanner`. Put that one-liner in every future post. Future versions: bump `PackageVersion`, `InstallerUrl`, `InstallerSha256`, `ProductCode`, `ReleaseDate` in `packaging/winget/manifests/...` and run `wingetcreate update Agrus.AgrusScanner --version X --urls <msi-url> --submit`.

**To do:** create a scoped Cloudflare API token (Workers R2 Storage: Edit, this bucket only) and add repo secrets `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` so releases auto-upload.

---

## Show HN

Post as a link post to the repo (not a text post). Title max 80 chars, no marketing words, no exclamation marks.

**Title (pick one):**

1. `Show HN: Agrus Scanner – Find every AI service and MCP server on your network`
2. `Show HN: Open-source Windows scanner that fingerprints LLM servers on your LAN`
3. `Show HN: Agrus Scanner – Shadow AI discovery for Windows, with signed signature updates`

Option 1 is the safest. Option 3 if you want the security angle.

**URL:** `https://github.com/NYBaywatch/AgrusScanner`

**First comment (post immediately after submitting; this is what people read):**

> Author here. Agrus started because a client asked how to find "shadow AI" on their network. Nmap told them port 11434 was open; it didn't tell them it was Ollama serving a 70B model to anyone on the subnet, or that the Docker socket had three AI containers behind it. Nothing on Windows did this well, so I built it.
>
> What it does: normal scanner things (ICMP sweep, TCP connect scan, DNS, CSV export), then talks to each service the way a client would and identifies it. 111 signatures across LLM servers, image/video gen, ML platforms, agent and RAG frameworks, vector DBs, GPU exporters, Docker, and MCP servers. It reports model names, versions, GPU and container details. Native C#/WPF, no Electron, readable on a 4K screen, which was half the motivation.
>
> The 1.0 change I'm most interested in feedback on: detection signatures are now separate from the app and update themselves, AV-style. The catalog is gzip → AES-GCM → ECDSA P-256 signed in CI with a key that only exists as an Actions secret; the app carries the public key and refuses anything that fails verification, is a downgrade, needs a newer app, or tries to do more than read-only discovery (a signature can't add code or new request types; POST is limited to the MCP handshake). New services land in installed copies within a day, no reinstall. Design doc and the tamper tests are in the repo.
>
> It also runs as an MCP server, so Claude Code or similar can do "scan 10.0.0.0/24 and tell me what's exposed."
>
> Limitations, so nobody has to discover them: Windows only. Detail extraction for a brand-new service (model lists etc.) is still code, so a signature can label a service before the next release shows its details. Detection is HTTP-based, so a service bound to localhost or behind auth shows up as an open port only. MIT licensed. Happy to answer anything about the probing or the feed design.

**Timing:** Tuesday–Thursday, 8–10am US Eastern. Don't ask anyone to upvote (HN penalizes voting rings). Reply to every comment in the first two hours. If it doesn't take off, you can re-submit once, a few weeks later, with a different title; HN allows this for Show HN if the first got little attention.

**Likely questions and short answers:**

- *Why not nmap + scripts?* nmap finds the port; this identifies the model. NSE scripts for 111 AI services don't exist, and this ships them as a maintained, signed feed.
- *Isn't this a tool for attackers?* It's read-only discovery on networks you administer, same category as nmap or Angry IP Scanner. It never authenticates, never calls MCP tools, and caps every probe.
- *Why Windows only?* The client environment was Windows, and native WPF is the point (fast, DPI-correct). The probing engine is plain .NET; a CLI port to Linux is plausible if there's demand.
- *Why AES on the feed if the key is in the binary?* Opacity only, stated as such in the design doc. The ECDSA signature is the trust boundary.
- *Does it phone home?* Two optional HTTPS calls: version check to api.jpftech.com (app version + OS version, nothing else) and the signature manifest on GitHub. Both have toggles.

---

## GitHub checklist

- [x] Description, homepage, topics
- [ ] Social preview image (manual, see above)
- [x] awesome-mcp-servers PR
- [x] awesome-security PR
- [x] awesome-windows PR
- [ ] Enable Discussions (Settings → General → Features) so "does it detect X?" questions don't clog Issues
- [ ] Pin the v1.0.1 release and the `signatures` pre-release note in the repo README (already linked)
- [ ] MCP registries: https://registry.modelcontextprotocol.io (needs a `server.json` in the repo; publish with the `mcp-publisher` CLI), https://smithery.ai, https://glama.ai/mcp/servers (auto-indexes from the awesome list PR), https://mcp.so
- [ ] Consider a `SECURITY.md` (responsible-disclosure contact) — reviewers on HN look for it

---

## Microsoft Store

The Store accepts unpackaged Win32 apps (MSI/EXE hosted by you) since 2021, so the existing signed MSI works without MSIX repackaging. What I cannot do for you: create the account or agree to the developer agreement.

**One-time (done 2026-09-18):** registration moved to https://storedeveloper.microsoft.com/onboarding and is now free. Account: jfago@hotmail.com (personal; the Azure signing tenant is a client's and must not own the Store listing). Name "Agrus Scanner" reserved; Partner Center ID b92b452c-7adb-4713-a73b-2f3f276d2853. Note: the old partner.microsoft.com/dashboard/registration URLs show "Access restricted" for personal accounts.

**Submission (I can fill everything except the final Submit if you sign in on the Chrome tab):**

- Packages: the Store rejects URLs that redirect, so GitHub release URLs do not work. Use the R2 URL `https://pub-b5e7279503cd4517a6c29ec7726d0e9c.r2.dev/AgrusScanner-Setup-<version>.msi` (versioned; the Store wants a new URL per version), architecture x64, language English (United States), *Installer parameters* `/qn`, *Package type* MSI.
- Properties: Category **Developer tools** (or **Security**), sub-category Networking. Privacy policy URL: you need a page; the README "Privacy & Updates" section on GitHub works as the URL: `https://github.com/NYBaywatch/AgrusScanner#privacy--updates`. Website: repo URL. Support contact: `https://github.com/NYBaywatch/AgrusScanner/issues`.
- Age ratings: IARC questionnaire, all "no" → rated for everyone.
- Store listing: description below; screenshots 1366×768 or larger (take 2–4: main results grid, an AI-detection detail, Settings showing the signature feed, MCP mode tray); logo 300×300 PNG (export from `AgrusScanner/icon.ico`); short title "Agrus Scanner"; search terms: network scanner, shadow AI, Ollama, MCP, port scanner.
- Notes for certification: paste the "Notes for certification" block below.

**Store description (≤ 10,000 chars, plain text):**

> Agrus Scanner finds every AI service and MCP server on your network.
>
> It is a fast, native Windows network scanner: ping sweep, TCP port scan, hostname resolution, CSV export, with a UI that is readable on high-DPI displays. What makes it different is what happens after a port is found: Agrus probes each service the way a client would and identifies it, reporting model names, versions, GPU details and container information.
>
> Detects 70+ AI/ML services across 13 categories: LLM servers (Ollama, vLLM, llama.cpp, LM Studio, KoboldCpp, Text Generation Inference, TabbyAPI and more), image and video generation (ComfyUI, Stable Diffusion WebUI, Forge, Fooocus, SwarmUI), ML platforms (NVIDIA Triton, TorchServe, TensorFlow Serving, MLflow, Ray Serve), AI platforms and agent frameworks (Open WebUI, AnythingLLM, LibreChat, Flowise, Dify, n8n, Langflow, Letta, OpenHands), RAG platforms, vector databases (Qdrant, Weaviate, Milvus, Chroma), speech services, GPU exporters, Docker containers running AI images, and Model Context Protocol (MCP) servers.
>
> Detection signatures update automatically. New services are delivered through a cryptographically signed feed, so Agrus keeps recognizing new tools without a reinstall. Choose Auto-install, Notify only, or Off.
>
> Runs as an MCP server. AI agents such as Claude Code can use Agrus as a tool to scan networks and probe hosts.
>
> Built for IT admins, security teams, and home-lab users who need visibility into shadow AI, rogue LLM deployments, and GPU infrastructure on networks they manage. Open source under the MIT license.

**Notes for certification (paste into that field):**

> Network reconnaissance tool for networks the user administers. Performs ICMP echo, TCP connect scans and unauthenticated HTTP GET/POST discovery requests only against the IP range the user enters; never authenticates, never modifies remote systems. Outbound internet traffic is limited to an optional version check (api.jpftech.com) and an optional signed detection-signature download (github.com), both user-configurable. Installer is a standard Windows Installer MSI, silent install with /qn, Authenticode-signed (Azure Trusted Signing). No account, no in-app purchases, no telemetry beyond the version check described. Source: https://github.com/NYBaywatch/AgrusScanner

**Expect:** certification takes 1–3 business days for a first Win32 submission. The most common rejection is a missing or unreachable privacy policy URL, so confirm the README anchor resolves before submitting.
