# Requirements: Agrus Scanner

**Defined:** 2026-02-09
**Core Value:** Fast, multithreaded network scanning with shadow AI detection — the one tool that finds everything on your network, including the AI services nobody told IT about.

## v1 Requirements

### Host Discovery

- [ ] **DISC-01**: User can ping sweep a /24 subnet to find live hosts
- [ ] **DISC-02**: User can input custom IP range (start-end or CIDR)
- [ ] **DISC-03**: App auto-detects local subnet and pre-fills range
- [ ] **DISC-04**: Live hosts show hostname via reverse DNS lookup
- [ ] **DISC-05**: Scan progress is visible with host count and percentage

### Port Scanning

- [ ] **PORT-01**: User can run TCP connect scan on discovered hosts
- [ ] **PORT-02**: User can select "Simple" preset (top 100 common ports)
- [ ] **PORT-03**: User can select "Full" scan (all 65535 ports)
- [ ] **PORT-04**: User can select "AI Ports" preset (Ollama 11434, LM Studio 1234, Jupyter 8888, vLLM 8000, TensorBoard 6006, MLflow 5000, etc.)
- [ ] **PORT-05**: User can input custom port list or range
- [ ] **PORT-06**: Open ports display service name (well-known port mapping)
- [ ] **PORT-07**: Port scan runs multithreaded with configurable concurrency

### Shadow AI Detection

- [ ] **SHAI-01**: Scanner detects local LLM services (Ollama, LM Studio, vLLM, llama.cpp, LocalAI)
- [ ] **SHAI-02**: Scanner detects AI dev tools (Jupyter, TensorBoard, MLflow, Weights & Biases)
- [ ] **SHAI-03**: Scanner detects AI inference servers (HuggingFace TGI, Text Generation WebUI, ComfyUI, Stable Diffusion WebUI)
- [ ] **SHAI-04**: Detected AI services are labeled with service name and category
- [ ] **SHAI-05**: Shadow AI results are visually distinct from regular port results (highlighted/tagged)

### Results & UI

- [ ] **UI-01**: Results display in sortable table (by IP, hostname, port count, status)
- [ ] **UI-02**: Results are filterable (by status, port, service type, AI detection)
- [ ] **UI-03**: Results stream in real-time as hosts/ports are discovered
- [ ] **UI-04**: Dark theme with hacker/terminal aesthetic is the default
- [ ] **UI-05**: Light theme is available as an option
- [ ] **UI-06**: UI is 4K-ready with proper scaling and resizable layout
- [ ] **UI-07**: User can cancel a running scan

### Configuration

- [ ] **CONF-01**: User can configure scan timeout per host
- [ ] **CONF-02**: User can configure thread/concurrency count
- [ ] **CONF-03**: User can manage port presets (view, create custom)
- [ ] **CONF-04**: Settings persist between sessions

### Platform

- [ ] **PLAT-01**: App runs on Windows as a desktop application
- [ ] **PLAT-02**: App detects Windows Firewall blocking and guides user
- [ ] **PLAT-03**: App handles admin privilege requirements gracefully (ICMP needs admin)

## v2 Requirements

### Export & Reporting

- **EXPR-01**: User can export results to CSV
- **EXPR-02**: User can export results to JSON
- **EXPR-03**: User can generate PDF report

### Scan History

- **HIST-01**: Scans are saved with timestamp
- **HIST-02**: User can compare two scans to see changes
- **HIST-03**: User can view scan history timeline

### Advanced Detection

- **ADVD-01**: Service fingerprinting via banner grabbing (reduce false positives)
- **ADVD-02**: HTTP header analysis for AI service identification
- **ADVD-03**: Risk scoring for detected shadow AI services

## Out of Scope

| Feature | Reason |
|---------|--------|
| Traffic monitoring / packet capture | Not a network monitor — scanner only |
| Vulnerability exploitation | Detection only, not offensive tooling |
| Cross-platform (Linux/macOS) | Windows-first, defer platform expansion |
| Real-time continuous monitoring | Point-in-time scan tool, not an agent |
| Auto-update mechanism | Single exe distribution, manual updates for v1 |
| Cloud/SaaS AI traffic detection | Requires deep packet inspection, out of scope |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| DISC-01 | Phase 2 | Pending |
| DISC-02 | Phase 2 | Pending |
| DISC-03 | Phase 1 | Pending |
| DISC-04 | Phase 2 | Pending |
| DISC-05 | Phase 2 | Pending |
| PORT-01 | Phase 2 | Pending |
| PORT-02 | Phase 2 | Pending |
| PORT-03 | Phase 2 | Pending |
| PORT-04 | Phase 2 | Pending |
| PORT-05 | Phase 2 | Pending |
| PORT-06 | Phase 2 | Pending |
| PORT-07 | Phase 2 | Pending |
| SHAI-01 | Phase 3 | Pending |
| SHAI-02 | Phase 3 | Pending |
| SHAI-03 | Phase 3 | Pending |
| SHAI-04 | Phase 3 | Pending |
| SHAI-05 | Phase 3 | Pending |
| UI-01 | Phase 4 | Pending |
| UI-02 | Phase 4 | Pending |
| UI-03 | Phase 2 | Pending |
| UI-04 | Phase 4 | Pending |
| UI-05 | Phase 4 | Pending |
| UI-06 | Phase 4 | Pending |
| UI-07 | Phase 2 | Pending |
| CONF-01 | Phase 2 | Pending |
| CONF-02 | Phase 2 | Pending |
| CONF-03 | Phase 3 | Pending |
| CONF-04 | Phase 5 | Pending |
| PLAT-01 | Phase 1 | Pending |
| PLAT-02 | Phase 1 | Pending |
| PLAT-03 | Phase 1 | Pending |

**Coverage:**
- v1 requirements: 31 total
- Mapped to phases: 31/31 (100%)
- Unmapped: 0

---
*Requirements defined: 2026-02-09*
*Last updated: 2026-02-09 after roadmap creation*
