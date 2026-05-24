# Stage 4D-17CT Recovery Snapshot Turn-Number Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now rejects recovered player snapshots whose `TurnNumber` is below `1` before baseline restoration can create a restored `MatchState` from impossible turn metadata.

Test change: `RecoveryValidatorRejectsSnapshotTurnNumbersBelowOne` covers turn number `0` and `-1`, asserting both explicit diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `124/124`.
- Adjacent recovery/opening: passed `704/704`.
- Backend full: passed `6070/6070`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered snapshot turn-number lower-bound validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
