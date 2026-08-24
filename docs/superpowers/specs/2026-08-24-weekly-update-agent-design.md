# Weekly Update Agent — Design

**Date:** 2026-08-24
**Goal:** A fully autonomous agent that keeps Agrus Scanner current once a week: new AI detection signatures + dependency/security bumps, ending in a published GitHub release with a fresh MSI.

## Architecture

Two components, because the cloud agent (Linux sandbox) cannot build a Windows MSI:

```
┌─────────────────────────────┐      tag push       ┌──────────────────────────────┐
│ Claude Code cloud routine   │  ── vX.Y.Z ──────▶  │ GitHub Actions release.yml   │
│ (weekly, Mon 06:00 ET)      │                     │ (windows-latest runner)      │
│ research → edit → validate  │                     │ publish → WiX MSI → Release  │
│ → commit → merge → tag      │                     └──────────────────────────────┘
└─────────────────────────────┘
```

1. **Cloud routine** (created with `/schedule`) — the brain. Clones the repo, does research and edits, gates itself on a successful build, merges to `master`, pushes a version tag. If there is nothing to change, it exits without committing.
2. **`release.yml`** (new workflow) — the hands. Triggered by `v*` tags. On `windows-latest`: `dotnet publish`, run `build-installer.ps1` (WiX), attach `AgrusScanner-Setup.msi` to a GitHub release with generated notes. This replaces the manual steps in CLAUDE.md's release process for agent-driven releases.

## Weekly routine flow

1. **Inventory** — read the probe catalog in `AgrusScanner/Services/AiServiceProber.cs` and the README service table.
2. **Signature research** — web-search for (a) newly released or newly popular self-hosted AI/ML services not in the catalog, and (b) endpoint/response changes in already-covered services (e.g. Ollama version bumps that alter `/api/*` responses).
3. **Signature edits** — add `ProbeDefinition` entries following existing conventions (Path, ServiceName, Category, Confidence, Specificity, BodyContains). Update the README detection table and, if a new service has a distinct default port, the AI port profiles.
4. **Dependencies** — `dotnet list package --outdated` and `--vulnerable`. Bump patch/minor freely; bump a major version only to clear a known vulnerability.
5. **Validate** — `dotnet build -p:EnableWindowsTargeting=true` must pass; catalog sanity checks (no duplicate Path+BodyContains pair, valid Category, Specificity in 0–100).
6. **Ship** — bump the version in the csproj and installer, commit with a changelog-style message, merge to `master`, push tag `vX.Y.Z`. The tag fires `release.yml`, which publishes the release with the MSI.
7. **Report** — the routine's run summary lists services added, packages bumped, and the release URL (or "no changes this week").

## Guardrails (fully autonomous, so these are hard rules in the prompt)

- **Signatures are additive and passive only** — new probes are GET requests matched on response content; never modify scanning-engine logic, concurrency, or existing probe behavior.
- **Cap of ~10 new probes per run** — keeps any bad week reviewable and revertible.
- **No release without a green build** — build failure means commit to a branch + open a PR instead of merging, and skip the release.
- **Major dependency bumps only for CVEs**, and named explicitly in the run report.
- **No changes → no commit, no tag, no release** — empty releases are worse than skipped weeks.

## Prerequisites / one-time setup

1. Create `.github/workflows/release.yml` (windows-latest, WiX via `dotnet tool install wix`, `contents: write` permission for the release).
2. Verify once that the WPF project compiles on Linux with `-p:EnableWindowsTargeting=true`; if it doesn't, the build gate moves into `release.yml` (tag still only pushed after catalog sanity checks, and the workflow aborts the release on build failure).
3. Register the routine with `/schedule` (weekly, Monday 06:00 ET) using the flow + guardrails above as its prompt.
4. **Recommended:** add a small test project asserting catalog invariants (unique signatures, valid categories) so the gate is a real test run, not just compilation.

## Risks accepted by "fully autonomous"

- A weak signature could ship a false positive/negative to users. Mitigated by: signature edits are data-only, capped, and every release has a diff + tag that `git revert` + re-tag can undo.
- The Windows-only MSI build happens after the merge; a WiX failure leaves a tag without a release. The workflow should fail loudly (routine checks last release status at the start of the next run).
