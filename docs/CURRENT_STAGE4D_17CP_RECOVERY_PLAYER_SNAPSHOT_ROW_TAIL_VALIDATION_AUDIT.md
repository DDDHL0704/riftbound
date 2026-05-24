# Stage 4D-17CP Recovery Player Snapshot Row Tail Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects recovered player snapshots whose row tick or event sequence does not match the recovery frame `currentTick` / `lastEventSequence`. This prevents stale player snapshots from being restored as the baseline for a newer recovery tail.

Test change: `RecoveryValidatorRejectsPlayerSnapshotRowsBehindRecoveryTail` builds a player view at snapshot row tick/event `1` while the recovery frame tick/event tail is `2`, then proves the validator reports both tail-alignment diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `120/120`.
- Adjacent recovery/opening: passed `700/700`.
- Backend full: passed `6066/6066`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot row-tail validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
