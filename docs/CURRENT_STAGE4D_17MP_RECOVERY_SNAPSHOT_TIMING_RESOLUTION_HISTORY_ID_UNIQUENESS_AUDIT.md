# Stage 4D-17MP Recovery Snapshot Timing Resolution History Id Uniqueness Audit

Date: 2026-05-28 02:21 CST
Owner: A_MAIN
Status: Accepted; project remains NOT READY.

## Scope

This slice covers recovered player-view snapshot timing resolution-history identity canonicality only:

- `Snapshot.Timing["battlefieldResolutions"][]`
- `Snapshot.Timing["battleResolutions"][]`

It does not change protocol shape, frontend behavior, matrix rows, official catalog data, Chrome/browser smoke, formal E2E, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSnapshotShape` now rejects duplicate normalized `resolutionId` values within recovered snapshot battlefield-resolution entries and battle-resolution entries before later resolution-history consumers can consume ambiguous recovered timing history identity.

The shared battlefield/battle resolution scalar value helpers now return the normalized `resolutionId` while preserving existing scalar diagnostics for required strings, surrounding whitespace, non-negative ticks, and optional-present scalar values.

## Test Coverage

Added `RecoveryValidatorRejectsSnapshotTimingResolutionHistoryDuplicateIds`.

The test mutates recovered player-view snapshot timing payloads with whitespace-mutated ids followed by duplicate normalized ids and asserts explicit diagnostics for both battlefield resolutions and battle resolutions.

## Validation

- Focused single test: `1/1`
- Focused recovery: `373/373`
- Adjacent recovery/opening/store-smoke: `954/954`
- Backend full conformance: `6319/6319`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Locks

A_MAIN touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board, and this audit file. Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status, and `riftbound-dotnet.sln` remain locked.
