# Pitfalls Research

**Domain:** Network Scanner with Shadow AI Detection (Tauri + Rust + React, Windows)
**Researched:** 2026-02-08
**Confidence:** MEDIUM-HIGH

## Critical Pitfalls

### Pitfall 1: Windows Raw Socket Restrictions Kill Performance

**What goes wrong:**
On Windows, raw socket support was disabled in XP SP2 for security reasons. Network scanners must either use Npcap/WinPcap for raw packet access or fall back to TCP connect() scanning, which is slower, more detectable, and consumes more resources. Developers build their scanner assuming raw sockets work, then discover Windows blocks them, forcing a late-stage architectural pivot.

**Why it happens:**
Linux network programming documentation dominates online resources. Developers assume Windows networking works the same way. Raw socket APIs exist in Windows but fail at runtime with permission errors.

**How to avoid:**
- Use Npcap (modern, NDIS 6 lightweight filter driver) not WinPcap (deprecated, slower NDIS 5)
- Design scanner to require Npcap installation as prerequisite
- Plan for administrator privileges requirement (Npcap can optionally restrict to admin-only)
- On Windows, IPv6 raw sockets only work on ethernet devices (not tunnels), only on Vista+
- Implement capability detection: if Npcap unavailable, fall back to TCP connect scanning with clear UX warning

**Warning signs:**
- Scan works on Linux dev environment but fails on Windows
- "Access denied" errors when creating raw sockets
- Significantly slower performance on Windows vs Linux
- Anti-virus flags your executable

**Phase to address:**
Phase 1 (Foundation) - Architecture must account for Windows limitations from day one. Do not defer this decision.

---

### Pitfall 2: Thread Pool Exhaustion and Socket Port Exhaustion

**What goes wrong:**
Developers spawn too many threads for parallel scanning, causing socket exhaustion where the OS runs out of available ports. On Windows, port exhaustion manifests as TCP communication failures. Some systems hit limits as low as 10 threads before socket-related problems occur. Multi-threaded port scanners miss open ports because they count socket errors as "closed ports" instead of retrying.

**Why it happens:**
More threads = faster scan seems obvious. Developers don't account for:
- Each thread uses approximately 5 ports per second
- One thread = ~300 socket ports per 60 seconds (at 200ms response time)
- Windows has ~64,000 ephemeral ports, but OS reserves many
- Global locks in runtime make multi-threaded programs sometimes slower than single-threaded

**How to avoid:**
- Calculate max threads: If response time is 200ms, max ~93 threads before port exhaustion
- For /24 subnet (254 hosts), 50-100 threads is sufficient
- Implement adaptive parallelism: start conservative, increase if network proves reliable
- Use async I/O with single-threaded event loop instead of thread-per-connection
- Monitor socket metrics: if errors spike, reduce parallelism dynamically
- Set connection timeouts aggressively but not too low (avoid timeout -> retransmit loops)
- Reuse sockets via connection pooling where possible

**Warning signs:**
- "Cannot create socket" errors during scans
- Intermittent failures that resolve after waiting
- High error rates reported as "closed ports"
- Scanner reports fewer open ports than expected
- System becomes unresponsive during scans

**Phase to address:**
Phase 2 (Core Scanning) - Implement adaptive thread pool with backoff from the start. This is not an optimization, it's correctness.

---

### Pitfall 3: Blocking Operations in Async Rust (Tokio) Kill Throughput

**What goes wrong:**
Network scanning code mixes blocking I/O (file operations, DNS lookups, synchronous Rust libraries) with async networking in Tokio. Blocking operations on async runtime threads starve the event loop, causing throughput collapse. Any latency >10-100 microseconds blocks the event loop. File I/O is blocking. Developers don't realize their "async" scanner is actually blocking.

**Why it happens:**
- Not all Rust crates are async-aware
- File I/O looks synchronous but blocks
- DNS resolution can block for seconds
- Developers use .await without understanding what's actually blocking vs truly async

**How to avoid:**
- Use `tokio::task::spawn_blocking()` for any blocking operations
- Never do file I/O on async runtime threads - spawn to blocking thread pool
- Use async DNS resolution libraries (trust-dns/hickory-dns) not std::net::lookup
- Tokio provides async file wrapper that spawns OS thread per operation
- Architecture: one async thread for network I/O, communicate with sync threads via channels
- Profile with tokio-console to identify blocking operations
- General rule: if it's not network I/O and takes >100 microseconds, spawn_blocking

**Warning signs:**
- High CPU but low network utilization
- Scan speed doesn't scale with thread count
- Tokio runtime warnings about "blocking"
- Uneven progress across targets (some fast, some stalled)

**Phase to address:**
Phase 2 (Core Scanning) - Architecture decision. Must separate blocking from async from the beginning.

---

### Pitfall 4: Tauri State Management Runtime Panics

**What goes wrong:**
Using the wrong type for State parameters causes runtime panics instead of compile-time errors. Developer uses `State<'_, AppState>` but stored `State<'_, Mutex<AppState>>`. Application compiles fine but panics when command is invoked. In multithreaded scanner, this causes UI to freeze or crash during active scans.

**Why it happens:**
- Tauri automatically wraps state in Arc, but developers add Arc manually (double-wrapping)
- State types must match exactly: `State<'_, Mutex<AppState>>` not `State<'_, AppState>`
- Rust's type system can't catch this at compile time due to Tauri's macro-based architecture
- Documentation shows simple examples without complex state

**How to avoid:**
- Use type aliases to prevent accidental double-wrapping: `type SharedState = Mutex<AppState>;`
- Tauri provides Arc automatically - never wrap state in Arc yourself
- For mutable state, wrap in `Mutex<T>` or `RwLock<T>`, but Tauri wraps in Arc
- Use std::sync::Mutex for most cases, only use tokio::sync::Mutex when holding guard across .await
- Test state access in integration tests before UI work begins
- Document exact state type in comments next to struct definition

**Warning signs:**
- "State not managed" runtime panic
- Type mismatches in Tauri command invocations
- State mutations don't persist across commands
- Deadlocks when accessing state from multiple commands

**Phase to address:**
Phase 2 (Core Scanning) - Set up state architecture correctly before implementing scan logic.

---

### Pitfall 5: Aggressive Scan Timing Causes Paradoxical Slowdown

**What goes wrong:**
Developer sets aggressive timing parameters to speed up scans. Network devices or host OS implement rate limiting. Scanner triggers rate limiting, causing massive packet loss. Nmap-style adaptive retransmission kicks in, increasing retransmissions. Paradoxically, aggressive timing makes scan take 10x longer than conservative timing. Scan appears to "hang" partway through.

**Why it happens:**
- "Faster = better" intuition from non-network domains
- Developers don't account for target-side rate limiting
- Setting min timeout too low causes timeout -> retransmit -> congestion loop
- Windows firewall drops ICMP packets above certain rate
- Network switches implement ICMP rate limiting (common default: 10-100 pings/second)

**How to avoid:**
- Implement adaptive timing: start conservative, speed up if network proves reliable
- Monitor packet loss: if >2% loss, back off timing
- Exponential backoff with jitter when rate limited
- For /24 networks, conservative timing completes in seconds anyway
- Set max-scan-delay to prevent excessive backoff, but not too low (causes retransmit waste)
- ICMP specifically: limit to 10-50 pings/second per target
- Implement scan delay when network shows congestion
- Log timing adjustments for debugging

**Warning signs:**
- Scan starts fast then slows dramatically
- Packet retransmission counters increase
- Scan time increases when reducing timeout values
- Target network shows signs of congestion
- Router logs show dropped packets

**Phase to address:**
Phase 3 (Performance) - Build adaptive timing from the start in Phase 2, tune in Phase 3.

---

### Pitfall 6: Windows Defender and Antivirus False Positives

**What goes wrong:**
Windows Defender and third-party antivirus tools flag network scanner as malware/hack tool. Executable is quarantined on user machines. Scanner is deleted before it runs. Users can't install or run the tool. Code-signing doesn't prevent this - behavior-based detection triggers on network scanning patterns.

**Why it happens:**
- Network scanning behavior matches malware reconnaissance patterns
- Raw packet access via Npcap triggers heuristics
- Fast network activity looks like botnet behavior
- Tools like Nirsoft WirelessNetView consistently flagged despite being legitimate
- AV vendors prioritize false positives over false negatives for network tools

**How to avoid:**
- Submit builds to Microsoft Defender for analysis before release
- Code-sign executable with EV certificate (more trusted than standard cert)
- Minimize obfuscation - clear function names, no packers
- Include clear UI explaining what scanner does before starting
- Provide instructions for adding exceptions to Windows Defender
- Consider distributing via Microsoft Store (pre-vetted)
- Document expected AV behavior in installation guide
- Build telemetry to detect when AV blocks execution
- Slow down scanning behavior slightly (AV looks for speed patterns)

**Warning signs:**
- Beta testers report "file deleted after download"
- Windows SmartScreen blocks execution
- Logs show scanner starts then immediately terminates
- Users report "Windows protected your PC" warnings

**Phase to address:**
Phase 5 (Polish) - Address before distributing to users. Submit for AV whitelisting during beta.

---

### Pitfall 7: Tauri Event Emission Memory Leaks

**What goes wrong:**
Scanner emits progress events from Rust backend to React frontend continuously during scan. After scanning large networks, frontend memory usage grows to 1GB+. Memory is never freed. Application becomes sluggish. Browser-based webview (WRY) doesn't garbage collect event listeners properly. Long-running scans crash due to OOM.

**Why it happens:**
- Documented issue in Tauri/WRY: emitting millions of events causes memory leak
- Frontend accumulates event history even after processing
- React components hold references to old event data
- Webview doesn't GC aggressively like browser tabs

**How to avoid:**
- Batch events: emit every 100ms with aggregated results, not per-host
- Use channels with bounded size, drop old events if consumer is slow
- Frontend: don't store all events in React state, only current scan progress
- Clear event listeners when components unmount
- For large datasets, use pagination or virtualized lists
- Consider polling for status instead of event streaming for long scans
- Monitor memory usage during development, set thresholds for alerts
- After scan completes, emit "reset" event to clear frontend state

**Warning signs:**
- Memory usage grows continuously during scan
- UI becomes sluggish after multiple scans
- Task Manager shows increasing memory for scanner process
- Frontend re-renders become slower over time

**Phase to address:**
Phase 3 (Performance) - Design event architecture with batching from the start. Monitor memory during Phase 2 development.

---

### Pitfall 8: Windows Firewall Blocks Self-Generated ICMP by Default

**What goes wrong:**
Scanner sends ICMP echo requests to discover hosts. Windows Firewall blocks ICMP echo requests by default on private/public networks (allowed on domain networks only). Scanner reports zero hosts found on local network. User thinks scanner is broken. Scanner actually works but firewall blocks responses.

**Why it happens:**
- Windows Firewall default policy: block inbound ICMP on non-domain networks
- Scanner developers test on permissive networks or with firewall disabled
- Users run scanner on default Windows configuration
- Error messaging doesn't distinguish "no hosts" from "firewall blocked"

**How to avoid:**
- Detect Windows Firewall status on startup
- Check if ICMP rule "File and Printer Sharing (Echo Request – ICMPv4-In)" is enabled
- Show clear warning if firewall will block results
- Provide one-click "Configure Firewall" button (requires admin, uses netsh or PowerShell)
- Documentation: include firewall configuration steps with screenshots
- Implement fallback: if ping scan finds zero hosts, try TCP SYN scan as sanity check
- Log firewall-related errors distinctly from "no hosts found"

**Warning signs:**
- Ping scan finds zero hosts on known-populated network
- Scanner works on some networks but not others
- Results differ between admin and non-admin users
- Localhost/127.0.0.1 ping works but LAN IPs don't

**Phase to address:**
Phase 1 (Foundation) - Include firewall detection in initial Windows integration work.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Skip Npcap check, assume raw sockets work | Faster initial development | App fails on most Windows systems, emergency rewrite needed | Never - 5 minutes to add check |
| Use std::sync instead of async for networking | Simpler code, easier to understand | Cannot scale beyond ~10 concurrent connections, thread pool exhaustion | Never for network I/O, always for file/disk |
| Emit individual events per scan result | Real-time UI updates feel responsive | Memory leak after 10K+ events, eventual OOM crash | Only for demos, must batch for production |
| Hard-code thread count instead of adaptive | Predictable behavior, easier to debug | Fails on slow networks, causes socket exhaustion on fast networks | Development only, never production |
| Ignore rate limiting, scan at max speed | Impressive demo performance | Triggers IDS, network admin complaints, paradoxically slower on real networks | Never - adaptive timing is table stakes |
| Store scan history in React state indefinitely | Simple state management, no backend needed | Memory grows unbounded, UI becomes sluggish | Acceptable for <100 results, pagination for more |

## Integration Gotchas

Common mistakes when connecting to external services.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Npcap | Assuming it's installed, no error handling | Detect on startup, show installation instructions with download link if missing |
| Windows Firewall | Ignore firewall state, assume network is open | Detect firewall rules, warn user if ICMP blocked, provide fix instructions |
| Tauri Commands | Return large data (file buffers) as JSON | Use `tauri::ipc::Response` for binary data, stream large results via channels |
| Tokio Runtime | Call blocking functions (file I/O, DNS) directly | Wrap in `spawn_blocking()` or use async-aware libraries |
| React State | Store entire scan results array in useState | Use pagination/virtualization, store in Rust state, query as needed |
| Shadow AI Detection | Block UI thread while ML model runs inference | Run in separate thread, emit progress events, keep UI responsive |

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Unbounded event emission (Tauri → React) | Memory grows 1GB+ after large scans | Batch events every 100ms, limit history size | >10,000 events (~40 hosts with detailed port scans) |
| Thread-per-target scanning | Socket exhaustion, "cannot create socket" errors | Async I/O with bounded task pool (50-100 max) | >93 threads with 200ms response time |
| Storing all results in React state | UI re-renders slow down, input lag | Virtualized list, paginate results, keep only visible items in state | >500 scan results rendered |
| Synchronous DNS lookups | Scan hangs for seconds on slow DNS | Use async DNS library (hickory-dns), timeout aggressively (1-2s) | First slow DNS lookup blocks entire scan |
| No scan delay/backoff | Network congestion, packet loss >10% | Adaptive timing: start at 10ms delay, increase if packet loss detected | Scanning faster than 100 packets/sec |
| File I/O on async runtime | Tokio warnings, throughput collapse | Use spawn_blocking for file operations | Writing scan logs during active scan |

## Security Mistakes

Domain-specific security issues beyond general web security.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Scanning without user confirmation | Legal liability, unauthorized network scanning | Require explicit user action to start scan, show target network clearly, confirm button |
| Storing scan results unencrypted | Sensitive network topology exposed if disk compromised | Encrypt results at rest, use Windows Data Protection API (DPAPI) |
| Running with permanent admin privileges | Entire app attack surface elevated | Request admin only for Npcap operations, drop privileges for UI/logic |
| Not validating IP range input | User scans 0.0.0.0/0 (entire internet), legal/network issues | Validate ranges, block public IP ranges by default, require confirmation for >254 hosts |
| Embedding Npcap installer without license | GPL/commercial license violation | Link to official Npcap download, document installation steps, or obtain commercial license |
| No rate limiting for external scans | Triggers IDS/IPS, angry network admins, legal issues | Default to conservative timing for non-local networks, warn about external scanning risks |

## UX Pitfalls

Common user experience mistakes in this domain.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| No progress indication during scan | User thinks app froze, kills process | Show per-host progress, estimated time remaining, current target IP |
| Reporting "host down" vs "filtered" identically | User can't distinguish offline hosts from firewalled hosts | Distinct states: Down (no response), Filtered (ICMP blocked), Up (responded) |
| Starting scan without Npcap check | Cryptic error messages, user frustration | Detect on startup, show clear instructions with download link before allowing scan |
| Displaying raw port numbers without service names | User sees "80, 443, 22" without context | Show "HTTP (80), HTTPS (443), SSH (22)" with common service names |
| No way to stop long-running scan | User must kill app, loses partial results | Cancellable scans with "Stop" button, save partial results on cancel |
| Scan results disappear on app close | User must re-scan to reference results | Auto-save results, persist across sessions, export to CSV/JSON |
| No explanation when zero hosts found | User thinks scanner is broken | Check common issues (firewall, network interface selection) and suggest fixes |

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Ping Scan:** Often missing ICMP firewall detection — verify shows warning when Windows Firewall blocks ICMP
- [ ] **Port Scan:** Often missing socket exhaustion handling — verify uses bounded thread/task pool and doesn't crash on large ranges
- [ ] **Progress UI:** Often missing time estimation — verify shows "N of M hosts scanned, ~X seconds remaining"
- [ ] **Npcap Integration:** Often missing graceful degradation — verify detects missing Npcap and falls back to TCP connect scan
- [ ] **Admin Privileges:** Often missing UAC prompt handling — verify requests elevation only when needed, explains why
- [ ] **Scan Cancellation:** Often missing cleanup on cancel — verify stops threads, closes sockets, saves partial results
- [ ] **Event Batching:** Often missing backpressure handling — verify frontend doesn't accumulate unbounded events
- [ ] **Error States:** Often missing distinction between error types — verify different messages for timeout, refused, filtered, unreachable
- [ ] **Large Network Scans:** Often missing memory management — verify doesn't leak memory on 1000+ host scans
- [ ] **Windows Firewall:** Often missing auto-configuration — verify detects blocked ICMP and offers one-click fix

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Socket exhaustion mid-scan | LOW | Reduce active thread count, wait 60s for ports to release, resume scan |
| Memory leak from event emission | MEDIUM | Restart app (loses scan state), refactor to batch events (1-2 days) |
| Raw socket restrictions | HIGH | Require Npcap installation (user action), refactor scanner to use libpcap bindings (1 week) |
| Blocking operations in async | MEDIUM | Identify blocking call with tokio-console, wrap in spawn_blocking (1-2 days) |
| Tauri state type mismatch | LOW | Fix type annotation, restart app (30 minutes) |
| AV false positive | HIGH | Submit to vendor for whitelist (weeks), add AV exception docs (immediate) |
| Rate limiting triggered | LOW | Implement adaptive backoff, reduce scan speed 50% (1 day) |
| Windows Firewall blocked | IMMEDIATE | Document firewall configuration, add detection + warning (2 hours) |

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Raw socket restrictions (Windows) | Phase 1: Foundation | Test on clean Windows VM without Npcap, verify clear error message |
| Thread pool exhaustion | Phase 2: Core Scanning | Scan /24 network, monitor socket count, verify stays below system limit |
| Blocking in async runtime | Phase 2: Core Scanning | Profile with tokio-console during scan, verify no blocking warnings |
| Tauri state management panics | Phase 2: Core Scanning | Integration tests for all state-accessing commands |
| Aggressive timing slowdown | Phase 3: Performance | Scan rate-limited network, verify adaptive backoff activates |
| Event emission memory leaks | Phase 3: Performance | Run 1000-host scan, monitor memory, verify <100MB growth |
| Windows Firewall blocks ICMP | Phase 1: Foundation | Test on default Windows config, verify firewall detection |
| AV false positives | Phase 5: Polish | Submit to Microsoft Defender, test on fresh Windows with default AV |
| Npcap missing/not installed | Phase 1: Foundation | Test without Npcap, verify clear installation instructions |
| Large result set UI slowdown | Phase 4: UI/UX Polish | Load 1000+ results, verify virtualization keeps UI responsive |

## Sources

**Network Scanning:**
- [Nmap Guide 2026: Network Scanning Basics](https://www.nucamp.co/blog/nmap-guide-2026-network-scanning-basics-ethical-practical)
- [Common challenges and pitfalls of vulnerability scanning](https://www.linkedin.com/advice/3/what-common-challenges-pitfalls-vulnerability)
- [Nmap Timing and Performance](https://nmap.org/book/man-performance.html)
- [Vulnerability Scanning Guide](https://www.srsnetworks.net/blog/network-vulnerability-scanning-guide/)

**Windows Raw Sockets & Npcap:**
- [Raw socket programming on Windows with Winsock](https://www.binarytides.com/raw-sockets-using-winsock/)
- [Npcap Users' Guide](https://npcap.com/guide/npcap-users-guide.html)
- [WinPcap FAQ](https://www.winpcap.org/misc/faq.htm)

**Multithreading & Performance:**
- [How to optimize port-scanning with a multi-threaded approach](https://initialcommit.com/blog/portscan-programming-security)
- [Script Parallelism in NSE](https://nmap.org/book/nse-parallelism.html)
- [TCP/IP port exhaustion troubleshooting - Windows](https://learn.microsoft.com/en-us/troubleshoot/windows-client/networking/tcp-ip-port-exhaustion-troubleshooting)

**Tauri Pitfalls:**
- [Calling Rust from the Frontend | Tauri](https://v2.tauri.app/develop/calling-rust/)
- [State Management | Tauri](https://v2.tauri.app/develop/state-management/)
- [Memory leak when emitting events](https://github.com/tauri-apps/tauri/issues/12724)
- [Memory leaks when reading files](https://github.com/tauri-apps/tauri/issues/9190)
- [Building Tauri Apps That Don't Hog Memory at Idle](https://medium.com/@hadiyolworld007/building-tauri-apps-that-dont-hog-memory-at-idle-de516dabb938)

**Rust Async & Tokio:**
- [Bridging with sync code | Tokio](https://tokio.rs/tokio/topics/bridging)
- [The Hidden Bottleneck: Blocking in Async Rust](https://cong-or.xyz/blocking-async-rust)
- [Practical Guide to Async Rust and Tokio](https://medium.com/@OlegKubrakov/practical-guide-to-async-rust-and-tokio-99e818c11965)

**Windows Firewall:**
- [How to Allow Ping (ICMP Echo Requests) on Windows Firewall](https://theitbros.com/allow-ping-icmp-echo-requests-on-windows-firewall/)
- [Windows PC Blocking ICMP](https://learn.microsoft.com/en-us/answers/questions/1495952/windows-pc-blocking-icmp)

**Rate Limiting & Backoff:**
- [Mastering Exponential Backoff in Distributed Systems](https://betterstack.com/community/guides/monitoring/exponential-backoff/)
- [What is an ICMP Flood? Ping Flood DDoS Attack](https://www.sentinelone.com/cybersecurity-101/cybersecurity/what-is-an-icmp-flood-ping-flood-ddos-attack/)

**Antivirus False Positives:**
- [Address false positives/negatives in Microsoft Defender](https://learn.microsoft.com/en-us/defender-endpoint/defender-endpoint-false-positives-negatives)
- [Windows Defender False-Positives](https://learn.microsoft.com/en-us/answers/questions/3785610/windows-defender-false-positives-loop)

---
*Pitfalls research for: Network Scanner with Shadow AI Detection (Tauri + Rust + React, Windows)*
*Researched: 2026-02-08*
