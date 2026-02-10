# Agrus Scanner

## What This Is

A modern network scanner built for security professionals who need fast, comprehensive network reconnaissance and shadow AI detection. It combines traditional scanning (ping, port) with specialized detection of unauthorized AI services running on corporate networks. Built with a hacker aesthetic UI that looks and feels like it belongs in 2026, not 2005.

## Core Value

Fast, multithreaded network scanning with shadow AI detection — the one tool that finds everything on your network, including the AI services nobody told IT about.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Ping scan across /24 subnets with configurable range
- [ ] Port scan — simple (common ports preset), full (all 65535), and custom port lists
- [ ] Shadow AI detection — identify known AI endpoints, local LLMs, and AI dev tools on the network
- [ ] Multithreaded scanning for speed
- [ ] Hacker aesthetic dark theme (default) with light theme option
- [ ] 4K-ready, resizable interface
- [ ] Sortable, filterable table/list results view
- [ ] Single-file Windows executable
- [ ] AI-specific port presets (Ollama 11434, LM Studio 1234, Jupyter 8888, vLLM 8000, etc.)

### Out of Scope

- Export/reporting (CSV, JSON, PDF) — defer to v2
- Scan history / comparison over time — defer to v2
- Cross-platform support (Linux, macOS) — Windows-first
- Real-time traffic monitoring / packet capture — not a network monitor
- Vulnerability exploitation — detection only, not offensive

## Context

- Existing network scanners (Advanced IP Scanner, Angry IP Scanner, nmap GUI wrappers) suffer from outdated UIs, poor parallelization, and clunky workflows
- Shadow AI is a growing security concern — employees spinning up Ollama, using ChatGPT via API, running Jupyter notebooks — often invisible to IT
- Primary scan target is /24 networks (~254 hosts) but should support scaling via settings (memory allocation, thread count)
- Shadow AI detection targets: OpenAI/Anthropic/HuggingFace API traffic patterns, Ollama (11434), LM Studio (1234), vLLM (8000), Jupyter (8888), TensorBoard (6006), MLflow (5000), and similar services

## Constraints

- **Platform**: Windows — compiled to single .exe via Tauri bundler
- **Stack**: Tauri (Rust backend) + React (TypeScript frontend)
- **Performance**: Must scan a /24 subnet in seconds, not minutes
- **UI**: Designed for 4K monitors — no tiny fonts or cramped layouts
- **Distribution**: Single file, no installer dependencies

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Tauri + React over Electron | Smaller exe (~5-10MB vs 100MB+), Rust backend for fast multithreaded scanning | — Pending |
| Hacker aesthetic over clean enterprise | User preference — terminal-inspired, monospace, cyberpunk feel | — Pending |
| Table/list results over dashboard | Scan results are data-dense, tables with sort/filter serve this better | — Pending |
| /24 default with scaling options | Covers most use cases without overwhelming resources | — Pending |

---
*Last updated: 2026-02-08 after initialization*
