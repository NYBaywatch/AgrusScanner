# Roadmap: Agrus Scanner

## Overview

Agrus Scanner delivers fast, multithreaded network reconnaissance with shadow AI detection through five focused phases. Starting with Windows-specific foundations (raw sockets, firewall handling), building concurrent scanning with bounded performance, adding AI service detection, polishing the hacker-aesthetic UI, and finishing with distribution and Windows integration. Each phase delivers verifiable capabilities that unblock the next.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Foundation & Windows Integration** - Tauri project, Windows raw socket handling, firewall detection
- [ ] **Phase 2: Core Scanning Engine** - Ping sweep, port scanning, bounded concurrency, real-time streaming
- [ ] **Phase 3: Shadow AI Detection** - Local LLM detection, AI dev tool identification, service labeling
- [ ] **Phase 4: UI & Real-Time Updates** - Results table, filters, dark theme, progress indicators
- [ ] **Phase 5: Polish & Configuration** - Settings persistence, export, platform polish

## Phase Details

### Phase 1: Foundation & Windows Integration
**Goal**: Establish Tauri project with Windows-specific requirements (Npcap, firewall, privileges) handled gracefully
**Depends on**: Nothing (first phase)
**Requirements**: PLAT-01, PLAT-02, PLAT-03, DISC-03
**Success Criteria** (what must be TRUE):
  1. Application launches on Windows as desktop executable
  2. Application detects Npcap availability and guides user if missing
  3. Application detects Windows Firewall ICMP blocking and warns user
  4. Application requests admin privileges for ICMP operations when needed
  5. Application auto-detects local subnet and pre-fills IP range field
**Plans**: 1 plan

Plans:
- [x] 01-01-PLAN.md -- Scaffold Tauri project, implement platform detection commands, create status UI with user guidance

### Phase 2: Core Scanning Engine
**Goal**: Fast, concurrent network scanning with ping sweep and port detection
**Depends on**: Phase 1
**Requirements**: DISC-01, DISC-02, DISC-04, DISC-05, PORT-01, PORT-02, PORT-03, PORT-04, PORT-05, PORT-06, PORT-07, UI-03, UI-07, CONF-01, CONF-02
**Success Criteria** (what must be TRUE):
  1. User can ping sweep /24 subnet and see live hosts with hostnames
  2. User can input custom IP ranges (CIDR or start-end format)
  3. User can run TCP port scans with Simple, Full, AI Ports, and Custom presets
  4. Open ports display with service names (HTTP, SSH, etc.)
  5. Scan progress shows host count, percentage, and streams results in real-time
  6. User can configure timeout and concurrency settings
  7. User can cancel running scans without crashes
  8. Scans complete /24 subnet in seconds using multithreaded execution
**Plans**: 5 plans

Plans:
- [x] 02-01-PLAN.md -- Scanner types, IP range parsing, port presets, service name mapping
- [ ] 02-02-PLAN.md -- Ping sweep engine with bounded concurrency and async reverse DNS
- [ ] 02-03-PLAN.md -- TCP port scanning engine with bounded concurrency and cancellation
- [x] 02-04-PLAN.md -- Tauri command layer with Channel streaming, cancellation state, progress batching
- [ ] 02-05-PLAN.md -- React scan UI (controls, progress bar, results table)

### Phase 3: Shadow AI Detection
**Goal**: Identify unauthorized AI services running on network
**Depends on**: Phase 2
**Requirements**: SHAI-01, SHAI-02, SHAI-03, SHAI-04, SHAI-05, CONF-03
**Success Criteria** (what must be TRUE):
  1. Scanner detects local LLM services (Ollama, LM Studio, vLLM, llama.cpp, LocalAI)
  2. Scanner detects AI dev tools (Jupyter, TensorBoard, MLflow, Weights & Biases)
  3. Scanner detects AI inference servers (HuggingFace TGI, Text Generation WebUI, ComfyUI, Stable Diffusion WebUI)
  4. Detected AI services show labeled with service name and category
  5. Shadow AI results are visually distinct in results table (highlighted/tagged)
  6. User can manage port presets including custom AI service signatures
**Plans**: TBD

Plans:
- [ ] 03-01: [To be planned]

### Phase 4: UI & Real-Time Updates
**Goal**: Professional hacker-aesthetic interface with responsive, filterable results
**Depends on**: Phase 3
**Requirements**: UI-01, UI-02, UI-04, UI-05, UI-06
**Success Criteria** (what must be TRUE):
  1. Results display in sortable table by IP, hostname, port count, status
  2. Results are filterable by status, port, service type, AI detection
  3. Dark theme with hacker/terminal aesthetic is default
  4. Light theme is available as toggle option
  5. UI scales properly on 4K monitors with resizable layout
  6. Table handles 1000+ results without performance degradation (virtualization)
**Plans**: TBD

Plans:
- [ ] 04-01: [To be planned]

### Phase 5: Polish & Configuration
**Goal**: Persistent configuration, export capabilities, and final Windows integration polish
**Depends on**: Phase 4
**Requirements**: CONF-04
**Success Criteria** (what must be TRUE):
  1. Settings persist between application sessions (timeout, concurrency, theme, presets)
  2. User can export scan results to CSV and JSON formats
  3. Application handles Windows Defender and antivirus concerns with documentation
  4. Single-file executable bundles all dependencies correctly
**Plans**: TBD

Plans:
- [ ] 05-01: [To be planned]

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation & Windows Integration | 1/1 | Complete | 2026-02-09 |
| 2. Core Scanning Engine | 1/5 | In progress | - |
| 3. Shadow AI Detection | 0/TBD | Not started | - |
| 4. UI & Real-Time Updates | 0/TBD | Not started | - |
| 5. Polish & Configuration | 0/TBD | Not started | - |

---
*Last updated: 2026-02-10 (Phase 2 Plan 1 complete)*
*Roadmap created with quick depth (3-5 phases, aggressive grouping)*
