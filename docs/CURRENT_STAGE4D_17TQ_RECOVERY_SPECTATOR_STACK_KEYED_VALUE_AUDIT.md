# Stage 4D-17TQ Recovery Spectator Stack Keyed Value Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TQ tightens spectator replay-frame snapshot stack validation for same-key `SpectatorSnapshot.Stack[]` payload values under stack count mismatch. This slice adds authoritative keyed-value diagnostics before the existing broad stack count and list-parity diagnostics continue to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now builds an authoritative stack item index keyed by `stackItemId` from `MatchState.StackItems`. When a spectator snapshot stack item has the same key as an authoritative stack item, the validator checks:

- `controllerId`
- optional `sourceObjectId`
- `effectKind`
- optional `cardNo`
- `targetObjectIds[]`
- `damageAmount`
- optional `destination`

The existing stack item payload shape validation, duplicate id validation, 17TP key-set diagnostics, stack count mismatch and broad list parity diagnostics remain intact.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemKeyedValuesWithCountMismatch`

The test builds a spectator replay frame with two authoritative stack items, mutates the first same-key spectator stack item values, appends an extra forged stack item, and verifies explicit keyed-value diagnostics plus the existing extra id and stack count mismatch diagnostics.

Validation passed:

- focused keyed-value test `1/1`
- focused SpectatorReplaySnapshotStack filter `15/15`
- focused recovery `645/645`
- adjacent recovery/opening/store-smoke filter `1225/1225`
- backend full `6590/6590`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
