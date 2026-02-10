# Feature Landscape

**Domain:** Network Scanner with Shadow AI Detection
**Researched:** 2026-02-08
**Confidence:** MEDIUM

## Table Stakes

Features users expect. Missing = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Host discovery (ping scan) | Core network scanner function - users expect to see what's alive on network | LOW | ICMP echo requests to /24 range, standard in all scanners |
| Port scanning (common ports) | Users need to identify what services are running | MEDIUM | TCP connect/SYN scanning for well-known ports (80, 443, 22, etc.) |
| IP address display | Basic network inventory requirement | LOW | Show IP, hostname, MAC address in results |
| Scan progress indicator | Users need feedback during multi-minute scans | LOW | Real-time progress bar with percentage/count of hosts scanned |
| Results table view | Standard presentation format for network data | LOW | Sortable columns (IP, hostname, ports, status) |
| Save scan results | Users need to preserve findings for comparison/reporting | MEDIUM | At minimum one format (CSV, JSON, or XML) |
| Port range customization | Power users need flexible port specifications | MEDIUM | Custom ranges (1-1000, 80,443,8080) beyond defaults |
| Scan cancellation | Users need ability to stop long-running scans | LOW | Graceful shutdown of scan threads |
| Performance throttling | Prevent network flooding and avoid IPS/rate limiting | MEDIUM | Configurable scan speed/thread count to manage impact |

## Differentiators

Features that set product apart. Not expected, but valued.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Shadow AI detection (local LLM) | Novel capability - detects unauthorized AI infrastructure on corporate networks | HIGH | Detect Ollama (11434), LM Studio (1234), vLLM, LocalAI via port scanning + banner grabbing |
| Shadow AI detection (dev tools) | Catches AI development activity others miss | MEDIUM | Detect Jupyter (8888), TensorBoard (6006), MLflow (5000) - indicates ML workloads |
| Shadow AI detection (cloud API usage) | Identifies cloud AI service consumption patterns | MEDIUM | Pattern matching for OpenAI, Anthropic, Google Gemini API endpoints via network traffic inspection (Note: v1 may be limited to port-based detection) |
| Shadow AI detection (HuggingFace TGI) | Detects production-grade LLM deployments | MEDIUM | Text Generation Inference endpoints, FastAPI-based LLM services |
| Multithreaded scanning | 10-100x faster than single-threaded scanners | MEDIUM | Rust async/tokio for concurrent host scanning - critical for /24 networks (254 hosts) |
| Dark hacker aesthetic UI | Appeals to power users, stands out from enterprise tools | LOW | Terminal-inspired interface, monospace fonts, matrix-green/cyberpunk color scheme |
| Service fingerprinting | Identifies what's actually running on open ports | HIGH | Banner grabbing to distinguish Ollama vs generic HTTP server on non-standard ports |
| Live scanning updates | See results populate in real-time as scan progresses | MEDIUM | Streaming results to UI as each host completes vs waiting for full scan |
| Shadow AI risk scoring | Contextualizes findings with severity/risk levels | MEDIUM | Flag high-risk services (public-facing Ollama, unprotected Jupyter) vs low-risk |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Vulnerability scanning | Scope creep into Nessus/Qualys territory - different product category | Focus on discovery and shadow AI detection, not CVE matching |
| Authenticated credential scanning | Adds massive complexity, legal/compliance risk, storage security concerns | Surface-level port/service detection only |
| Continuous monitoring/agent deployment | Transforms into enterprise monitoring platform (PRTG/Auvik) - different use case | Single-shot scans that users run on demand |
| Cloud-based scanning | Requires backend infrastructure, SaaS complexity, data privacy issues | Standalone Windows exe with local-only execution |
| Real-time alerting/notifications | Implies continuous monitoring which contradicts single-shot scan model | Export results for integration with user's own alerting systems |
| Compliance reporting (PCI, HIPAC, SOC2) | Enterprise feature requiring extensive templates, expertise, certification | Provide raw data that compliance tools can consume |
| Network packet capture (PCAP) | Deep packet inspection is Wireshark's domain, major complexity | Rely on banner grabbing and port probing only |
| Exploit modules | Crosses line into offensive security (Metasploit territory), huge legal risk | Discovery only - never attempt to exploit findings |

## Feature Dependencies

```
Host Discovery (Ping Scan)
    └──required for──> Port Scanning
                          └──required for──> Service Fingerprinting
                                                └──enables──> Shadow AI Detection

Multithreaded Scanning
    └──enhances──> All Scan Types (performance multiplier)

Results Table View
    └──required for──> Save Results
    └──required for──> Live Scanning Updates

Performance Throttling
    └──prevents──> Rate Limiting Issues (IPS blocking)
    └──prevents──> Network Flooding

Shadow AI Risk Scoring
    └──requires──> Shadow AI Detection
    └──requires──> Service Fingerprinting
```

### Dependency Notes

- **Port Scanning requires Host Discovery:** No point scanning ports on hosts that don't exist/respond
- **Service Fingerprinting requires Port Scanning:** Need to know which ports are open before attempting banner grabs
- **Shadow AI Detection requires Service Fingerprinting:** Distinguishing Ollama from generic HTTP requires banner analysis, not just port 11434 being open
- **Live Updates enhances UX:** Users see results immediately vs staring at progress bar, especially valuable for slow scans
- **Performance Throttling prevents failures:** Enterprise networks often have IPS that block rapid scanning; throttling is critical for success

## MVP Recommendation

### Launch With (v1)

Minimum viable product to validate shadow AI detection value proposition.

- [x] Host discovery (ping scan) - Table stakes, foundation for all other features
- [x] Port scanning (simple mode: top 100 ports) - Table stakes, enables AI detection
- [x] Port scanning (custom mode) - Differentiator prep, allows targeting known AI ports
- [x] Results table view with sorting - Table stakes, users need to see findings
- [x] Scan progress indicator - Table stakes, scans take 2-10 minutes on /24 networks
- [x] Shadow AI detection (local LLM ports) - Core differentiator, validates market need
- [x] Shadow AI detection (dev tools) - Core differentiator, completes AI ecosystem coverage
- [x] Multithreaded scanning - Differentiator, required for acceptable scan times on 254-host networks
- [x] Dark hacker aesthetic UI - Differentiator, positions product distinctly
- [x] Scan cancellation - Table stakes, usability requirement

### Defer to v1.x (Post-Launch)

Features to add once core is validated and user feedback collected.

- [ ] Save/export results (CSV, JSON, XML) - **v1.1** - Table stakes, but users can screenshot/copy for v1 validation
- [ ] Load previous scan results - **v1.2** - Enables comparison workflows once save exists
- [ ] Service fingerprinting (banner grabbing) - **v1.2** - Reduces false positives, requires protocol expertise
- [ ] Live scanning updates - **v1.3** - UX enhancement, not blocking for validation
- [ ] Port scanning (full mode: all 65535) - **v1.1** - Power user feature, simple/custom covers 90% of use cases
- [ ] Shadow AI risk scoring - **v1.3** - Value-add once base detection proven useful
- [ ] Performance throttling controls - **v1.2** - Add when users report IPS blocking issues
- [ ] Shadow AI detection (cloud API patterns) - **v2.0** - Requires network traffic inspection beyond port scanning

### Explicitly NOT Building (Ever)

Scope boundaries to prevent feature creep.

- [ ] Vulnerability scanning - Different product category
- [ ] Authenticated scanning - Legal/security complexity
- [ ] Continuous monitoring - Different use case/architecture
- [ ] Exploit modules - Legal risk, out of scope
- [ ] Compliance reporting - Enterprise complexity

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Host discovery | HIGH | LOW | P1 |
| Port scan (simple) | HIGH | MEDIUM | P1 |
| Port scan (custom) | HIGH | MEDIUM | P1 |
| Results table view | HIGH | LOW | P1 |
| Shadow AI (LLM ports) | HIGH | MEDIUM | P1 |
| Shadow AI (dev tools) | HIGH | LOW | P1 |
| Multithreading | HIGH | MEDIUM | P1 |
| Scan progress | HIGH | LOW | P1 |
| Dark aesthetic UI | MEDIUM | LOW | P1 |
| Scan cancellation | MEDIUM | LOW | P1 |
| Save results | HIGH | MEDIUM | P2 |
| Load results | MEDIUM | LOW | P2 |
| Service fingerprinting | MEDIUM | HIGH | P2 |
| Live updates | MEDIUM | MEDIUM | P2 |
| Port scan (full) | MEDIUM | LOW | P2 |
| Performance throttling | MEDIUM | MEDIUM | P2 |
| Shadow AI risk scoring | MEDIUM | MEDIUM | P3 |
| Cloud API detection | LOW | HIGH | P3 |

**Priority key:**
- **P1**: Must have for launch - validates core value proposition
- **P2**: Should have - add based on user feedback within first 3 releases
- **P3**: Nice to have - future consideration after product-market fit

## Shadow AI Detection Catalog (2026)

Services and patterns to detect for shadow AI discovery. Ordered by detection priority.

### Local LLM Deployments (High Priority)

| Service | Default Port | Detection Method | Risk Level |
|---------|--------------|------------------|------------|
| Ollama | 11434 | Port scan + banner "Ollama" | HIGH - 1,100+ exposed instances found in wild |
| LM Studio | 1234 | Port scan + path /v1/models | HIGH - Common in dev environments |
| Text Generation WebUI | 5000 | Port scan + banner check | MEDIUM |
| vLLM | 8000 (common) | Port scan + OpenAPI endpoint | MEDIUM |
| LocalAI | 8080 (common) | Port scan + /readiness endpoint | MEDIUM |
| OpenLLM (BentoML) | 3000 (default) | Port scan + /docs endpoint | MEDIUM |

### AI Development Tools (Medium Priority)

| Service | Default Port | Detection Method | Risk Level |
|---------|--------------|------------------|------------|
| Jupyter Notebook | 8888 | Port scan + banner "Jupyter" | HIGH - Common entry point for data exfil |
| TensorBoard | 6006 | Port scan + path /#scalars | MEDIUM |
| MLflow | 5000 | Port scan + /api/2.0/mlflow | MEDIUM |
| Weights & Biases (local) | 8080 | Port scan + /api/v1/system-info | LOW |

### Production LLM Serving (Medium Priority)

| Service | Default Port | Detection Method | Risk Level |
|---------|--------------|------------------|------------|
| HuggingFace TGI | 8080 (common) | Port scan + /health endpoint | MEDIUM |
| FastAPI (LLM apps) | 8000 (common) | Port scan + /docs OpenAPI | LOW - Too generic without fingerprinting |
| LangServe | 8000 (common) | Port scan + LangChain patterns | LOW |

### Cloud AI Service Proxies (Low Priority - v2)

| Service | Detection Method | Notes |
|---------|------------------|-------|
| OpenAI API (api.openai.com) | Network traffic analysis | Beyond v1 scope - requires packet inspection |
| Anthropic Claude (api.anthropic.com) | Network traffic analysis | Beyond v1 scope |
| Google Gemini (generativelanguage.googleapis.com) | Network traffic analysis | Beyond v1 scope |
| CLIProxyAPI (local proxy) | Port scan for proxy endpoints | Converts web AI to API - hard to fingerprint |

### Known Port Conflicts to Handle

- Port 5000: MLflow vs Text Generation WebUI vs Flask apps (common collision)
- Port 8000: vLLM vs FastAPI vs generic web apps (very common)
- Port 8080: LocalAI vs HuggingFace TGI vs standard web servers (extremely common)

**Detection Strategy:** Ports alone are insufficient. v1 can flag "potential AI service" based on port, v1.2+ should add service fingerprinting (banner grab, HTTP headers, API endpoints) to confirm.

## Competitor Feature Analysis

| Feature | Nmap (CLI) | Angry IP Scanner | Advanced IP Scanner | Our Approach |
|---------|------------|------------------|---------------------|--------------|
| Host discovery | Yes (flexible) | Yes (fast) | Yes (simple) | Yes - ICMP ping scan |
| Port scanning | Yes (comprehensive) | Yes (basic) | Yes (limited) | Yes - 3 modes (simple/full/custom) |
| Service detection | Yes (deep, scriptable) | Basic | No | v1.2+ - Banner grabbing |
| UI/UX | CLI only (Zenmap GUI exists) | Basic table | Windows-native | Dark hacker aesthetic |
| Speed | Configurable | Very fast | Fast | Fast - Rust multithreading |
| Shadow AI detection | No (manual scripting) | No | No | YES - Core differentiator |
| Export formats | XML, JSON, TXT | CSV, XML, TXT | XML, HTML | v1.1+ - CSV, JSON, XML |
| Platform | Cross-platform | Cross-platform | Windows only | Windows only (v1) |
| Multithreading | Yes | Yes | Yes | Yes - Tokio async |
| Learning curve | Steep | Gentle | Gentle | Medium - power user tool |

**Competitive positioning:** We sit between Angry IP Scanner (too basic, no AI detection) and Nmap (too complex, CLI-focused). Target users who need network visibility specifically for shadow AI governance, not general-purpose pentesting.

## User Workflow Scenarios

### Scenario 1: IT Admin Discovers Shadow AI

1. User opens scanner, enters subnet (192.168.1.0/24)
2. Selects "Simple Scan" (top 100 ports including AI services)
3. Clicks "Scan" - progress bar shows 45/254 hosts scanned
4. Results populate in real-time - table shows IP, hostname, open ports
5. Shadow AI column flags: 192.168.1.157 - Ollama (port 11434), 192.168.1.203 - Jupyter (port 8888)
6. User screenshots results for security team meeting
7. **v1.1+:** User exports to CSV, emails to CISO

**Critical features:** Host discovery, port scanning (simple), progress indicator, results table, shadow AI detection (LLM + dev tools)

### Scenario 2: Security Team Hunts for Rogue LLMs

1. User opens scanner, enters corporate subnet
2. Selects "Custom Scan" - manually specifies AI-related ports: 11434,1234,8888,6006,5000
3. Clicks "Scan" with max threads for speed
4. 5 minutes later, scan completes - filters results to "Shadow AI Detected Only"
5. Finds 3 Ollama instances, 2 Jupyter notebooks on developer machines
6. **v1.2+:** User loads previous week's scan to compare - sees new Ollama instance on 192.168.1.89
7. **v1.3+:** Risk scoring shows Ollama on DMZ subnet flagged as HIGH risk

**Critical features:** Port scanning (custom), multithreading, results filtering, shadow AI detection, save/load (v1.2+), risk scoring (v1.3+)

### Scenario 3: Compliance Audit Preparation

1. Auditor asks "Do you have unauthorized AI tools on the network?"
2. IT runs full network scan across all subnets
3. Scanner identifies 12 shadow AI instances across organization
4. **v1.1+:** Exports detailed CSV with IP, hostname, service type, port
5. **v1.3+:** Risk scoring auto-categorizes: 8 low risk (dev machines), 3 medium (shared servers), 1 high (DMZ Jupyter)
6. IT remediates HIGH/MEDIUM findings, documents in compliance report

**Critical features:** Port scanning (comprehensive), export (CSV), shadow AI detection, risk scoring (v1.3+)

## Open Questions for User Validation

1. **Export priority:** Do users need export in v1, or can they validate value with screenshots/manual copy first?
2. **Service fingerprinting:** How many false positives are acceptable? (e.g., port 8080 = Ollama vs generic web app)
3. **Scan scope:** Do users scan /24 subnets, or do they need /16 support (65k hosts)? Performance implications.
4. **Risk scoring criteria:** What makes an AI service "high risk"? Public-facing? Unencrypted? No auth? User-defined?
5. **Update frequency:** How often do new AI tools emerge with different ports? Need update mechanism?

## Sources

**Network Scanner Features & Best Practices:**
- [Tenable - Network Scanning Best Practices](https://www.tenable.com/cybersecurity-guide/learn/network-scanner-best-practices)
- [SecOps Solution - Top 10 Network Scanning Tools for 2026](https://www.secopsolution.com/blog/top-10-network-scanning-tools)
- [Software Testing Help - Top 15 Network Scanning Tools 2026](https://www.softwaretestinghelp.com/network-scanning-tools/)
- [Nmap - Performance and Timing](https://nmap.org/book/man-performance.html)

**Network Scanner Tool Comparisons:**
- [Web Asha Technologies - Nmap vs Zenmap vs Angry IP Scanner Comparison](https://www.webasha.com/blog/best-network-scanning-tool-comparison-nmap-vs-zenmap-vs-angry-ip-scanner-vs-hping3-with-commands-use-cases-and-real-time-output)
- [Comparitech - Best Angry IP Scanner Alternatives](https://www.comparitech.com/net-admin/angry-ip-scanner-review-best-alternatives/)

**Shadow AI Detection:**
- [Knostic - 10 Best Shadow AI Detection Tools for 2026](https://www.knostic.ai/blog/shadow-ai-detection-tools)
- [Teramind - Shadow AI Detection Software](https://www.teramind.co/solutions/shadow-ai-detection/)
- [TechCrunch - Rogue Agents and Shadow AI Security](https://techcrunch.com/2026/01/19/rogue-agents-and-shadow-ai-why-vcs-are-betting-big-on-ai-security/)
- [Auvik - Shadow AI Detection Tool](https://www.auvik.com/saas-management/use-case/shadow-ai-detection-tool/)

**Local LLM Deployments:**
- [Cisco - Detecting Exposed LLM Servers: Ollama Case Study](https://blogs.cisco.com/security/detecting-exposed-llm-servers-shodan-case-study-on-ollama)
- [Glukhov - Local LLM Hosting Complete 2026 Guide](https://www.glukhov.org/post/2025/11/hosting-llms-ollama-localai-jan-lmstudio-vllm-comparison/)
- [Oreate AI - Understanding Ollama Port Configuration](https://www.oreateai.com/blog/understanding-ollama-the-port-it-runs-on/1ca77b4c5bcd7eaafb4c93c2d7782b58)
- [Hypereal Tech - Top Tools for Running LLMs Locally 2026](https://hypereal.tech/a/top-llm-local-tools)

**LLM Endpoints & Security:**
- [arXiv - Unveiling LLM Deployment in the Wild: Empirical Study](https://arxiv.org/html/2505.02502v2)
- [DEV Community - LLM Security Alert: 91,000+ Attacks on Enterprise Endpoints](https://dev.to/alessandro_pignati/llm-security-alert-91000-attacks-probing-enterprise-ai-endpoints-and-how-to-stop-them-2cko)

**HuggingFace & AI Development Tools:**
- [HuggingFace - Text Generation Inference](https://huggingface.co/docs/text-generation-inference/en/index)
- [DataCamp - Hugging Face TGI Toolkit Tutorial](https://www.datacamp.com/tutorial/hugging-faces-text-generation-inference-toolkit-for-llms)
- [Machine Learning Mastery - Building LLM Applications with FastAPI](https://machinelearningmastery.com/building-llm-applications-with-hugging-face-endpoints-and-fastapi/)

**AI Services Market Share (2026):**
- [IsDown - AI Systems Status Report January 2026](https://isdown.app/blog/ai-systems-status-report-january-2026)
- [Views4You - 2025 AI Tools Usage Statistics](https://views4you.com/ai-tools-usage-statistics-report-2025/)
- [First AI Movers - Complete AI Platform Comparison Guide 2026](https://www.firstaimovers.com/p/complete-eight-ai-platform-comparison-guide-2025)

**Export Formats & Results Management:**
- [SoftPerfect - Network Scanner Manual](https://www.softperfect.com/products/networkscanner/manual/)
- [Nmap - Output Formats](https://nmap.org/book/man-output.html)
- [Advanced IP Scanner - Help Documentation](https://www.advanced-ip-scanner.com/help/)

**Scanning Performance & Common Pitfalls:**
- [Nmap - Scan Time Reduction Techniques](https://nmap.org/book/reduce-scantime.html)
- [Nucamp - Nmap Guide 2026: Network Scanning Basics](https://www.nucamp.co/blog/nmap-guide-2026-network-scanning-basics-ethical-practical)
- [LinkedIn - Common Challenges of Vulnerability Scanning](https://www.linkedin.com/advice/3/what-common-challenges-pitfalls-vulnerability)

---

**Confidence Assessment:**

- **Table Stakes Features:** HIGH confidence - Based on analysis of established tools (Nmap, Angry IP, Advanced IP Scanner) with consistent feature sets
- **Shadow AI Services Catalog:** MEDIUM confidence - Based on 2026 web research and security studies, but AI tooling landscape evolves rapidly
- **Shadow AI Detection as Differentiator:** MEDIUM confidence - Market research confirms demand (Teramind, Witness AI raised $58M), but limited competitors doing network-level detection vs endpoint agents
- **Port/Service Detection:** HIGH confidence - Industry-standard approaches documented in Nmap, Cisco security research
- **Feature Prioritization:** MEDIUM confidence - Based on competitive analysis and user workflows, but needs real user validation

**Gaps Requiring Validation:**

1. **User willingness to pay:** Shadow AI detection is novel - is it valuable enough to drive adoption vs free Nmap + custom scripts?
2. **False positive tolerance:** Without v1 service fingerprinting, port-based detection will flag generic web apps - acceptable?
3. **Update cadence:** New AI tools emerge monthly - how to keep detection signatures current without auto-update infra?
4. **Enterprise integration:** Do users need API/CLI for integration with SIEM/SOAR, or is standalone exe sufficient?

---

*Feature research for: Network Scanner with Shadow AI Detection*
*Researched: 2026-02-08*
