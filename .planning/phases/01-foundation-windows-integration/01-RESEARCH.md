# Phase 1: Foundation & Windows Integration - Research

**Researched:** 2026-02-09
**Domain:** Tauri 2.x desktop application development with Windows-specific platform integration
**Confidence:** MEDIUM-HIGH

## Summary

Phase 1 establishes a Tauri 2.x desktop application using React + TypeScript + Vite for the frontend and Rust for the backend. The critical Windows-specific challenges are: detecting and guiding users on Npcap installation (required for raw socket access), detecting Windows Firewall ICMP blocking, requesting admin privileges for ICMP operations, and auto-detecting the local network subnet for IP range pre-filling.

Tauri 2 provides excellent Windows support with modern tooling (create-tauri-app scaffolding, WebView2 bundling, NSIS/WiX installers). The Rust ecosystem offers mature libraries for all requirements: surge-ping for async ICMP, socket2 for raw sockets, tokio for async runtime, ipconfig for Windows network adapter enumeration, and is_elevated for admin privilege detection. The primary architectural concern is proper event handler cleanup to prevent memory leaks (documented Tauri pitfall with continuous event emission).

**Primary recommendation:** Use create-tauri-app for project scaffolding, implement Windows-specific detection logic as Tauri commands in Rust, communicate results to React frontend via invoke API, and provide user-friendly guidance dialogs when platform requirements (Npcap, firewall, admin) are not met.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Tauri | 2.10+ | Desktop application framework | Official framework, small binaries (~5-10MB), WebView2 integration, mature Windows support |
| React | 19.x | Frontend UI framework | Chosen in project decisions, excellent TypeScript support, large ecosystem |
| TypeScript | 5.x | Type-safe JavaScript | Industry standard for maintainable frontend code, Tauri command type safety |
| Vite | 5.x | Frontend build tool | Fast dev server, HMR, recommended by Tauri for React projects |
| tokio | 1.43+ (LTS) | Async runtime for Rust | Most widely used async runtime, required by surge-ping, LTS until March 2026 |
| surge-ping | 0.8.4+ | ICMP ping operations | Async-first design with tokio, wraps socket2, handles IPv4/IPv6, 2.5M+ downloads |
| socket2 | Latest | Low-level socket operations | Rust standard for raw sockets, Windows support via windows-sys, used by surge-ping |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ipconfig | 0.3.2+ | Windows network adapter info | Enumerate network interfaces, get IP addresses, DNS servers, gateways on Windows |
| is_elevated | Latest | Admin privilege detection | Check if process is running elevated (admin) on Windows |
| serde/serde_json | Latest | JSON serialization | Tauri command data exchange between Rust and JavaScript |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Tauri | Electron | Electron: 100MB+ bundles vs Tauri's ~5-10MB, slower startup, higher memory usage |
| surge-ping | fastping-rs, pnet | surge-ping has better async integration and active maintenance |
| ipconfig | local-ip-address, getifaddrs | ipconfig is Windows-specific with gateway support, others are cross-platform but less detailed |

**Installation:**
```bash
# Frontend dependencies
npm create tauri-app@latest
# Follow prompts: React + TypeScript + Vite

# Rust dependencies (add to src-tauri/Cargo.toml)
[dependencies]
tauri = "2"
tokio = { version = "1.43", features = ["full"] }
surge-ping = "0.8"
socket2 = "0.5"
ipconfig = "0.3"
is_elevated = "0.1"
serde = { version = "1", features = ["derive"] }
serde_json = "1"
```

## Architecture Patterns

### Recommended Project Structure
```
Scanner/
├── package.json
├── index.html
├── vite.config.ts
├── src/                      # React frontend
│   ├── main.tsx              # Entry point
│   ├── App.tsx               # Root component
│   ├── components/           # UI components
│   ├── hooks/                # Custom React hooks
│   ├── services/             # Tauri command wrappers
│   └── types/                # TypeScript types
└── src-tauri/                # Rust backend
    ├── tauri.conf.json       # Tauri configuration
    ├── Cargo.toml            # Rust dependencies
    ├── build.rs              # Build script
    ├── icons/                # App icons
    ├── capabilities/         # Tauri permissions
    └── src/
        ├── main.rs           # Desktop entry point
        ├── lib.rs            # App logic and commands
        ├── commands/         # Tauri command modules
        │   ├── mod.rs
        │   ├── platform.rs   # Platform detection
        │   └── network.rs    # Network operations
        └── utils/            # Helper modules
```

### Pattern 1: Tauri Command Pattern
**What:** Expose Rust functions to frontend via `#[tauri::command]` attribute
**When to use:** All backend operations (platform checks, network scanning, privileged operations)
**Example:**
```rust
// Source: https://v2.tauri.app/develop/calling-rust/
#[tauri::command]
async fn check_npcap_installed() -> Result<bool, String> {
    // Check C:\Program Files\Npcap\NPFInstall.exe
    let npcap_path = std::path::Path::new(r"C:\Program Files\Npcap\NPFInstall.exe");
    Ok(npcap_path.exists())
}

// In lib.rs
fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![check_npcap_installed])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
```

**Frontend invocation:**
```typescript
// Source: https://v2.tauri.app/develop/calling-rust/
import { invoke } from '@tauri-apps/api/core';

const isNpcapInstalled = await invoke<boolean>('check_npcap_installed');
```

### Pattern 2: Async Commands for I/O Operations
**What:** Use async Rust functions for network operations, executed on tokio thread pool
**When to use:** Network scanning, file I/O, any blocking operations
**Example:**
```rust
// Source: https://v2.tauri.app/develop/calling-rust/
#[tauri::command]
async fn detect_local_subnet() -> Result<String, String> {
    use ipconfig::get_adapters;

    let adapters = get_adapters()
        .map_err(|e| format!("Failed to get adapters: {}", e))?;

    for adapter in adapters {
        if !adapter.ip_addresses().is_empty() {
            let ip = &adapter.ip_addresses()[0];
            // Calculate /24 subnet from first IP
            return Ok(format!("{}.0/24", ip.to_string().rsplitn(2, '.').nth(1).unwrap()));
        }
    }

    Err("No network adapters found".to_string())
}
```

### Pattern 3: Platform Detection on Startup
**What:** Run platform checks when app loads, display warnings/guidance if requirements not met
**When to use:** App initialization, before allowing network operations
**Example:**
```typescript
// React hook for platform checks
export function usePlatformChecks() {
  const [checks, setChecks] = useState({
    npcap: false,
    firewall: false,
    elevated: false,
    loading: true
  });

  useEffect(() => {
    async function runChecks() {
      const [npcap, firewall, elevated] = await Promise.all([
        invoke<boolean>('check_npcap_installed'),
        invoke<boolean>('check_firewall_blocks_icmp'),
        invoke<boolean>('is_elevated')
      ]);
      setChecks({ npcap, firewall, elevated, loading: false });
    }
    runChecks();
  }, []);

  return checks;
}
```

### Anti-Patterns to Avoid
- **Don't forget event cleanup:** Tauri event listeners MUST be unlistened on component unmount to prevent memory leaks (major documented issue)
- **Don't run blocking operations in async context without spawn_blocking:** Will block tokio runtime threads
- **Don't emit events continuously without batching:** Causes memory growth in WebView (use channels or batching strategies)
- **Don't assume WebView2 is installed:** Tauri handles this, but older Windows 10 versions may need it
- **Don't skip admin check before ICMP operations:** Raw sockets on Windows require admin privileges

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Network interface enumeration | Parse ipconfig output | `ipconfig` crate | Handles encoding issues, adapter types, IPv4/IPv6, DNS servers, gateways reliably |
| Admin privilege detection | Check registry or user groups | `is_elevated` crate | Correctly checks TOKEN_ELEVATION_TYPE via Windows API, handles integrity levels |
| ICMP ping implementation | Raw socket + packet building | `surge-ping` crate | Handles socket creation, ICMP packet building, async/await, timeout handling, IPv4/IPv6 |
| Windows Firewall rule queries | Parse netsh output | Execute PowerShell Get-NetFirewallRule | Structured output, handles localization, reliable rule detection |
| Npcap version detection | File version parsing | Check file existence + pcap_lib_version | Version string format changes, registry keys vary by Npcap version |

**Key insight:** Windows platform APIs are complex with edge cases (localization, encoding, privilege contexts, version differences). Use battle-tested crates that abstract these details.

## Common Pitfalls

### Pitfall 1: Event Listener Memory Leaks
**What goes wrong:** Continuous event emission from Rust to React frontend causes memory growth and crashes
**Why it happens:** Tauri stores event callbacks in window object without cleanup mechanism, particularly with Channel API
**How to avoid:**
- Always store unlisten function from event listeners
- Clean up in useEffect cleanup or component unmount
- For channels, delete onmessage property when component unmounts
- Avoid high-frequency event emission (>10/sec sustained)
**Warning signs:** Memory usage grows over time, app becomes sluggish, WebView crashes after extended use

**Example:**
```typescript
// CORRECT: Cleanup listener
useEffect(() => {
  const unlisten = await listen('scan-progress', (event) => {
    setProgress(event.payload);
  });

  return () => {
    unlisten(); // CRITICAL: cleanup on unmount
  };
}, []);

// WRONG: No cleanup
useEffect(() => {
  listen('scan-progress', (event) => setProgress(event.payload));
}, []); // Memory leak on every mount/unmount cycle
```

### Pitfall 2: Admin Privileges Not Requested Before ICMP
**What goes wrong:** surge-ping fails with "permission denied" errors on Windows when not elevated
**Why it happens:** Windows requires admin privileges for raw socket access (ICMP) since XP SP2
**How to avoid:**
- Check elevation status with `is_elevated::is_elevated()` before ICMP operations
- Display warning dialog if not elevated, guide user to restart app as admin
- Consider adding manifest for UAC prompt on startup (optional, intrusive UX)
**Warning signs:** ICMP operations fail immediately with permission errors, surge-ping socket creation returns errors

### Pitfall 3: Missing Npcap Detection
**What goes wrong:** Raw socket operations fail silently or with cryptic errors
**Why it happens:** Windows raw socket support requires Npcap (or legacy WinPcap) driver installation
**How to avoid:**
- Check `C:\Program Files\Npcap\NPFInstall.exe` exists on startup
- Display download link (https://npcap.com/) and installation instructions if missing
- Verify npcap service is running via service manager query
**Warning signs:** Socket creation succeeds but operations timeout, "no such device" errors

### Pitfall 4: Windows Firewall Blocks ICMP by Default
**What goes wrong:** ICMP Echo Requests are blocked by Windows Firewall on private/public networks, ping operations timeout
**Why it happens:** Default Windows Firewall policy blocks inbound ICMP
**How to avoid:**
- Detect firewall ICMP blocking via PowerShell: `Get-NetFirewallRule -DisplayName "File and Printer Sharing (Echo Request - ICMPv4-In)" | Select-Object Enabled`
- Warn user with instructions to enable firewall rule or add exception
- Provide button to open firewall settings (Windows Settings deep link)
**Warning signs:** Outbound pings work but no responses received, 100% packet loss

### Pitfall 5: Blocking Operations in Async Context
**What goes wrong:** Entire app freezes, UI becomes unresponsive, async operations block each other
**Why it happens:** Tokio runtime threads blocked by synchronous I/O or long-running CPU work
**How to avoid:**
- Wrap blocking operations with `tokio::task::spawn_blocking()`
- Use async file I/O (`tokio::fs` instead of `std::fs`)
- Keep hot loops short, use `tokio::time::sleep()` for delays, not `std::thread::sleep()`
- Limit concurrent tasks (use semaphore or bounded channels)
**Warning signs:** High CPU on single core, other async tasks don't progress, UI freezes during backend operations

## Code Examples

Verified patterns from official sources and crate documentation:

### Check Admin Elevation (is_elevated)
```rust
// Source: https://docs.rs/is_elevated
use is_elevated::is_elevated;

#[tauri::command]
fn check_admin_privileges() -> bool {
    is_elevated()
}
```

### Detect Local Network Interfaces (ipconfig)
```rust
// Source: https://docs.rs/ipconfig/latest/ipconfig/
use ipconfig::get_adapters;

#[tauri::command]
async fn get_local_subnet() -> Result<String, String> {
    let adapters = get_adapters()
        .map_err(|e| format!("Failed to get adapters: {}", e))?;

    for adapter in adapters {
        let ips = adapter.ip_addresses();
        if !ips.is_empty() && ips[0].is_ipv4() {
            // Extract first 3 octets for /24 subnet
            let ip_str = ips[0].to_string();
            let parts: Vec<&str> = ip_str.split('.').collect();
            if parts.len() == 4 {
                return Ok(format!("{}.{}.{}.0/24", parts[0], parts[1], parts[2]));
            }
        }
    }

    Err("No suitable network adapter found".to_string())
}
```

### ICMP Ping with surge-ping
```rust
// Source: https://github.com/kolapapa/surge-ping (README)
use surge_ping::ping;
use std::net::IpAddr;

#[tauri::command]
async fn ping_host(ip: String) -> Result<u64, String> {
    let addr: IpAddr = ip.parse()
        .map_err(|e| format!("Invalid IP: {}", e))?;

    let payload = [0u8; 8];

    let (packet, duration) = ping(addr, &payload)
        .await
        .map_err(|e| format!("Ping failed: {}", e))?;

    Ok(duration.as_millis() as u64)
}
```

### Check Npcap Installation
```rust
// Source: https://npcap.com/guide/npcap-devguide.html
use std::path::Path;

#[tauri::command]
fn check_npcap_installed() -> bool {
    Path::new(r"C:\Program Files\Npcap\NPFInstall.exe").exists()
}
```

### Windows Firewall ICMP Check (PowerShell)
```rust
// Source: https://learn.microsoft.com/en-us/powershell/module/netsecurity/get-netfirewallrule
use std::process::Command;

#[tauri::command]
async fn check_firewall_blocks_icmp() -> Result<bool, String> {
    let output = Command::new("powershell")
        .args(&[
            "-Command",
            "Get-NetFirewallRule -DisplayName 'File and Printer Sharing (Echo Request - ICMPv4-In)' | Select-Object -ExpandProperty Enabled"
        ])
        .output()
        .map_err(|e| format!("PowerShell execution failed: {}", e))?;

    let stdout = String::from_utf8_lossy(&output.stdout);

    // If rule is disabled (False) or not found, firewall blocks ICMP
    let blocks_icmp = !stdout.trim().eq_ignore_ascii_case("True");
    Ok(blocks_icmp)
}
```

### Tauri Command Registration
```rust
// Source: https://v2.tauri.app/develop/calling-rust/
#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            check_admin_privileges,
            check_npcap_installed,
            check_firewall_blocks_icmp,
            get_local_subnet,
            ping_host
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Electron for desktop apps | Tauri 2.x | 2019-2024 | 95% smaller binaries, faster startup, lower memory, native Rust performance |
| WinPcap | Npcap | 2016+ | Npcap actively maintained, Windows 10/11 support, loopback capture, better API |
| WinAPI raw bindings | windows-rs/windows-sys | 2021+ | Type-safe Windows API access, better ergonomics, maintained by Microsoft |
| Callback-based async | async/await (tokio) | Rust 1.39+ (2019) | Cleaner async code, better composability, easier error handling |
| fastping-rs | surge-ping | 2021+ | Better async integration, maintained, uses modern socket2 |

**Deprecated/outdated:**
- **WinPcap**: Discontinued, use Npcap (drop-in replacement)
- **winapi crate**: Use windows-rs/windows-sys (official Microsoft crates)
- **tauri v1**: Use tauri v2 (mobile support, improved security, better performance)

## Open Questions

1. **UAC Manifest Configuration**
   - What we know: Can embed manifest with `rust-embed-resource` or `winres` crate, set `requestedExecutionLevel` to `requireAdministrator`
   - What's unclear: UX impact (always prompts for UAC on launch), Microsoft Store compatibility, whether conditional elevation is better
   - Recommendation: Start with conditional elevation (check on-demand, prompt user to restart as admin), avoid always-elevated for better UX

2. **Windows Defender/Antivirus False Positives**
   - What we know: Network scanning behavior triggers heuristics, mentioned in STATE.md blockers
   - What's unclear: Which specific Tauri APIs trigger alerts, whether code signing certificate prevents this, submission process for Microsoft Defender SmartScreen
   - Recommendation: Plan for Phase 5 (Polish), research code signing requirements, prepare documentation for users about expected warnings

3. **Npcap License for Distribution**
   - What we know: Npcap is free for personal use, commercial redistribution requires OEM license
   - What's unclear: Scanner's distribution model (free/paid), whether bundling Npcap installer is needed or just detection/download link
   - Recommendation: Use detection + download link approach for now (no redistribution), defer licensing decision to distribution phase

## Sources

### Primary (HIGH confidence)
- [Tauri v2 Official Documentation](https://v2.tauri.app/) - Project structure, configuration, command patterns
- [surge-ping GitHub README](https://github.com/kolapapa/surge-ping) - ICMP implementation, requirements
- [Npcap Developer's Guide](https://npcap.com/guide/npcap-devguide.html) - Detection methods, version checking
- [is_elevated crate docs](https://docs.rs/is_elevated) - Admin privilege detection API
- [ipconfig crate docs](https://docs.rs/ipconfig/latest/ipconfig/) - Network adapter enumeration
- [socket2 crate docs](https://docs.rs/socket2) - Raw socket operations
- [tokio official docs](https://tokio.rs/) - Async runtime best practices

### Secondary (MEDIUM confidence)
- [Tauri GitHub Discussions #4201](https://github.com/tauri-apps/tauri/discussions/4201) - UAC elevation patterns (community solutions)
- [Tauri Issue #12388](https://github.com/tauri-apps/tauri/issues/12388) - Event memory leak documentation improvements
- [Microsoft Learn - Get-NetFirewallRule](https://learn.microsoft.com/en-us/powershell/module/netsecurity/get-netfirewallrule) - PowerShell firewall detection
- [Windows Firewall ICMP Configuration](https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/create-an-inbound-icmp-rule) - Default ICMP blocking behavior

### Tertiary (LOW confidence, needs validation)
- Community forum discussions about Tauri + React project structures
- WebSearch results for Windows API patterns (verify with official Windows docs in implementation)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries well-documented, actively maintained, official recommendations
- Architecture: HIGH - Tauri patterns documented, React + Vite + TypeScript is standard approach
- Windows detection: MEDIUM-HIGH - APIs verified via official docs, implementation patterns found but need testing
- Pitfalls: HIGH - Event memory leaks are documented issues with GitHub issues/discussions confirming

**Research date:** 2026-02-09
**Valid until:** ~30 days (stack is stable, Tauri 2 mature, tokio LTS until March 2026)

**Notes:**
- No CONTEXT.md exists for this phase, full research freedom applied
- surge-ping documentation lacks Windows-specific privilege requirements detail - assumes knowledge of OS raw socket restrictions
- Windows Firewall detection via PowerShell is reliable but requires localized rule names may vary on non-English Windows
- Admin privilege requirement for ICMP is OS-level, not library-specific - all ICMP libraries on Windows will have this constraint
