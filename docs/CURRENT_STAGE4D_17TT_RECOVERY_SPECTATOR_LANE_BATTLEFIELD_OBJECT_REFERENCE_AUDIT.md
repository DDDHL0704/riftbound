# Stage 4D-17TT Recovery Spectator Lane Battlefield-Object Reference Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TT tightens spectator replay-frame snapshot lane validation for `SpectatorSnapshot.Lanes["battlefieldObjectIds"]` pair references under lane battlefield count mismatch. This slice adds same-payload player and object membership diagnostics before the existing broad lane pair parity diagnostics continue to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now validates spectator lane `battlefieldObjectIds[]` pair `playerId` values against authoritative `MatchState.Seats`.

It also validates each pair `objectId` against the authoritative visible battlefield-object id set derived from the existing spectator lane battlefield object pair expectation.

The existing lane payload shape validation, lane battlefield count mismatch diagnostic, broad battlefield-object pair parity diagnostic, battlefield payload parity diagnostics and downstream standby-slot diagnostics remain intact.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotLaneBattlefieldObjectIdReferencesWithCountMismatch`

The test builds a spectator replay frame with one authoritative battlefield object, appends a forged extra `battlefieldObjectIds[]` pair with valid payload shape but a player outside seats and an object outside authoritative visible battlefield object ids, and verifies explicit membership diagnostics plus the existing lane battlefield count and broad pair mismatch diagnostics.

Validation passed:

- focused lane reference test `1/1`
- focused SpectatorReplaySnapshotLane filter `7/7`
- focused recovery `647/647`
- adjacent recovery/opening/store-smoke filter `1228/1228`
- backend full `6593/6593`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

Additional mechanical note:

- `dotnet format Riftbound.slnx --no-restore --verify-no-changes` reported unrelated pre-existing whitespace diagnostics outside this slice, including `CoreRuleEngine.cs` and `MatchSession.cs`; no unrelated formatting was applied.

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
