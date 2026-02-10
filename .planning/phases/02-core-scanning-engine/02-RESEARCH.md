# Phase 2: Core Scanning Engine - Research

**Researched:** 2026-02-10
**Domain:** Concurrent network scanning with Tokio - CIDR parsing, async port scanning, reverse DNS, bounded concurrency, real-time progress streaming
**Confidence:** MEDIUM-HIGH

## Summary

Phase 2 implements fast, concurrent network scanning using Tokio's async runtime for both ping sweep (ICMP) and TCP port scanning. The core technical challenges are: parsing IP ranges (CIDR and start-end formats), managing bounded concurrency to prevent socket exhaustion, performing async reverse DNS lookups without blocking, streaming real-time progress to React frontend without memory leaks, and implementing graceful scan cancellation.

The Rust ecosystem provides solid libraries for all requirements: cidr crate for CIDR notation parsing, tokio::net::TcpStream for async TCP connections, hickory-resolver (formerly trust-dns) for async DNS lookups, tokio::sync::Semaphore for bounded concurrency control, and tokio_util::sync::CancellationToken for graceful task cancellation. Tauri provides channels for high-throughput event streaming (superior to events for real-time progress). The critical architectural decisions are using semaphore-based concurrency limits (50-100 concurrent tasks to avoid socket exhaustion), batching progress updates (emit every 100ms, not per-host), and separating blocking operations via spawn_blocking.

**Primary recommendation:** Use tokio::sync::Semaphore with 50-100 permits for concurrency control, stream results via Tauri channels with batched updates, use hickory-resolver for async reverse DNS, implement CancellationToken pattern for scan cancellation, and avoid FuturesUnordered for >100 tasks (use spawn with semaphore instead).

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| cidr | 0.3+ | CIDR notation parsing and IP iteration | FromStr trait for parsing, iterator support for IP ranges, handles IPv4/IPv6 |
| tokio::net::TcpStream | 1.43+ (LTS) | Async TCP port scanning | Built into tokio, async connect with timeout, zero-cost abstractions |
| hickory-resolver | 0.24+ | Async reverse DNS lookups | Successor to trust-dns, async-first design, tokio integration, dual-stack support |
| tokio::sync::Semaphore | 1.43+ (LTS) | Bounded concurrency control | Built into tokio, prevents socket exhaustion, efficient permit-based system |
| tokio_util::sync::CancellationToken | 0.7+ | Graceful scan cancellation | Official tokio-util pattern, cooperative cancellation, supports child tokens |
| tauri::ipc::Channel | 2.10+ | Real-time progress streaming | Fast ordered delivery, designed for streaming, avoids event memory leaks |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| serde | 1.0+ | Result serialization | Already in Cargo.toml, serialize scan results for frontend |
| tokio::time::timeout | 1.43+ (LTS) | Per-host/port timeouts | Wrap TCP connect and DNS lookups, configurable via CONF-01 |
| futures::stream::FuturesUnordered | 0.3+ | Concurrent task collection (LIMITED USE) | ONLY for <100 tasks, avoid for main scan loop |
| tokio::task::spawn_blocking | 1.43+ (LTS) | Blocking operation wrapper | File I/O, any non-async library calls |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| cidr | ipnet, iprange | cidr has simpler API for our use case, ipnet has more features (subnet math) |
| hickory-resolver | dns-lookup (stdlib wrapper) | dns-lookup is synchronous (blocks runtime), hickory-resolver is async-first |
| tokio::sync::Semaphore | Custom channel-based limiter | Semaphore is battle-tested, custom solution is error-prone |
| Tauri Channels | Tauri Events | Events cause memory leaks with high-frequency emission, channels designed for streaming |
| tokio::net::TcpStream | async-port-scanner crate | Custom implementation gives better control, crate adds dependency |

**Installation:**
```toml
# Add to src-tauri/Cargo.toml [dependencies]
cidr = "0.3"
hickory-resolver = "0.24"
tokio-util = { version = "0.7", features = ["sync"] }

# Already have: tokio 1.43, surge-ping 0.8, socket2 0.5, serde 1.0
```

## Architecture Patterns

### Recommended Project Structure
```
src-tauri/src/
├── commands/
│   ├── mod.rs
│   ├── platform.rs          # Existing platform checks
│   ├── scan.rs              # NEW: Main scan orchestration
│   └── network.rs           # NEW: Ping, port scan, DNS helpers
├── types/
│   └── scan_types.rs        # NEW: ScanConfig, ScanResult, ScanProgress
└── lib.rs                   # Register new commands
```

### Pattern 1: CIDR Parsing and IP Range Iteration
**What:** Parse CIDR notation (192.168.1.0/24) and iterate over all IPs in range
**When to use:** User inputs IP range (DISC-02), generating target list
**Example:**
```rust
// Source: https://docs.rs/cidr
use cidr::Ipv4Cidr;
use std::net::Ipv4Addr;

fn parse_cidr(range: &str) -> Result<Vec<Ipv4Addr>, String> {
    let cidr: Ipv4Cidr = range.parse()
        .map_err(|e| format!("Invalid CIDR: {}", e))?;

    // Iterate over all IPs in range
    let ips: Vec<Ipv4Addr> = cidr.iter().map(|inet| inet.address()).collect();
    Ok(ips)
}

// Parse start-end format (192.168.1.1-192.168.1.254)
fn parse_ip_range(range: &str) -> Result<Vec<Ipv4Addr>, String> {
    let parts: Vec<&str> = range.split('-').collect();
    if parts.len() != 2 {
        return Err("Invalid range format".to_string());
    }

    let start: Ipv4Addr = parts[0].trim().parse()
        .map_err(|_| "Invalid start IP")?;
    let end: Ipv4Addr = parts[1].trim().parse()
        .map_err(|_| "Invalid end IP")?;

    let start_u32 = u32::from(start);
    let end_u32 = u32::from(end);

    Ok((start_u32..=end_u32)
        .map(|ip_u32| Ipv4Addr::from(ip_u32))
        .collect())
}
```

### Pattern 2: Bounded Concurrent Port Scanning with Semaphore
**What:** Limit concurrent TCP connections to prevent socket exhaustion
**When to use:** PORT-07 multithreaded scanning, CONF-02 configurable concurrency
**Example:**
```rust
// Source: https://docs.rs/tokio/latest/tokio/sync/struct.Semaphore.html
use tokio::sync::Semaphore;
use tokio::net::TcpStream;
use tokio::time::{timeout, Duration};
use std::sync::Arc;

async fn scan_ports_bounded(
    host: Ipv4Addr,
    ports: Vec<u16>,
    concurrency: usize,
    timeout_ms: u64,
) -> Vec<u16> {
    let semaphore = Arc::new(Semaphore::new(concurrency));
    let mut tasks = Vec::new();

    for port in ports {
        let sem = semaphore.clone();
        let task = tokio::spawn(async move {
            // Acquire permit before attempting connection
            let _permit = sem.acquire_owned().await.unwrap();

            let addr = format!("{}:{}", host, port);
            let result = timeout(
                Duration::from_millis(timeout_ms),
                TcpStream::connect(&addr)
            ).await;

            // Permit is dropped here, allowing next task to proceed
            if result.is_ok() {
                Some(port)
            } else {
                None
            }
        });
        tasks.push(task);
    }

    // Wait for all tasks to complete
    let mut open_ports = Vec::new();
    for task in tasks {
        if let Ok(Some(port)) = task.await {
            open_ports.push(port);
        }
    }

    open_ports
}
```

### Pattern 3: Async Reverse DNS Lookup
**What:** Perform non-blocking reverse DNS lookups to get hostnames (DISC-04)
**When to use:** After discovering live hosts, resolve IP to hostname
**Example:**
```rust
// Source: https://github.com/hickory-dns/hickory-dns
use hickory_resolver::TokioAsyncResolver;
use hickory_resolver::config::{ResolverConfig, ResolverOpts};
use std::net::IpAddr;

async fn reverse_dns_lookup(ip: IpAddr) -> Option<String> {
    // Create resolver with system configuration
    let resolver = TokioAsyncResolver::tokio(
        ResolverConfig::default(),
        ResolverOpts::default()
    );

    // Perform reverse lookup with timeout
    match tokio::time::timeout(
        Duration::from_secs(2),
        resolver.reverse_lookup(ip)
    ).await {
        Ok(Ok(lookup)) => {
            // Get first PTR record
            lookup.iter().next().map(|name| name.to_string())
        }
        _ => None, // Timeout or lookup failed
    }
}
```

### Pattern 4: Real-Time Progress Streaming with Tauri Channels
**What:** Stream scan progress to frontend without memory leaks (UI-03)
**When to use:** Updating UI during long-running scans
**Example:**
```rust
// Source: https://v2.tauri.app/develop/calling-frontend/
use tauri::ipc::Channel;
use serde::Serialize;

#[derive(Clone, Serialize)]
#[serde(tag = "type")]
enum ScanProgress {
    Started { total_hosts: usize },
    HostComplete { ip: String, hostname: Option<String>, open_ports: Vec<u16> },
    Progress { scanned: usize, total: usize, percent: f32 },
    Completed { total_scanned: usize, duration_ms: u64 },
}

#[tauri::command]
async fn start_scan(
    range: String,
    on_progress: Channel<ScanProgress>,
) -> Result<(), String> {
    let ips = parse_cidr(&range)?;
    let total = ips.len();

    on_progress.send(ScanProgress::Started { total_hosts: total }).unwrap();

    let mut scanned = 0;
    let mut last_update = std::time::Instant::now();

    for ip in ips {
        // Perform scan...
        let open_ports = scan_host(ip).await;
        scanned += 1;

        // Batch progress updates - emit at most every 100ms
        if last_update.elapsed() > Duration::from_millis(100) {
            on_progress.send(ScanProgress::Progress {
                scanned,
                total,
                percent: (scanned as f32 / total as f32) * 100.0,
            }).unwrap();
            last_update = std::time::Instant::now();
        }

        // Always emit individual results
        on_progress.send(ScanProgress::HostComplete {
            ip: ip.to_string(),
            hostname: reverse_dns_lookup(IpAddr::V4(ip)).await,
            open_ports,
        }).unwrap();
    }

    on_progress.send(ScanProgress::Completed {
        total_scanned: scanned,
        duration_ms: 0, // Calculate actual duration
    }).unwrap();

    Ok(())
}
```

### Pattern 5: Cancellable Scans with CancellationToken
**What:** Allow user to stop running scan gracefully (UI-07)
**When to use:** Long-running scans that need cancellation support
**Example:**
```rust
// Source: https://docs.rs/tokio-util/latest/tokio_util/sync/struct.CancellationToken.html
use tokio_util::sync::CancellationToken;
use tauri::State;
use std::sync::Mutex;

// Shared state for cancellation
struct ScanState {
    cancel_token: Mutex<Option<CancellationToken>>,
}

#[tauri::command]
async fn start_cancellable_scan(
    range: String,
    state: State<'_, ScanState>,
) -> Result<(), String> {
    let cancel_token = CancellationToken::new();

    // Store token so cancel_scan can access it
    *state.cancel_token.lock().unwrap() = Some(cancel_token.clone());

    let ips = parse_cidr(&range)?;

    for ip in ips {
        // Check for cancellation before each host
        if cancel_token.is_cancelled() {
            return Ok(()); // Exit gracefully
        }

        // Perform scan with cancellation-aware select
        tokio::select! {
            _ = cancel_token.cancelled() => {
                return Ok(()); // Cancelled mid-scan
            }
            result = scan_host(ip) => {
                // Process result
            }
        }
    }

    Ok(())
}

#[tauri::command]
fn cancel_scan(state: State<'_, ScanState>) -> Result<(), String> {
    if let Some(token) = state.cancel_token.lock().unwrap().as_ref() {
        token.cancel();
        Ok(())
    } else {
        Err("No active scan".to_string())
    }
}
```

### Pattern 6: Port Preset Management
**What:** Define common port lists for Simple, Full, AI Ports, Custom presets (PORT-02, PORT-03, PORT-04, PORT-05)
**When to use:** User selects scan preset or inputs custom ports
**Example:**
```rust
const TOP_100_PORTS: &[u16] = &[
    21, 22, 23, 25, 53, 80, 110, 111, 135, 139, 143, 443, 445, 993, 995,
    1723, 3306, 3389, 5900, 8080, // Add top 100 from nmap
];

const AI_PORTS: &[u16] = &[
    11434, // Ollama
    1234,  // LM Studio
    8888,  // Jupyter
    8000,  // vLLM
    6006,  // TensorBoard
    5000,  // MLflow
    7860,  // Gradio
    8501,  // Streamlit
];

fn get_port_list(preset: &str, custom: Option<Vec<u16>>) -> Vec<u16> {
    match preset {
        "simple" => TOP_100_PORTS.to_vec(),
        "full" => (1..=65535).collect(),
        "ai_ports" => AI_PORTS.to_vec(),
        "custom" => custom.unwrap_or_default(),
        _ => TOP_100_PORTS.to_vec(),
    }
}
```

### Pattern 7: Service Name Mapping
**What:** Map port numbers to well-known service names (PORT-06)
**When to use:** Displaying open ports with human-readable service names
**Example:**
```rust
// Embed common services - full IANA list can be added later
fn get_service_name(port: u16) -> &'static str {
    match port {
        20 | 21 => "FTP",
        22 => "SSH",
        23 => "Telnet",
        25 => "SMTP",
        53 => "DNS",
        80 => "HTTP",
        110 => "POP3",
        143 => "IMAP",
        443 => "HTTPS",
        445 => "SMB",
        3306 => "MySQL",
        3389 => "RDP",
        5432 => "PostgreSQL",
        6379 => "Redis",
        8080 => "HTTP-Proxy",
        11434 => "Ollama",
        1234 => "LM Studio",
        8888 => "Jupyter",
        8000 => "vLLM",
        6006 => "TensorBoard",
        5000 => "MLflow",
        _ => "Unknown",
    }
}
```

### Anti-Patterns to Avoid
- **Don't use FuturesUnordered for >100 tasks:** Causes CPU spin at 128+ tasks due to tokio cooperation limits, use spawn with semaphore instead
- **Don't emit progress events per-host without batching:** Causes Tauri/WRY memory leaks, batch every 100ms minimum
- **Don't block tokio runtime with DNS lookups:** Use hickory-resolver async, not std::net::lookup_host
- **Don't create unbounded task spawns:** Always use semaphore to limit concurrent connections (50-100 max)
- **Don't forget timeout on network operations:** Wrap all TcpStream::connect and DNS lookups with tokio::time::timeout
- **Don't use Arc<Mutex<T>> for Tauri state:** Tauri wraps state in Arc automatically, use Mutex<T> only

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CIDR parsing and IP iteration | String splitting and bit math | `cidr` crate | Handles edge cases (network/broadcast addresses), IPv6 support, iterator traits |
| Bounded concurrency control | Custom channel-based limiter | `tokio::sync::Semaphore` | Battle-tested, efficient permit system, prevents deadlocks, widely used pattern |
| Graceful cancellation | Boolean flag in Arc<Mutex> | `tokio_util::sync::CancellationToken` | Cooperative cancellation, child token support, integrates with tokio::select! |
| Async DNS resolution | std::net::lookup_host or sync dns-lookup | `hickory-resolver` | Non-blocking, configurable timeouts, handles DNS failures gracefully |
| Service name mapping | Hardcoded match statement | IANA services database (embedded or parsed) | Covers 15,000+ services, standardized names, can be updated |

**Key insight:** Network programming has subtle edge cases (timeout handling, resource limits, DNS quirks, CIDR boundary conditions). Use proven libraries that handle these details correctly rather than reimplementing.

## Common Pitfalls

### Pitfall 1: Socket Exhaustion from Unbounded Concurrency
**What goes wrong:** Spawning too many concurrent TCP connections exhausts available ephemeral ports, causing "cannot create socket" errors and false negatives (closed ports reported instead of errors)
**Why it happens:** Each thread uses ~5 ports/sec, at 200ms response time one thread = ~300 ports/60s, Windows has ~64K ephemeral ports but OS reserves many, limit is ~93 threads before exhaustion
**How to avoid:**
- Use tokio::sync::Semaphore with 50-100 permits maximum
- Calculate: max_threads = available_ports / (ports_per_second * average_response_time)
- For /24 subnet (254 hosts), 50-100 concurrent tasks is sufficient
- Monitor for connection errors, reduce concurrency dynamically if errors spike
**Warning signs:** "Cannot create socket" errors, intermittent scan failures, system becomes unresponsive, fewer ports detected than expected

### Pitfall 2: Blocking DNS Lookups Starve Tokio Runtime
**What goes wrong:** Using std::net::lookup_host or synchronous DNS libraries blocks tokio runtime threads, causing all async operations to stall
**Why it happens:** DNS lookups can take seconds on slow/failing DNS servers, blocking operation on async thread starves event loop
**How to avoid:**
- Use hickory-resolver (async DNS library) for all reverse lookups
- Wrap DNS calls with tokio::time::timeout (2 second max)
- Never use std::net functions on tokio runtime
- If must use blocking DNS library, use tokio::task::spawn_blocking
**Warning signs:** High CPU but low network utilization, some scans fast while others stall, uneven progress across targets

### Pitfall 3: Event Memory Leaks from High-Frequency Emission
**What goes wrong:** Emitting Tauri events for every scanned host causes WebView memory growth (1GB+ after large scans), eventual OOM crash
**Why it happens:** Documented Tauri/WRY issue - event callbacks accumulate in window object without cleanup mechanism
**How to avoid:**
- Use Tauri channels (not events) for progress streaming
- Batch progress updates: emit every 100ms, not per-host
- Frontend: don't store all events in React state, only current progress
- Clear/reset state after scan completes
- Limit result set size, use pagination for 500+ results
**Warning signs:** Memory usage grows continuously during scan, UI becomes sluggish after multiple scans, WebView crashes

### Pitfall 4: FuturesUnordered Hangs at 128+ Tasks
**What goes wrong:** Using FuturesUnordered with >128 concurrent futures causes 100% CPU spin, no progress
**Why it happens:** Tokio runtime cooperation limits - FuturesUnordered polls all futures in sequence, hits yield point at 128 tasks
**How to avoid:**
- Don't use FuturesUnordered for main scan loop
- Use tokio::spawn with Semaphore for concurrency control
- If using FuturesUnordered, keep <100 tasks maximum
- Alternative: use futures-buffered crate (FuturesUnorderedBounded)
**Warning signs:** Scan starts then stops progressing, CPU pegged at 100%, no network activity after certain point

### Pitfall 5: Missing Timeout on Network Operations
**What goes wrong:** TCP connections or DNS lookups hang indefinitely on unresponsive hosts, scan never completes
**Why it happens:** Default TcpStream::connect has no timeout, DNS queries can wait minutes
**How to avoid:**
- Wrap all TcpStream::connect with tokio::time::timeout (configurable, default 200ms-1s)
- Wrap all DNS lookups with timeout (2 second max)
- Make timeout configurable via CONF-01
- Log timeout events separately from connection refused
**Warning signs:** Scan hangs on certain hosts, progress stalls, scan time far exceeds expected duration

### Pitfall 6: CIDR Parsing Edge Cases
**What goes wrong:** Custom CIDR parser fails on network/broadcast addresses, non-standard masks, IPv6, input validation
**Why it happens:** Developers parse string manually without accounting for CIDR standard edge cases
**How to avoid:**
- Use cidr crate FromStr trait for parsing
- Validate input before parsing (reject invalid CIDR)
- Handle /32 (single IP) and /31 (point-to-point) correctly
- Skip network address (X.X.X.0) and broadcast (X.X.X.255) if scanning /24
**Warning signs:** Parser accepts invalid CIDR notation, generates wrong IP count, includes network/broadcast addresses incorrectly

### Pitfall 7: Shared State Race Conditions in Cancellation
**What goes wrong:** Race condition between starting scan and cancelling causes panic or incorrect cancellation
**Why it happens:** Scan start and cancel commands run concurrently, shared state accessed without proper synchronization
**How to avoid:**
- Use CancellationToken (not boolean flag) for cancellation
- Store token in Mutex for thread-safe access
- Check is_cancelled() before each host scan
- Use tokio::select! for mid-operation cancellation
- Clear token after scan completes
**Warning signs:** Cancel command sometimes has no effect, scan continues after cancel, panic in state access

## Code Examples

Verified patterns from official sources and proven libraries:

### Parse CIDR and Iterate IPs
```rust
// Source: https://docs.rs/cidr
use cidr::Ipv4Cidr;

fn parse_and_iterate(range: &str) -> Result<Vec<String>, String> {
    let cidr: Ipv4Cidr = range.parse()
        .map_err(|e| format!("Invalid CIDR: {}", e))?;

    let ips: Vec<String> = cidr.iter()
        .map(|inet| inet.address().to_string())
        .collect();

    Ok(ips)
}
```

### Bounded Concurrent TCP Scan
```rust
// Source: https://docs.rs/tokio/latest/tokio/sync/struct.Semaphore.html
use tokio::sync::Semaphore;
use tokio::net::TcpStream;
use tokio::time::{timeout, Duration};
use std::sync::Arc;

async fn scan_port(host: String, port: u16, timeout_ms: u64) -> Option<u16> {
    let addr = format!("{}:{}", host, port);
    match timeout(Duration::from_millis(timeout_ms), TcpStream::connect(addr)).await {
        Ok(Ok(_)) => Some(port),
        _ => None,
    }
}

async fn scan_ports(host: String, ports: Vec<u16>, concurrency: usize) -> Vec<u16> {
    let sem = Arc::new(Semaphore::new(concurrency));
    let mut tasks = Vec::new();

    for port in ports {
        let sem_clone = sem.clone();
        let host_clone = host.clone();

        let task = tokio::spawn(async move {
            let _permit = sem_clone.acquire_owned().await.unwrap();
            scan_port(host_clone, port, 500).await
        });

        tasks.push(task);
    }

    let mut open_ports = Vec::new();
    for task in tasks {
        if let Ok(Some(port)) = task.await {
            open_ports.push(port);
        }
    }

    open_ports
}
```

### Async Reverse DNS
```rust
// Source: https://github.com/hickory-dns/hickory-dns
use hickory_resolver::TokioAsyncResolver;
use std::net::IpAddr;

async fn get_hostname(ip: IpAddr) -> Option<String> {
    let resolver = TokioAsyncResolver::tokio_from_system_conf().ok()?;

    tokio::time::timeout(
        Duration::from_secs(2),
        resolver.reverse_lookup(ip)
    )
    .await
    .ok()?
    .ok()?
    .iter()
    .next()
    .map(|name| name.to_string())
}
```

### Tauri Channel Progress Streaming
```rust
// Source: https://v2.tauri.app/develop/calling-frontend/
use tauri::ipc::Channel;
use serde::Serialize;

#[derive(Clone, Serialize)]
struct Progress {
    scanned: usize,
    total: usize,
    current_ip: String,
}

#[tauri::command]
async fn scan_with_progress(
    range: String,
    on_progress: Channel<Progress>,
) -> Result<(), String> {
    let ips = parse_cidr(&range)?;
    let total = ips.len();

    for (idx, ip) in ips.iter().enumerate() {
        // Scan logic here...

        // Emit progress
        on_progress.send(Progress {
            scanned: idx + 1,
            total,
            current_ip: ip.to_string(),
        }).ok();
    }

    Ok(())
}
```

### Cancellable Scan
```rust
// Source: https://docs.rs/tokio-util/latest/tokio_util/sync/struct.CancellationToken.html
use tokio_util::sync::CancellationToken;

async fn cancellable_scan(ips: Vec<String>, cancel_token: CancellationToken) {
    for ip in ips {
        if cancel_token.is_cancelled() {
            return; // Exit gracefully
        }

        tokio::select! {
            _ = cancel_token.cancelled() => {
                return; // Mid-operation cancel
            }
            result = scan_host(ip) => {
                // Process result
            }
        }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| trust-dns | hickory-dns (0.24+) | 2024 rebrand | Same functionality, new organization, hickory-resolver replaces trust-dns-resolver |
| FuturesUnordered for all concurrency | Spawn with Semaphore | 2022+ best practice | Avoids 128-task hang, better backpressure control |
| Tauri Events for streaming | Tauri Channels | Tauri 2.0 (2024) | Solves memory leak issues, designed for high-throughput streaming |
| Manual Arc<Mutex<T>> for state | Tauri State management | Tauri 1.0+ | Automatic Arc wrapping, simpler API, less error-prone |
| Blocking DNS (std::net) | Async DNS (hickory) | Async/await era | Non-blocking, integrates with tokio, better performance |

**Deprecated/outdated:**
- **trust-dns-resolver**: Use hickory-resolver (same functionality, rebranded)
- **FuturesUnordered for large task sets**: Use spawn + Semaphore for >100 tasks
- **Tauri Events for progress streaming**: Use Channels to avoid memory leaks
- **futures::future::join_all**: Unbounded concurrency, use Semaphore pattern instead

## Open Questions

1. **Port Service Name Database**
   - What we know: IANA maintains official service-names-port-numbers.csv, covers 15,000+ services
   - What's unclear: Best approach - embed common 100-200 services vs parse full IANA CSV at build time vs runtime lookup
   - Recommendation: Start with hardcoded top 100 services + AI ports (sufficient for Phase 2), defer full IANA parsing to Phase 3

2. **Optimal Concurrency Limits**
   - What we know: Pitfalls research shows ~93 threads at 200ms response time before exhaustion, socket exhaustion is platform-dependent
   - What's unclear: Windows-specific limits with Npcap, actual production values for /24 scans
   - Recommendation: Start conservative (50 concurrent), make configurable (CONF-02), provide adaptive mode in Phase 3

3. **Progress Batching Frequency**
   - What we know: Tauri events cause memory leaks with high frequency, channels are better but still need batching
   - What's unclear: Optimal batch interval (50ms vs 100ms vs 200ms), tradeoff between UI responsiveness and memory
   - Recommendation: Start with 100ms batching, monitor memory usage, make configurable if needed

4. **Ping Sweep vs Port Scan Integration**
   - What we know: DISC-01 requires ping sweep, PORT-01 requires port scan on discovered hosts
   - What's unclear: Should ping sweep and port scan be separate commands or single integrated scan workflow
   - Recommendation: Separate commands for flexibility (user may want ping-only or port-only), orchestrate in frontend

## Sources

### Primary (HIGH confidence)
- [cidr crate documentation](https://docs.rs/cidr) - CIDR parsing and IP iteration
- [tokio::sync::Semaphore](https://docs.rs/tokio/latest/tokio/sync/struct.Semaphore.html) - Bounded concurrency control
- [tokio_util::sync::CancellationToken](https://docs.rs/tokio-util/latest/tokio_util/sync/struct.CancellationToken.html) - Graceful cancellation
- [Tauri Channels](https://v2.tauri.app/develop/calling-frontend/) - Real-time progress streaming
- [hickory-dns GitHub](https://github.com/hickory-dns/hickory-dns) - Async DNS resolver
- [Tokio Documentation](https://tokio.rs/) - Async runtime patterns

### Secondary (MEDIUM confidence)
- [RustScan Architecture](https://github.com/bee-san/RustScan) - Real-world port scanner implementation patterns
- [async-port-scanner](https://github.com/bparli/port-scanner) - Batched scanning approach
- [FuturesUnordered issues](https://github.com/tokio-rs/tokio/discussions/4514) - 128-task hang documentation
- [Tauri State Management](https://v2.tauri.app/develop/state-management/) - Shared state patterns
- [Common Port Numbers](https://www.stationx.net/common-ports-cheat-sheet/) - Service name mapping reference
- [IANA Service Names Database](https://www.iana.org/assignments/service-names-port-numbers) - Official port/service registry

### Tertiary (LOW confidence, marked for validation)
- WebSearch results for bounded concurrency patterns (verify with tokio docs in implementation)
- Community discussions on scan optimization (validate performance claims through testing)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries well-documented, actively maintained, official tokio ecosystem
- Architecture: MEDIUM-HIGH - Patterns verified via official docs and existing projects (RustScan), need implementation validation
- Concurrency limits: MEDIUM - Socket exhaustion values from pitfalls research, need Windows-specific testing
- Progress streaming: HIGH - Tauri channel pattern documented, memory leak issue well-known
- Pitfalls: HIGH - Documented issues (FuturesUnordered hang, Tauri event leaks, socket exhaustion)

**Research date:** 2026-02-10
**Valid until:** ~30 days (tokio 1.43 LTS until March 2026, stack stable, hickory-dns actively maintained)

**Notes:**
- No CONTEXT.md exists for this phase, full research freedom applied
- hickory-resolver reverse DNS examples not in docs, but API supports it (reverse_lookup method exists)
- Port service name mapping will need manual curation or IANA CSV parsing (no dedicated Rust crate found)
- Socket exhaustion limits are theoretical (from pitfalls research), need empirical testing on Windows with Npcap
- Tauri channel batching frequency (100ms) is recommendation, not official guidance - may need tuning
