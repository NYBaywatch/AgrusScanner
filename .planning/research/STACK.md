# Technology Stack

**Project:** Network Scanner with Shadow AI Detection
**Researched:** 2026-02-08
**Confidence:** HIGH

## Recommended Stack

### Core Framework

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Tauri | 2.10.2 | Desktop application framework | Cross-platform Rust backend + web frontend. Smaller bundle size than Electron, native performance, strong security model with permissions system. Latest stable with plugin architecture for extensibility. |
| Rust | 1.80.0+ | Backend systems language | Memory safety without GC, excellent concurrency primitives, zero-cost abstractions. Required for network operations (raw sockets) and high-performance scanning. |
| React | 18.x | Frontend UI framework | Component-based architecture, excellent ecosystem, TypeScript support. Pairs naturally with Vite for fast development. |
| TypeScript | 5.x | Type-safe JavaScript | Catches errors at compile time, better IDE support, essential for maintainable React applications. |
| Vite | 5.x | Frontend build tool | 40x faster builds than CRA, native ESM support, lightning-fast HMR. Standard choice for modern React apps in 2026. |

### Rust Backend Crates

| Crate | Version | Purpose | Why Recommended |
|-------|---------|---------|-----------------|
| tokio | 1.49.0 (LTS: 1.47.x) | Async runtime | Industry standard async runtime. Work-stealing scheduler, efficient for I/O-bound network operations. Use 1.47.x for LTS support until Sep 2026. |
| surge-ping | 0.8.x | ICMP ping implementation | Async ping based on tokio, uses pnet_packet for ICMP parsing. Cleaner API than raw socket manipulation. Windows-compatible. |
| socket2 | 0.5.x | Low-level socket operations | Raw socket access for port scanning. Cross-platform (Windows/Unix). Required for custom network packet creation. |
| pnet_packet | 0.35.x | Network packet parsing | Parse/build network packets (Ethernet, IP, ICMP, TCP, UDP). Used by surge-ping. Essential for custom protocol detection. |
| rayon | 1.10.x | Data parallelism | Work-stealing thread pool for CPU-bound operations. Convert iter() to par_iter() for parallel scanning. Data-race freedom guaranteed. |
| serde | 1.0.x | Serialization framework | De/serialize Rust structs to JSON. Required for Tauri IPC. Industry standard with derive macros. |
| serde_json | 1.0.x | JSON serialization | Fast JSON processing (600-900 MB/s). Tauri frontend-backend communication. |
| tauri-plugin-window | 2.x | Window management | Access window controls, resize, minimize. Required for 4K-ready resizable windows. |
| tauri-plugin-dialog | 2.x | Native dialogs | File pickers, message boxes. Better UX than web dialogs. |

### React Frontend Libraries

| Library | Version | Purpose | Why Recommended |
|---------|---------|---------|-----------------|
| TanStack Table | 8.x | Data grid with sorting/filtering | Headless UI, handles 100k+ rows client-side. Built-in sorting, filtering, grouping. Flexible for custom styling. |
| xterm-for-react | 2.x | Terminal emulator component | React wrapper for xterm.js. Enables terminal-style output for scan results. Supports ANSI escape codes for colors. |
| Tailwind CSS | 4.x | Utility-first CSS framework | Fast styling, dark mode built-in (selector strategy), small bundle size with tree-shaking. CSS-based config in v4. |
| Framer Motion | 11.x | Animation library | Smooth animations for hacker aesthetic. Performance-optimized, declarative API. |
| lucide-react | 0.x | Icon library | Modern icon set, tree-shakeable, consistent with terminal aesthetic. |

### Development Tools

| Tool | Purpose | Configuration Notes |
|------|---------|---------------------|
| cargo | Rust package manager | Use workspace feature for multi-crate project if needed. |
| rustfmt | Rust code formatter | Standard formatting, consistent across team. |
| clippy | Rust linter | Catches common mistakes, suggests idiomatic patterns. |
| @tauri-apps/cli | Tauri CLI | Build, dev server, bundling. Handles Vite + Rust orchestration. |
| eslint | JavaScript/TypeScript linter | Enforce React best practices, catch type errors. |
| prettier | Code formatter | Consistent formatting for TS/TSX/CSS. |

## Installation

### Rust Dependencies (Cargo.toml)

```toml
[dependencies]
tauri = { version = "2.10", features = ["protocol-asset"] }
tokio = { version = "1.49", features = ["full"] }
surge-ping = "0.8"
socket2 = { version = "0.5", features = ["all"] }
pnet_packet = "0.35"
rayon = "1.10"
serde = { version = "1.0", features = ["derive"] }
serde_json = "1.0"

[target.'cfg(windows)'.dependencies]
windows-sys = { version = "0.59", features = ["Win32_NetworkManagement_IpHelper"] }

[build-dependencies]
tauri-build = { version = "2.10", features = [] }
```

### Frontend Dependencies (package.json)

```bash
# Core
npm install react@^18.3.0 react-dom@^18.3.0

# Build tools
npm install -D vite@^5.4.0 @vitejs/plugin-react@^4.3.0

# Tauri integration
npm install -D @tauri-apps/cli@^2.10.0
npm install @tauri-apps/api@^2.10.0

# UI libraries
npm install @tanstack/react-table@^8.20.0
npm install xterm-for-react@^2.0.2 xterm@^5.3.0
npm install tailwindcss@^4.0.0 postcss@^8.4.0 autoprefixer@^10.4.0
npm install framer-motion@^11.11.0
npm install lucide-react@^0.460.0

# TypeScript
npm install -D typescript@^5.6.0 @types/react@^18.3.0 @types/react-dom@^18.3.0

# Dev tools
npm install -D eslint@^9.17.0 prettier@^3.4.0
```

## Alternatives Considered

| Category | Recommended | Alternative | Why Not Alternative |
|----------|-------------|-------------|---------------------|
| Desktop Framework | Tauri 2.10 | Electron | Tauri: smaller binaries (3-5MB vs 100MB+), lower memory usage, Rust security model, native permissions system. Electron bloat unnecessary for this use case. |
| Async Runtime | tokio 1.49 | async-std | Tokio has larger ecosystem, better maintained, work-stealing scheduler optimized for I/O workloads. Industry standard. |
| Ping Library | surge-ping 0.8 | fastping-rs | surge-ping actively maintained, better async/await support, uses modern pnet_packet. fastping-rs less active. |
| Table Library | TanStack Table v8 | AG Grid React | TanStack is headless (full styling control), free, handles 100k+ rows. AG Grid commercial license required for features, opinionated styling conflicts with hacker aesthetic. |
| Terminal UI | xterm-for-react | react-terminal-ui | xterm.js is industry standard (VSCode uses it), full terminal emulation, ANSI support. react-terminal-ui more limited, simpler command-based interface. |
| Styling | Tailwind CSS 4.x | styled-components | Tailwind faster development, smaller bundle, built-in dark mode, no runtime cost. styled-components adds runtime overhead. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| pnet (full library) | Heavy dependency, includes packet capture we don't need. Windows requires WinPcap/Npcap driver installation. | pnet_packet only (packet parsing without capture). Build packets manually with socket2. |
| Create React App (CRA) | Deprecated, slow builds, webpack bloat. No longer maintained. | Vite (40x faster, modern ESM, active development). |
| reqwest for HTTP scanning | Adds unnecessary async HTTP client when we need raw TCP sockets for port scanning. | socket2 + tokio for TCP connections, manual HTTP detection. |
| nmap integration | External process dependency, licensing complexity, binary size increase. | Build scanning logic in Rust (we control threading, results format, no external deps). |
| Electron | 100MB+ bundle size, slow startup, high memory usage. | Tauri (3-5MB bundle, instant startup, native performance). |

## Stack Patterns by Use Case

**For Basic Ping Scan (ICMP):**
- Use `surge-ping` with tokio runtime
- Requires elevated privileges (admin on Windows)
- Handle Windows firewall prompts

**For Port Scanning (TCP SYN/Connect):**
- Use `socket2` for raw socket creation
- Use `tokio::time::timeout` for connection timeouts
- Use `rayon::par_iter` for parallelizing across target list (not per-target ports—keep async)

**For Service Detection (HTTP, SSH, etc.):**
- Connect via socket2, send protocol-specific probe
- Parse response with pnet_packet or manual parsing
- Store signatures in JSON (serde_json)

**For AI Service Detection:**
- HTTP endpoints: Connect, check headers/paths (`/v1/models`, `/health`)
- Local services: Port scan known ports (11434 Ollama, 1234 LM Studio, 8000 vLLM)
- Match against signature database (JSON config)

**For Frontend Data Display:**
- TanStack Table for large result sets (1000+ hosts)
- xterm-for-react for live scan output (streaming logs)
- Tailwind dark theme classes for hacker aesthetic
- Framer Motion for subtle scan progress animations

## Windows-Specific Considerations

**Bundling to Single EXE:**
- Tauri produces `.msi` installer by default (WiX Toolset)
- NSIS option for `-setup.exe` (smaller, no admin required)
- Portable ZIP not officially supported but possible
- WebView2 must be installed (Tauri can bundle installer)

**Raw Socket Permissions:**
- ICMP ping requires Administrator privileges
- TCP connect scan works without admin (use this by default)
- Prompt user to "Run as Administrator" for ICMP features

**Firewall Considerations:**
- Windows Defender Firewall may block raw sockets
- Application must request network permissions in manifest
- Tauri handles this via capabilities system

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| tauri 2.10.2 | tokio 1.x | Tauri's async runtime uses tokio internally. |
| surge-ping 0.8.x | tokio 1.x, pnet_packet 0.35.x | Requires matching tokio version. Uses pnet_packet for ICMP. |
| socket2 0.5.x | tokio 1.x | tokio can wrap socket2 sockets with `TcpSocket::from_raw_fd`. |
| TanStack Table v8 | React 18.x | Requires React 18+ for concurrent features. |
| xterm-for-react 2.x | React 18.x, xterm 5.x | Peer dependency on xterm. |
| Tailwind CSS 4.x | Vite 5.x | PostCSS plugin, works with Vite out of box. |

## Confidence Assessment

| Technology Area | Confidence | Source |
|-----------------|------------|--------|
| Tauri 2.10.2 | HIGH | Official docs, verified version on docs.rs |
| Tokio 1.49/1.47 LTS | HIGH | Official docs, crates.io verified |
| surge-ping | MEDIUM | Crates.io, GitHub repo (active but smaller community) |
| socket2 | HIGH | Rust-lang maintained, official repo |
| pnet_packet | MEDIUM | Active project, cross-platform verified, less recent updates |
| rayon | HIGH | Mature library, Red Hat blog verification, wide adoption |
| TanStack Table v8 | HIGH | Official docs, verified Jan 2026 guides |
| xterm-for-react | MEDIUM | Multiple React wrappers exist, xterm.js itself is HIGH (industry standard) |
| Tailwind CSS 4.x | HIGH | Official docs, v4 recently released with CSS-based config |
| Vite 5.x | HIGH | Official docs, verified as standard for React in 2026 |

## Sources

### Official Documentation (HIGH Confidence)
- [Tauri 2.10.2 - docs.rs](https://docs.rs/crate/tauri/latest) — Version verification
- [Tauri 2.0 Stable Release](https://v2.tauri.app/blog/tauri-20/) — Architecture, plugin system, mobile support
- [Tauri Windows Installer](https://v2.tauri.app/distribute/windows-installer/) — Bundling strategies
- [Tokio Official Docs](https://tokio.rs/) — Async runtime features
- [TanStack Table v8 Docs](https://tanstack.com/table/v8/docs/guide/sorting) — Sorting/filtering capabilities
- [Tailwind CSS Dark Mode](https://tailwindcss.com/docs/dark-mode) — Selector strategy, v4 features
- [Vite Official Guide](https://vite.dev/guide/) — Build tool features, HMR
- [serde.rs](https://serde.rs/) — Serialization framework

### Crates.io / Package Registries (HIGH Confidence)
- [tokio - crates.io](https://crates.io/crates/tokio) — Version 1.49.0, LTS info
- [surge-ping - crates.io](https://crates.io/crates/surge-ping) — ICMP async library
- [socket2 GitHub](https://github.com/rust-lang/socket2) — Raw socket operations
- [rayon GitHub](https://github.com/rayon-rs/rayon) — Data parallelism
- [pnet_packet - crates.io](https://crates.io/crates/pnet_packet) — Packet parsing

### Community Resources & Guides (MEDIUM Confidence)
- [Vite + TypeScript React 2026 Guide](https://medium.com/@mernstackdevbykevin/vite-typescript-2026-frontend-setup-in-the-fast-lane-822c28a6c3f0) — Current best practices
- [React xterm.js Integration](https://www.qovery.com/blog/react-xtermjs-a-react-library-to-build-terminals) — Terminal component patterns
- [15 Best React UI Libraries for 2026](https://www.builder.io/blog/react-component-libraries-2026) — Ecosystem landscape
- [RustScan Guide 2026](https://tabcode.net/threads/799/) — Network scanner patterns
- [TanStack Table v8 Complete Demo](https://dev.to/abhirup99/tanstack-table-v8-complete-interactive-data-grid-demo-1eo0) — Implementation examples

---
*Stack research for: Network Scanner with Shadow AI Detection*
*Researched: 2026-02-08*
*Mode: Ecosystem (greenfield project)*
