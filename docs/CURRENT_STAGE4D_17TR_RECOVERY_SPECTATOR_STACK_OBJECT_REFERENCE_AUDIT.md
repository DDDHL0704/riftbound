# Stage 4D-17TR Recovery Spectator Stack Object-Reference Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TR tightens spectator replay-frame snapshot stack validation for `SpectatorSnapshot.Stack[]` object references under stack count mismatch. This slice adds same-payload source/target object-registry diagnostics before the existing broad stack count and list-parity diagnostics continue to run.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now validates spectator snapshot stack item object refs against the authoritative object registry built from `MatchState.CardObjects` and `MatchState.ObjectLocations`.

The validator checks:

- optional `sourceObjectId`
- `targetObjectIds[]`

The existing stack item payload shape validation, duplicate id validation, key-set diagnostics, keyed authoritative value diagnostics, stack count mismatch and broad list parity diagnostics remain intact.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplaySnapshotStackItemObjectReferencesOutsideRegistryWithCountMismatch`

The test builds a spectator replay frame with known authoritative stack source/target objects, appends a forged extra stack item with valid payload shape but missing source/target object ids, and verifies explicit missing-registry diagnostics plus the existing extra id and stack count mismatch diagnostics.

Validation passed:

- focused object-reference test `1/1`
- focused SpectatorReplaySnapshotStack filter `16/16`
- focused recovery `646/646`
- adjacent recovery/opening/store-smoke filter `1226/1226`
- backend full `6591/6591`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
