# Stage 4D-17CS Recovery Snapshot Active-Player Membership Validation Audit

Date: 2026-05-24

Status: accepted. Project remains **NOT READY**.

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

Runtime change: `MatchRecoveryValidator.ValidatePlayerViews` now validates recovered snapshot active-player membership. Each snapshot must carry a nonblank `ActivePlayerId`, and that player id must exist in the snapshot `Players` map before restoration proceeds.

Test change: `RecoveryValidatorRejectsSnapshotActivePlayerOutsidePlayerMap` covers a missing active player (`charlie`) and a blank active-player id, asserting both explicit diagnostics.

Validation:

- Focused recovery: `MatchRecoveryTests` passed `123/123`.
- Adjacent recovery/opening: passed `703/703`.
- Backend full: passed `6069/6069`.
- Mechanical gates: `git diff --check`, anchored conflict-marker scan, and matrix JSON parse passed before checkpoint commit.

Non-scope: no matrix JSON, payment coverage guard, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, READY / READY-CANDIDATE, or `riftbound-dotnet.sln` changes.

Remaining risk: this closes only recovered snapshot active-player membership validation. Broader command/recovery/random determinism, P0/P1 closure, frontend build, Chrome smoke, formal 18-step E2E, `fullOfficial`, and READY remain open.
