# Stage 4D-17DJ Recovery Command Player-View Membership Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidateCommands` now receives the recovered player-view key set and rejects recovered command `PlayerId` values that are missing from that set when the frame carries player views. The guard keeps command metadata aligned with the recovered player snapshot set before idempotency-cache restoration or action-log replay consumes the frame.

Test change: `RecoveryValidatorRejectsRecoveredCommandPlayerOutsidePlayerViews` covers a recovered `charlie` command against alice/bob recovered player views.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `140/140`.
- Adjacent recovery/opening: passed `720/720`.
- Backend full: passed `6086/6086`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered command player membership against available player views. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
