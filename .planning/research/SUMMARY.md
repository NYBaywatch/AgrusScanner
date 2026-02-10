# Project Research Summary

**Project:** Network Scanner with Shadow AI Detection
**Domain:** Desktop security tool (Windows), network scanning + AI service detection
**Researched:** 2026-02-08
**Confidence:** HIGH

## Executive Summary

This is a Windows desktop network scanner built with Tauri 2.10 (Rust backend + React frontend) that discovers hosts and open ports while specifically detecting unauthorized "shadow AI" services on corporate networks. The core value proposition is identifying rogue LLM deployments (Ollama, LM Studio, Jupyter notebooks) that bypass IT approval - a growing compliance concern in 2026.

The recommended approach combines async Rust network I/O (tokio, surge-ping, socket2) for high-performance scanning with a dark-themed React UI featuring real-time results streaming via Tauri channels. The architecture uses bounded concurrency (50-100 simultaneous connections) to avoid socket exhaustion while maintaining speed. Critical Windows-specific requirements include Npcap for raw packet access, elevated privileges for ICMP scanning, and careful firewall configuration handling.

Key risks center on Windows platform limitations (raw socket restrictions, firewall blocking ICMP, antivirus false positives) and performance scaling challenges (thread pool exhaustion, event emission memory leaks, blocking operations in async runtime). All are mitigable with proactive architecture decisions and defensive coding. The research provides high confidence in stack choices and standard network scanning patterns, medium confidence in shadow AI detection signatures (rapidly evolving landscape).

## Key Findings

### Recommended Stack

Tauri 2.10 provides the ideal foundation - 3-5MB binaries vs Electron's 100MB+, native performance, strong security model. Rust handles network operations with memory safety and zero-cost concurrency via tokio 1.49 async runtime. React 18 + Vite 5 + Tailwind CSS 4 delivers fast development and a responsive UI with built-in dark mode.

**Core technologies:**
- **Tauri 2.10.2**: Desktop framework - smaller binaries, native performance, permissions system for network access
- **Rust 1.80+**: Backend systems language - memory safety, concurrency primitives, required for raw sockets
- **tokio 1.49**: Async runtime - work-stealing scheduler optimized for I/O-bound network operations
- **surge-ping 0.8**: ICMP ping - async implementation with pnet_packet parsing, Windows-compatible
- **socket2 0.5**: Raw socket operations - cross-platform TCP/UDP scanning, manual packet creation
- **rayon 1.10**: Data parallelism - convert sequential iterations to parallel for multi-host scanning
- **React 18 + TypeScript 5**: Frontend UI - component architecture, type safety, excellent ecosystem
- **TanStack Table v8**: Results display - handles 100k+ rows client-side with sorting/filtering
- **Tailwind CSS 4**: Styling - utility-first, dark mode built-in, fast development

### Expected Features

**Must have (table stakes):**
- Host discovery (ICMP ping scan) - core scanner function, users expect network visibility
- Port scanning (common + custom ranges) - identify running services, enable AI detection
- Results table with sorting/filtering - standard presentation format for network data
- Scan progress indicator - essential for multi-minute scans on /24 networks
- Scan cancellation - users need ability to stop long-running operations
- Multithreaded scanning - 10-100x faster than single-threaded, critical for usability
- Performance throttling - prevent network flooding and IPS blocking

**Should have (competitive):**
- Shadow AI detection (local LLMs) - core differentiator, detects Ollama, LM Studio, vLLM on non-standard ports
- Shadow AI detection (dev tools) - extends coverage to Jupyter, TensorBoard, MLflow
- Dark hacker aesthetic UI - appeals to power users, stands out from enterprise tools
- Live scanning updates - see results populate in real-time vs waiting for completion
- Service fingerprinting - reduces false positives, distinguishes generic web servers from AI services

**Defer (v2+):**
- Save/export results (CSV, JSON) - v1.1, users can screenshot for initial validation
- Shadow AI risk scoring - v1.3, contextualizes findings after base detection proven useful
- Cloud API detection - v2.0, requires network traffic inspection beyond port scanning
- Load previous scans - v1.2, enables comparison workflows once save exists

### Architecture Approach

The architecture separates concerns across three layers: React presentational components consume custom hooks (business logic), hooks invoke Tauri commands via IPC, Rust backend handles concurrent scanning with tokio runtime. Streaming results flow backend-to-frontend via Tauri channels (not events - channels handle high throughput). Shared state uses Mutex for thread-safe access from multiple commands. Bounded concurrency via tokio Semaphore prevents resource exhaustion.

**Major components:**
1. **Scanning Engine (Rust)** - executes ping/port/AI detection scans concurrently, manages network I/O with tokio async runtime
2. **Task Scheduler (Rust)** - implements semaphore-based concurrency control (50-100 max), prevents socket exhaustion
3. **Commands Layer (Rust)** - thin IPC boundary exposing scan operations to frontend, validates inputs, delegates to engine
4. **TypeScript Hooks** - encapsulate Tauri communication, state management, event subscriptions, keep components pure
5. **React Components** - presentational UI rendering only, receive data/callbacks from hooks

**Key architectural patterns:**
- Command + Channel for streaming progress (not events - memory leak risk)
- Tokio Semaphore for bounded concurrency (prevents thread pool exhaustion)
- Interior mutability with Mutex for shared state (Tauri wraps in Arc automatically)
- Container/presentational component split (logic in hooks, UI in components)

### Critical Pitfalls

1. **Windows raw socket restrictions** - Raw sockets disabled in Windows XP SP2+. Must use Npcap (modern) not WinPcap (deprecated). Scanner requires Npcap installation as prerequisite and admin privileges. Without detection/fallback, app fails on most Windows systems. **Prevention:** Detect Npcap on startup, show installation instructions if missing, implement TCP connect scan fallback.

2. **Thread pool exhaustion and socket port exhaustion** - Each thread uses ~5 ports/second. Windows has ~64k ephemeral ports but many reserved. At 200ms response time, >93 threads causes socket exhaustion ("cannot create socket" errors). Scanner reports false negatives (errors counted as closed ports). **Prevention:** Bounded task pool with 50-100 max concurrent connections, adaptive parallelism based on network performance.

3. **Blocking operations in async Rust kill throughput** - Mixing blocking I/O (file operations, DNS lookups) with async networking starves tokio event loop. Any latency >100 microseconds blocks all tasks. **Prevention:** Use spawn_blocking() for all blocking operations, async DNS libraries (hickory-dns), profile with tokio-console.

4. **Tauri event emission memory leaks** - Documented issue: emitting millions of events causes webview memory leak, never garbage collected. Long scans grow to 1GB+ memory usage. **Prevention:** Batch events (emit every 100ms with aggregated results), use channels not events for streaming, clear frontend state after scan completes.

5. **Windows Firewall blocks ICMP by default** - Windows Firewall blocks ICMP echo requests on private/public networks (allowed on domain networks only). Ping scan finds zero hosts, users think scanner is broken. **Prevention:** Detect firewall status on startup, show clear warning if ICMP blocked, provide one-click configuration or fallback to TCP SYN scan.

6. **Aggressive scan timing causes paradoxical slowdown** - Network devices rate-limit responses. Scanner triggers rate limiting, causing massive packet loss and retransmissions. Aggressive timing makes scans 10x slower than conservative timing. **Prevention:** Adaptive timing (start conservative, speed up if network reliable), exponential backoff on packet loss, monitor for >2% loss.

7. **Windows Defender antivirus false positives** - Network scanning behavior matches malware reconnaissance patterns. Raw packet access triggers heuristics. Executable quarantined before it runs. **Prevention:** Submit builds to Microsoft Defender for analysis, EV code signing, clear UI explaining scanner purpose, provide AV exception instructions.

## Implications for Roadmap

Based on research, suggested phase structure addresses dependencies and mitigates critical pitfalls:

### Phase 1: Foundation & Windows Integration
**Rationale:** Windows-specific requirements are non-negotiable and create architectural constraints. Must be addressed first to avoid emergency rewrites later. Raw socket restrictions, firewall detection, and Npcap integration are foundational - all scanning features depend on these working correctly.

**Delivers:**
- Tauri + React + Rust project scaffolding
- Npcap detection with installation instructions
- Windows Firewall detection and status reporting
- Admin privilege handling (UAC prompts)
- Basic shared state management (ScanState, ResultsStore)
- Type-safe IPC with tauri-specta (Rust structs -> TypeScript types)

**Addresses pitfalls:**
- Windows raw socket restrictions (Pitfall 1) - detect Npcap, show installation flow
- Windows Firewall ICMP blocking (Pitfall 5) - detect rules, warn user, suggest fixes
- Tauri state management panics (mentioned in PITFALLS) - establish correct state patterns early

**Stack elements used:** Tauri 2.10, Rust, React 18, TypeScript 5, Vite 5, tokio 1.49

**Research flag:** STANDARD PATTERNS - Well-documented in Tauri official docs, skip deep research

---

### Phase 2: Core Scanning Engine
**Rationale:** Implements fundamental network operations with performance correctness from the start. All features depend on working ping/port scan. Bounded concurrency and async patterns must be architected correctly initially - retrofitting is expensive and error-prone.

**Delivers:**
- ICMP ping sweep module (surge-ping + tokio)
- TCP port scanning module (socket2 + tokio)
- Bounded task scheduler with semaphore (50-100 concurrent limit)
- Async/blocking separation (spawn_blocking for non-network operations)
- Basic scan results streaming via Tauri channels
- Scan cancellation support

**Addresses pitfalls:**
- Thread pool exhaustion (Pitfall 2) - semaphore limits concurrent connections
- Blocking operations in async (Pitfall 3) - proper spawn_blocking usage
- Aggressive timing slowdown (Pitfall 6) - adaptive timing from start
- Event memory leaks (Pitfall 4) - use channels not events for streaming

**Implements architecture components:**
- Scanning Engine (ping.rs, port.rs, scheduler.rs)
- Commands Layer (scan_commands.rs with channel support)
- Task Scheduler (Tokio Semaphore for concurrency control)

**Stack elements used:** tokio, surge-ping, socket2, rayon, serde/serde_json

**Research flag:** STANDARD PATTERNS - Network scanning is well-documented, Nmap patterns apply

---

### Phase 3: Shadow AI Detection
**Rationale:** Core differentiator builds on working scanning engine. Port detection is straightforward (known ports: 11434 Ollama, 1234 LM Studio, 8888 Jupyter). Service fingerprinting (banner grabbing) can be deferred to v1.2 - port-based detection provides value despite some false positives.

**Delivers:**
- Shadow AI service catalog (local LLMs + dev tools)
- Port-based detection for known AI services
- Results flagging (Shadow AI column in table)
- Detection heuristics (port patterns, common configurations)

**Features from FEATURES.md:**
- Shadow AI detection (local LLM) - HIGH value differentiator
- Shadow AI detection (dev tools) - completes AI ecosystem coverage

**Defers to v1.2:**
- Service fingerprinting (banner grabbing) - reduces false positives but complex
- Shadow AI risk scoring - contextualizes findings, v1.3 feature

**Stack elements used:** Existing socket2 for TCP connections, pattern matching logic

**Research flag:** MAY NEED RESEARCH - AI tooling landscape evolves rapidly, signature catalog may need updating. Monitor for new services during implementation.

---

### Phase 4: UI & Real-Time Updates
**Rationale:** With backend working, focus shifts to UX. Real-time updates and dark aesthetic differentiate from enterprise tools. Virtualization critical for performance with large result sets (table stakes from research).

**Delivers:**
- Results table with TanStack Table (virtualized, sortable, filterable)
- Live scanning updates (stream results as they arrive)
- Scan progress indicator (N of M hosts, time remaining)
- Dark hacker aesthetic (Tailwind dark mode, terminal-inspired)
- Scan control UI (start, stop, cancel, configuration)
- Filter bar (show only shadow AI, filter by port/status)

**Features from FEATURES.md:**
- Results table view - table stakes
- Scan progress indicator - table stakes
- Live scanning updates - differentiator, valued by users
- Dark hacker aesthetic UI - differentiator, appeals to power users

**Addresses pitfalls:**
- Large result set slowdown - virtualized table rendering
- Event batching for performance - batch updates every 100ms

**Stack elements used:** TanStack Table 8, Tailwind CSS 4, Framer Motion 11, lucide-react

**Research flag:** STANDARD PATTERNS - React data grid patterns well-documented

---

### Phase 5: Polish & Distribution
**Rationale:** Product is functional after Phase 4. This phase addresses Windows distribution challenges, antivirus whitelisting, and quality-of-life features that don't block initial validation.

**Delivers:**
- Export results (CSV, JSON formats)
- Save/load scan configurations
- Windows installer (.msi with WiX or .exe with NSIS)
- WebView2 bundling strategy
- Code signing with EV certificate
- Microsoft Defender whitelisting submission
- AV exception documentation
- Performance throttling UI controls (scan speed adjustment)
- Error state improvements (distinguish timeout/refused/filtered)

**Features from FEATURES.md:**
- Save scan results - table stakes, deferred from v1 to v1.1
- Performance throttling - table stakes, users add when needed

**Addresses pitfalls:**
- Windows Defender false positives (Pitfall 7) - submit for whitelisting, document exceptions
- AV blocking execution - clear installation guide, expected behavior docs

**Stack elements used:** Tauri bundler, tauri-plugin-dialog

**Research flag:** MAY NEED RESEARCH - Windows installer/distribution specifics may need deeper investigation (WiX vs NSIS tradeoffs, WebView2 bundling strategies)

---

### Phase Ordering Rationale

**Why Phase 1 first:** Windows platform constraints are non-negotiable. Npcap dependency, firewall detection, privilege handling create architectural requirements that affect all subsequent work. Discovering raw socket restrictions in Phase 2 would force expensive rewrites.

**Why Phase 2 before Phase 3:** Shadow AI detection requires working port scanner. Building AI detection first would duplicate scanning logic or create tight coupling. Bounded concurrency patterns must be correct from the start - socket exhaustion isn't an optimization problem, it's a correctness problem.

**Why Phase 3 before Phase 4:** Backend-first development enables testing scanning logic independently. UI built on stable backend is faster than UI and backend simultaneously. Channel-based streaming architecture from Phase 2 enables Phase 4's real-time updates without rework.

**Why Phase 5 last:** Distribution and polish don't block validation. Users can test .exe without installer. AV whitelisting takes weeks - submit early in Phase 5 but don't block development. Export to CSV can be deferred - screenshots work for initial user feedback.

**Dependency chain:**
```
Phase 1 (Windows integration)
  └─> Phase 2 (Scanning engine with correct concurrency)
       └─> Phase 3 (AI detection using engine)
            └─> Phase 4 (UI consuming backend via channels)
                 └─> Phase 5 (Distribution polish)
```

### Research Flags

**Phases likely needing deeper research during planning:**
- **Phase 5:** Windows distribution specifics (WiX vs NSIS installer tradeoffs, WebView2 bundling strategies, code signing certificate acquisition) - moderate complexity, official Tauri docs cover basics but production distribution has nuances
- **Phase 3:** Shadow AI service catalog updates (new LLM tools emerge monthly in 2026, ports/endpoints change) - not deep research but ongoing monitoring

**Phases with standard patterns (skip research-phase):**
- **Phase 1:** Tauri project setup, state management, IPC patterns - official Tauri v2 docs are comprehensive
- **Phase 2:** Network scanning with tokio async - well-documented patterns, Nmap book applies
- **Phase 4:** React data grid, real-time updates - TanStack Table docs comprehensive, standard React patterns

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All technologies verified on official docs/crates.io. Tauri 2.10, tokio 1.49, surge-ping, socket2, TanStack Table v8, Tailwind CSS 4 all confirmed current and compatible. |
| Features | MEDIUM | Table stakes features confirmed via competitor analysis (Nmap, Angry IP Scanner). Shadow AI detection validated by market research (1,100+ exposed Ollama instances, $58M Witness AI funding) but rapid evolution risk. |
| Architecture | HIGH | Tauri IPC patterns, tokio async, React hooks all standard and documented. Specific patterns (channels for streaming, semaphore for concurrency) verified in official docs. |
| Pitfalls | MEDIUM-HIGH | Windows raw socket restrictions, thread exhaustion, Tauri memory leaks all documented in official sources. Specific thresholds (93 threads, 100ms event batching) from community experience and Nmap docs. |

**Overall confidence:** HIGH for technical implementation, MEDIUM for market assumptions (shadow AI detection value proposition needs user validation)

### Gaps to Address

**Shadow AI service catalog maintenance:** AI tooling landscape evolves rapidly. New local LLM frameworks emerge monthly. Current catalog (Ollama, LM Studio, vLLM, LocalAI, Jupyter, TensorBoard) is 2026-current but will need updates.
- **Handling:** Build extensible detection system with JSON configuration for easy signature updates. Monitor AI dev tool releases during development. Plan for quarterly catalog updates post-launch.

**Service fingerprinting complexity:** Port-based detection (v1) will have false positives (port 8080 = Ollama vs generic web server). Banner grabbing (v1.2) reduces this but requires protocol expertise for HTTP/SSH/custom protocols.
- **Handling:** Ship v1 with port-based detection and clear "potential AI service" language. Gather user feedback on false positive tolerance. Prioritize fingerprinting for high-traffic ports (8000, 8080) in v1.2.

**Windows Defender whitelisting timeline:** Submitting builds to Microsoft for analysis can take days to weeks. No guaranteed approval.
- **Handling:** Submit early in Phase 5. Prepare comprehensive AV exception documentation as fallback. Consider Microsoft Store distribution (pre-vetted) for v2.

**Npcap licensing for distribution:** Npcap has dual licensing (free for open source, commercial license required for closed-source distribution). Scanner business model unclear.
- **Handling:** Clarify licensing during Phase 1. Either: (a) open-source the scanner to use free Npcap license, (b) obtain commercial Npcap license, or (c) link to Npcap download instead of bundling, require user installation.

**Multi-subnet scanning performance:** Research focused on /24 networks (254 hosts). Unclear how architecture scales to /16 (65k hosts) or multi-subnet enterprise scans.
- **Handling:** Design for /24 in Phase 2, add chunked scanning for larger ranges in Phase 3 if users request. Virtualized table (Phase 4) already handles large result sets.

## Sources

### Primary (HIGH confidence)
- **Tauri v2 Official Documentation** - Architecture, IPC, state management, channels, project structure, Windows installer
- **Tokio Official Documentation** - Async runtime, semaphore, spawn_blocking, performance patterns
- **Crates.io** - tokio 1.49, surge-ping 0.8, socket2 0.5, rayon 1.10, pnet_packet 0.35 (versions verified)
- **TanStack Table v8 Official Docs** - Sorting, filtering, virtualization capabilities
- **Nmap Official Documentation** - Performance tuning, timing parameters, scan techniques, port exhaustion
- **Microsoft Learn** - TCP/IP port exhaustion troubleshooting, Windows Firewall ICMP configuration, Defender false positives

### Secondary (MEDIUM confidence)
- **Cisco Security Blog** - Ollama exposed instance study (1,100+ found via Shodan)
- **Network Scanner Comparisons** - Nmap vs Angry IP Scanner vs Advanced IP Scanner feature analysis
- **Shadow AI Market Research** - Knostic, Teramind, TechCrunch (Witness AI $58M raise), Auvik
- **Local LLM Deployment Guides** - Ollama, LM Studio, vLLM, LocalAI port configurations and detection methods
- **Rust Network Scanner Examples** - GitHub rust-network-scanner, community implementations
- **Tauri Community Issues** - Memory leak reports (GitHub #12724, #9190), state management patterns

### Tertiary (LOW confidence, needs validation)
- **Shadow AI detection signatures** - Port/service catalog aggregated from multiple blog posts, may have inaccuracies
- **Thread pool limits** - 93 threads at 200ms response time is calculation, not empirically verified for this stack
- **AV detection patterns** - Antivirus behavior based on community reports, varies by vendor and version

---
*Research completed: 2026-02-08*
*Ready for roadmap: yes*
