# Architecture Research: Network Scanner

**Domain:** Network scanning tool with Tauri + React
**Researched:** 2026-02-08
**Confidence:** HIGH

## Recommended Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      REACT FRONTEND (src/)                      │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ ScanControl  │  │ ResultsTable │  │ StatusPanel  │          │
│  │  Component   │  │  Component   │  │  Component   │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
│         │                 │                 │                   │
│         └────────┬────────┴─────────────────┘                   │
│                  │                                               │
│  ┌───────────────▼────────────────────────────────────┐         │
│  │          TypeScript API Layer (hooks)              │         │
│  │  - useScanResults() - tracks streaming updates     │         │
│  │  - useScanControl() - start/stop/pause operations  │         │
│  └───────────────┬────────────────────────────────────┘         │
├──────────────────┼──────────────────────────────────────────────┤
│                  │      IPC BOUNDARY                            │
│            (invoke/channels/events)                             │
├──────────────────┼──────────────────────────────────────────────┤
│                  │     RUST BACKEND (src-tauri/)                │
├──────────────────▼──────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────┐           │
│  │           Commands Layer (commands/)             │           │
│  │  - scan_commands.rs  - start/stop/pause          │           │
│  │  - export_commands.rs - save results             │           │
│  └──────────────────┬───────────────────────────────┘           │
│                     │                                            │
│  ┌──────────────────▼───────────────────────────────┐           │
│  │        Scanning Engine (scanning/)               │           │
│  │  ┌──────────────┐  ┌──────────────┐             │           │
│  │  │ PingScan     │  │ PortScan     │  ┌────────┐ │           │
│  │  │   Module     │  │   Module     │  │ShadowAI│ │           │
│  │  └──────┬───────┘  └──────┬───────┘  │ Module │ │           │
│  │         │                 │           └────┬───┘ │           │
│  │         └────────┬────────┴────────────────┘     │           │
│  │                  │                                │           │
│  │         ┌────────▼────────┐                       │           │
│  │         │  TaskScheduler  │                       │           │
│  │         │ (Tokio Runtime) │                       │           │
│  │         │  - Semaphore    │                       │           │
│  │         │  - Channels     │                       │           │
│  │         └─────────────────┘                       │           │
│  └───────────────────────────────────────────────────┘           │
│                     │                                            │
│  ┌──────────────────▼───────────────────────────────┐           │
│  │         Shared State (state/)                    │           │
│  │  - ScanState (Mutex<ScanProgress>)               │           │
│  │  - ResultsStore (Mutex<Vec<ScanResult>>)         │           │
│  └──────────────────────────────────────────────────┘           │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| **React Components** | UI rendering, user interactions, display state | Presentational components with hooks |
| **TypeScript Hooks** | State management, API calls, event subscriptions | Custom hooks wrapping Tauri invoke/events |
| **Commands Layer** | Expose Rust functions to frontend, validate inputs | `#[tauri::command]` annotated functions |
| **Scanning Engine** | Execute scans concurrently, manage network I/O | Async Rust with Tokio runtime |
| **TaskScheduler** | Limit concurrency, coordinate parallel tasks | Tokio Semaphore + channels |
| **Shared State** | Track scan progress, store results | `Mutex<T>` managed by Tauri |

## Recommended Project Structure

```
Scanner/
├── src/                          # React frontend
│   ├── components/               # UI components
│   │   ├── ScanControl.tsx       # Start/stop/configure scans
│   │   ├── ResultsTable.tsx      # Display scan results
│   │   ├── StatusPanel.tsx       # Show progress/status
│   │   └── FilterBar.tsx         # Filter/sort controls
│   ├── hooks/                    # Custom React hooks
│   │   ├── useScanResults.ts     # Subscribe to result updates
│   │   ├── useScanControl.ts     # Control scan lifecycle
│   │   └── useRealTimeUpdates.ts # Handle streaming data
│   ├── types/                    # TypeScript types
│   │   └── scanner.ts            # Shared types (auto-generated)
│   ├── App.tsx                   # Root component
│   └── main.tsx                  # Entry point
│
├── src-tauri/                    # Rust backend
│   ├── src/
│   │   ├── commands/             # Tauri command handlers
│   │   │   ├── mod.rs
│   │   │   ├── scan_commands.rs  # start_scan, stop_scan, pause_scan
│   │   │   └── export_commands.rs # export_results
│   │   ├── scanning/             # Core scanning logic
│   │   │   ├── mod.rs
│   │   │   ├── ping.rs           # Ping sweep implementation
│   │   │   ├── port.rs           # Port scanning logic
│   │   │   ├── shadow_ai.rs      # Shadow AI detection
│   │   │   ├── scheduler.rs      # Concurrent task management
│   │   │   └── types.rs          # Scan result types
│   │   ├── state/                # Application state
│   │   │   ├── mod.rs
│   │   │   ├── scan_state.rs     # Current scan progress
│   │   │   └── results.rs        # Result storage
│   │   ├── lib.rs                # Library entry point
│   │   └── main.rs               # Desktop entry point
│   ├── Cargo.toml
│   └── tauri.conf.json
│
├── package.json
└── README.md
```

### Structure Rationale

- **`src/components/`**: Presentational components focused on UI rendering only, following container/presentational pattern
- **`src/hooks/`**: Business logic separation—handles state and Tauri communication, keeps components pure
- **`src-tauri/src/commands/`**: Thin layer exposing Rust functions to frontend, validates inputs, delegates to scanning modules
- **`src-tauri/src/scanning/`**: Core domain logic isolated from IPC concerns, testable independently
- **`src-tauri/src/state/`**: Centralized state management using Tauri's built-in state system with interior mutability

## Architectural Patterns

### Pattern 1: Command + Channel for Streaming Progress

**What:** Commands initiate operations, channels stream incremental updates back to frontend

**When to use:** Any long-running operation that produces incremental results (network scans)

**Trade-offs:** More complex than simple invoke/return, but essential for real-time UX

**Example:**
```typescript
// Frontend hook
import { invoke, Channel } from '@tauri-apps/api/core';

export function useScanResults() {
  const [results, setResults] = useState<ScanResult[]>([]);

  async function startScan(target: string) {
    const channel = new Channel<ScanResult>();

    channel.onmessage = (result) => {
      setResults(prev => [...prev, result]);
    };

    await invoke('start_scan', {
      target,
      onProgress: channel
    });
  }

  return { results, startScan };
}
```

```rust
// Backend command
#[tauri::command]
async fn start_scan(
    target: String,
    on_progress: Channel<ScanResult>,
    state: State<'_, Mutex<ScanState>>
) -> Result<(), String> {
    let state = state.lock().unwrap();

    // Spawn scanning task
    tokio::spawn(async move {
        // For each result
        let result = scan_host(&host).await;
        on_progress.send(result).unwrap();
    });

    Ok(())
}
```

### Pattern 2: Tokio Semaphore for Bounded Concurrency

**What:** Limit number of concurrent network operations to avoid overwhelming network/system

**When to use:** High-volume parallel I/O operations (scanning 254 hosts, 1000+ ports)

**Trade-offs:** Adds coordination overhead, but prevents resource exhaustion and network flooding

**Example:**
```rust
use tokio::sync::Semaphore;
use std::sync::Arc;

async fn scan_network(hosts: Vec<String>, max_concurrent: usize) {
    let semaphore = Arc::new(Semaphore::new(max_concurrent));
    let mut tasks = Vec::new();

    for host in hosts {
        let sem = semaphore.clone();
        let task = tokio::spawn(async move {
            let _permit = sem.acquire().await.unwrap();
            // Scan host - permit auto-released when dropped
            scan_host(&host).await
        });
        tasks.push(task);
    }

    // Wait for all to complete
    for task in tasks {
        task.await.unwrap();
    }
}
```

### Pattern 3: State Management with Interior Mutability

**What:** Use `Mutex<T>` wrapped by Tauri's state management for thread-safe shared state

**When to use:** Any state accessed from multiple commands or background tasks

**Trade-offs:** Lock contention possible, but Tauri handles Arc internally so simple to use

**Example:**
```rust
use std::sync::Mutex;
use tauri::State;

#[derive(Default)]
struct ScanState {
    is_scanning: bool,
    progress: f64,
    total_hosts: usize,
}

// In setup
fn main() {
    tauri::Builder::default()
        .manage(Mutex::new(ScanState::default()))
        .invoke_handler(tauri::generate_handler![start_scan, get_progress])
        .run(tauri::generate_context!())
        .unwrap();
}

// In commands
#[tauri::command]
fn get_progress(state: State<Mutex<ScanState>>) -> f64 {
    let state = state.lock().unwrap();
    state.progress
}
```

### Pattern 4: TypeScript Type Safety with tauri-specta

**What:** Auto-generate TypeScript types from Rust structs to maintain type consistency

**When to use:** Always—eliminates manual type duplication and sync issues

**Trade-offs:** Additional build step, but prevents entire class of runtime errors

**Example:**
```rust
// Rust side
use specta::Type;
use serde::{Serialize, Deserialize};

#[derive(Serialize, Deserialize, Type)]
pub struct ScanResult {
    pub host: String,
    pub port: u16,
    pub status: String,
    pub response_time_ms: u64,
}

// TypeScript types auto-generated
// frontend can import from './types/scanner'
```

### Pattern 5: Container/Presentational Component Split

**What:** Separate stateful logic (containers/hooks) from UI rendering (presentational components)

**When to use:** Always—makes components easier to test, reuse, and reason about

**Trade-offs:** More files, but much better separation of concerns

**Example:**
```typescript
// Presentational component
interface ResultsTableProps {
  results: ScanResult[];
  onSort: (column: string) => void;
  onFilter: (filter: FilterConfig) => void;
}

export function ResultsTable({ results, onSort, onFilter }: ResultsTableProps) {
  // Pure UI rendering, no business logic
  return <table>...</table>;
}

// Container hook
export function useScanResults() {
  const [results, setResults] = useState<ScanResult[]>([]);
  const [sortBy, setSortBy] = useState('host');
  const [filter, setFilter] = useState<FilterConfig>({});

  // All business logic here
  const sortedResults = useMemo(() =>
    sortResults(results, sortBy),
    [results, sortBy]
  );

  return { results: sortedResults, onSort: setSortBy, onFilter: setFilter };
}
```

## Data Flow

### Scan Initiation Flow

```
User clicks "Start Scan"
    ↓
React Component (ScanControl.tsx)
    ↓
Custom Hook (useScanControl.ts)
    ↓
Tauri invoke('start_scan', { target, config })
    ↓
━━━━━━━━━━━━━ IPC BOUNDARY ━━━━━━━━━━━━━
    ↓
Command Handler (scan_commands.rs)
    ↓
Update Shared State (set is_scanning = true)
    ↓
Spawn Tokio Task
    ↓
TaskScheduler creates bounded task pool
    ↓
Scanning Engine executes scans
```

### Real-Time Results Flow

```
Scanning Engine finds open port
    ↓
Create ScanResult struct
    ↓
Send via Channel (on_progress.send(result))
    ↓
━━━━━━━━━━━━━ IPC BOUNDARY ━━━━━━━━━━━━━
    ↓
Channel.onmessage handler
    ↓
Update React state (setResults([...prev, result]))
    ↓
Component re-renders with new result
    ↓
ResultsTable displays updated data
```

### State Query Flow

```
Component needs current progress
    ↓
invoke('get_progress')
    ↓
━━━━━━━━━━━━━ IPC BOUNDARY ━━━━━━━━━━━━━
    ↓
Command acquires state.lock()
    ↓
Returns progress value
    ↓
━━━━━━━━━━━━━ IPC BOUNDARY ━━━━━━━━━━━━━
    ↓
Promise resolves with value
    ↓
Component displays progress
```

### Key Data Flow Principles

1. **Frontend → Backend**: Always via `invoke()` commands with serialized JSON arguments
2. **Backend → Frontend (streaming)**: Via Channels for high-frequency updates
3. **Backend → Frontend (events)**: Via events for low-frequency notifications (scan complete, errors)
4. **Shared State Access**: Always through `Mutex` locks, never direct mutation
5. **Concurrency Control**: Semaphore limits parallel scan tasks to prevent resource exhaustion

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 1-10 hosts | Simple sequential scanning acceptable, minimal concurrency needed |
| 10-100 hosts | Implement semaphore with ~20-50 concurrent tasks, channels for progress |
| 100-500 hosts | Add result batching (send every 10 results vs. individual), consider pagination in UI |
| 500+ hosts | Implement chunked scanning, database for results storage, virtualized table rendering |

### Scaling Priorities

1. **First bottleneck: UI rendering with large result sets**
   - **Symptom:** Table becomes sluggish with 1000+ rows
   - **Fix:** Implement virtual scrolling with `react-window`, only render visible rows
   - **When:** Proactively if expecting >500 results

2. **Second bottleneck: Channel message overhead**
   - **Symptom:** Scan slows down due to frequent IPC messages
   - **Fix:** Batch results—send array of 10-20 results per message instead of individual
   - **When:** If scanning >1000 ports or >100 hosts simultaneously

3. **Third bottleneck: Memory from storing all results**
   - **Symptom:** Application memory grows unbounded
   - **Fix:** Implement result pagination or streaming export, don't keep all in memory
   - **When:** Scans producing >10,000 individual results

4. **Fourth bottleneck: Network flooding**
   - **Symptom:** Scans fail, network becomes unstable
   - **Fix:** Reduce semaphore permits, add rate limiting between packets
   - **When:** User reports network issues or scans timing out

## Anti-Patterns

### Anti-Pattern 1: Blocking the Main Thread

**What people do:** Run network I/O operations without spawning async tasks
```rust
// BAD - blocks the entire app
#[tauri::command]
fn start_scan(target: String) -> Vec<ScanResult> {
    let mut results = Vec::new();
    for port in 1..65535 {
        results.push(scan_port(&target, port)); // Blocks for hours!
    }
    results
}
```

**Why it's wrong:** Freezes the UI, can't cancel, no progress updates

**Do this instead:** Spawn async task, use channels for incremental results
```rust
// GOOD - async with progress updates
#[tauri::command]
async fn start_scan(
    target: String,
    on_progress: Channel<ScanResult>
) -> Result<(), String> {
    tokio::spawn(async move {
        for port in 1..65535 {
            let result = scan_port(&target, port).await;
            on_progress.send(result).unwrap();
        }
    });
    Ok(())
}
```

### Anti-Pattern 2: Unbounded Concurrency

**What people do:** Spawn thousands of tasks without limiting
```rust
// BAD - spawns 65,535 concurrent tasks!
for port in 1..65535 {
    tokio::spawn(async move {
        scan_port(&target, port).await
    });
}
```

**Why it's wrong:** Exhausts file descriptors, overwhelms network, crashes system

**Do this instead:** Use semaphore to limit concurrent tasks
```rust
// GOOD - limits to 50 concurrent scans
let sem = Arc::new(Semaphore::new(50));
for port in 1..65535 {
    let sem = sem.clone();
    tokio::spawn(async move {
        let _permit = sem.acquire().await.unwrap();
        scan_port(&target, port).await
    });
}
```

### Anti-Pattern 3: Using Events for High-Frequency Updates

**What people do:** Emit event for every scan result
```rust
// BAD - hundreds of events per second
for result in scan_results {
    app.emit("scan-result", result).unwrap();
}
```

**Why it's wrong:** Event system not designed for high throughput, creates performance bottleneck

**Do this instead:** Use channels for streaming data
```rust
// GOOD - channels designed for streaming
#[tauri::command]
async fn start_scan(on_progress: Channel<ScanResult>) {
    for result in scan_results {
        on_progress.send(result).unwrap();
    }
}
```

### Anti-Pattern 4: Direct State Mutation

**What people do:** Try to mutate state directly without mutex
```rust
// BAD - won't compile (or causes data races)
#[tauri::command]
fn update_progress(state: State<ScanState>) {
    state.progress = 50.0; // ERROR: can't mutate
}
```

**Why it's wrong:** Rust prevents this at compile time—state is shared across threads

**Do this instead:** Use interior mutability with Mutex
```rust
// GOOD - proper interior mutability
#[tauri::command]
fn update_progress(state: State<Mutex<ScanState>>) {
    let mut state = state.lock().unwrap();
    state.progress = 50.0;
}
```

### Anti-Pattern 5: Mixing Business Logic in UI Components

**What people do:** Put Tauri invoke calls directly in components
```typescript
// BAD - component knows too much
function ScanControl() {
  const [scanning, setScanning] = useState(false);

  async function handleStart() {
    setScanning(true);
    const results = await invoke('start_scan', { target: '192.168.1.0/24' });
    setScanning(false);
  }

  return <button onClick={handleStart}>Start</button>;
}
```

**Why it's wrong:** Hard to test, can't reuse logic, violates separation of concerns

**Do this instead:** Extract to custom hook
```typescript
// GOOD - logic separated from UI
function useScanControl() {
  const [scanning, setScanning] = useState(false);

  async function startScan(target: string) {
    setScanning(true);
    try {
      await invoke('start_scan', { target });
    } finally {
      setScanning(false);
    }
  }

  return { scanning, startScan };
}

// Component just handles UI
function ScanControl() {
  const { scanning, startScan } = useScanControl();
  return <button onClick={() => startScan('192.168.1.0/24')}>Start</button>;
}
```

## Integration Points

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Frontend ↔ Backend | IPC (invoke/channels/events) | All data JSON-serialized, type-safe with tauri-specta |
| Commands ↔ Scanning Engine | Direct function calls | Commands are thin wrappers, delegate to engine |
| Scanning Engine ↔ State | Mutex locks | Short-lived locks to update progress, store results |
| React Components ↔ Hooks | Function calls, React context | Hooks encapsulate all Tauri communication |

### Concurrency Boundaries

| Boundary | Synchronization | Notes |
|----------|-----------------|-------|
| Multiple scan tasks | Tokio Semaphore | Limits concurrent network operations |
| State access from tasks | Mutex<T> | Prevents data races when updating shared state |
| Channel sends | Lock-free queue | Tokio channels handle concurrent sends internally |

## Build Order Recommendations

Based on dependencies between components, recommended build sequence:

### Phase 1: Foundation
1. **Setup Tauri + React project structure**
   - Initialize Tauri with React template
   - Configure TypeScript, install dependencies
   - Verify basic IPC works (hello world command)

2. **Define shared types**
   - Create Rust structs for ScanResult, ScanConfig
   - Setup tauri-specta for type generation
   - Generate TypeScript types

3. **Implement state management**
   - Create `ScanState` and `ResultsStore` structs
   - Register with Tauri state system
   - Add basic get/set commands

### Phase 2: Core Scanning (can parallelize)
4. **Ping sweep module** (independent)
   - Implement ICMP ping logic
   - Test standalone without UI

5. **Port scan module** (independent)
   - Implement TCP connect scanning
   - Test standalone without UI

6. **Shadow AI detection** (independent)
   - Define detection heuristics
   - Test standalone without UI

### Phase 3: Orchestration
7. **Task scheduler**
   - Implement semaphore-based concurrency control
   - Integrate ping/port/AI modules
   - Add progress tracking

8. **Command layer**
   - Create `start_scan` with channel support
   - Create `stop_scan`, `pause_scan`
   - Wire to scanning engine

### Phase 4: Frontend
9. **Custom hooks** (before components)
   - `useScanResults` - subscribe to channels
   - `useScanControl` - start/stop operations

10. **UI components** (after hooks)
    - `ResultsTable` - display results
    - `ScanControl` - start/stop buttons
    - `StatusPanel` - progress display

### Phase 5: Polish
11. **Filtering/sorting**
    - Client-side filtering logic
    - Sort by host/port/status

12. **Export functionality**
    - Export to CSV/JSON
    - Save scan configurations

### Dependency Diagram
```
Foundation → Types → State
                ↓
    Scanning Modules (parallel)
                ↓
         Task Scheduler
                ↓
          Commands
                ↓
        Hooks → Components
```

**Key insight:** Scanning modules can be built and tested independently before integration. Frontend can begin once commands + channels are working, even with mock data.

## Sources

### High Confidence (Official Documentation)
- [Tauri Architecture](https://v2.tauri.app/concept/architecture/) - Official architecture overview
- [Tauri IPC](https://v2.tauri.app/concept/inter-process-communication/) - Communication patterns
- [Tauri Calling Rust](https://v2.tauri.app/develop/calling-rust/) - Command pattern
- [Tauri Calling Frontend](https://v2.tauri.app/develop/calling-frontend/) - Events and channels
- [Tauri State Management](https://v2.tauri.app/develop/state-management/) - State handling
- [Tauri Project Structure](https://v2.tauri.app/start/project-structure/) - File organization
- [Tokio Semaphore](https://docs.rs/tokio/latest/tokio/sync/struct.Semaphore.html) - Concurrency control

### Medium Confidence (Community & Examples)
- [Tauri React Template](https://github.com/dannysmith/tauri-template) - Production template
- [Rust Network Scanner](https://github.com/guardsarm/rust-network-scanner) - Example implementation
- [Network Scanner Design](https://pmc.ncbi.nlm.nih.gov/articles/PMC7472026/) - Architecture patterns
- [Tauri Async Best Practices](https://rfdonnelly.github.io/posts/tauri-async-rust-process/) - Async patterns
- [React Real-Time Updates](https://www.telerik.com/blogs/how-to-build-real-time-updating-data-grid-react) - Streaming data patterns
- [React Component Architecture](https://www.bacancytechnology.com/blog/react-architecture-patterns-and-best-practices) - 2026 patterns
- [Rust Project Structure](https://doc.rust-lang.org/book/ch07-00-managing-growing-projects-with-packages-crates-and-modules.html) - Module organization

---
*Architecture research for: Network Scanner with Tauri + React*
*Researched: 2026-02-08*
