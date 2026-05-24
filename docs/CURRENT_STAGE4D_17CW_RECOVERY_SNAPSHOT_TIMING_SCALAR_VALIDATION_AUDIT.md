# Stage 4D-17CW Recovery Snapshot Timing Scalar Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now requires nonblank `phase`, `turnPlayerId` and `roomStatus` scalar fields inside recovered player snapshot `Timing` payloads.

Test change: the recovery player-view helper now emits those legal core timing scalars, and `RecoveryValidatorRejectsMissingSnapshotTimingCoreFields` covers missing phase, turn-player and room-status diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `127/127`.
- Adjacent recovery/opening: passed `707/707`.
- Backend full: passed `6073/6073`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered player snapshot timing scalar presence validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
