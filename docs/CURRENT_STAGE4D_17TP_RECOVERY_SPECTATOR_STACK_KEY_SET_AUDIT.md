# Stage 4D-17TP Recovery Spectator Stack Key-Set Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TP tightens spectator replay-frame snapshot stack validation for `SpectatorSnapshot.Stack[]` under stack count mismatch. This slice adds authoritative key-set diagnostics before the existing broad stack count and list-parity diagnostics continue to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now compares spectator snapshot stack item ids against authoritative `MatchState.StackItems` ids. The validator emits explicit diagnostics when:

- a spectator stack item id is not present in authoritative stack items
- an authoritative stack item id is missing from the spectator snapshot stack

The existing stack item payload shape validation, duplicate id validation, stack count mismatch and broad list parity diagnostics remain intact.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemKeySetWithCountMismatch`

The test builds a spectator replay frame with two authoritative stack items, replaces one spectator stack id with a forged id, appends a second forged stack item, and verifies explicit extra/missing stack id diagnostics plus the stack count mismatch diagnostic.

Validation passed:

- focused stack key-set test `1/1`
- focused SpectatorReplaySnapshotStack filter `14/14`
- focused recovery `644/644`
- adjacent recovery/opening/store-smoke filter `1224/1224`
- backend full `6589/6589`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
