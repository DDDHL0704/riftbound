# Stage 4D-17TS Recovery Spectator Stack Controller Reference Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TS tightens spectator replay-frame snapshot stack validation for `SpectatorSnapshot.Stack[]` controller player references under stack count mismatch. This slice adds same-payload controller seat diagnostics before the existing broad stack count and list-parity diagnostics continue to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now validates spectator snapshot stack item `controllerId` values against authoritative `MatchState.Seats`.

The existing stack item payload shape validation, duplicate id validation, key-set diagnostics, keyed authoritative value diagnostics, object-reference diagnostics, stack count mismatch and broad list parity diagnostics remain intact.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemControllerOutsideSeatsWithCountMismatch`

The test builds a spectator replay frame with valid authoritative stack items, appends a forged extra stack item with valid payload shape but a controller outside seats, and verifies an explicit missing-seat diagnostic plus the existing extra id and stack count mismatch diagnostics.

Validation passed:

- focused controller seat test `1/1`
- focused SpectatorReplaySnapshotStack filter `17/17`
- focused recovery `647/647`
- adjacent recovery/opening/store-smoke filter `1227/1227`
- backend full `6592/6592`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
