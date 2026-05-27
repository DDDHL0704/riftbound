# Stage 4D-17MQ Recovery Spectator Timing Resolution History Id Uniqueness Audit

Date: 2026-05-28 02:28 CST
Owner: A_MAIN
Status: Accepted; project remains NOT READY.

## Scope

This slice covers spectator replay-frame timing resolution-history identity canonicality only:

- `Timing["battlefieldResolutions"][]`
- `Timing["battleResolutions"][]`

It does not change protocol shape, frontend behavior, matrix rows, official catalog data, Chrome/browser smoke, formal E2E, `fullOfficial`, final readiness, or `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now rejects duplicate normalized `resolutionId` values within spectator battlefield-resolution entries and battle-resolution entries before later resolution-history parity consumers compare spectator timing history against authoritative state.

The spectator battlefield/battle resolution scalar callers now use the shared helpers' normalized `resolutionId` return values while preserving existing scalar/list/property diagnostics.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryDuplicateIds`.

The test builds an authoritative state with two battlefield resolutions and two battle resolutions, mutates the spectator replay-frame timing payloads to contain whitespace-mutated ids followed by duplicate normalized ids, and asserts explicit diagnostics for both resolution-history lists.

## Validation

- Focused single test: `1/1`
- Focused recovery: `374/374`
- Adjacent recovery/opening/store-smoke: `955/955`
- Backend full conformance: `6320/6320`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Locks

A_MAIN touched only recovery runtime validation, recovery tests, current checkpoint/completion/P0-P1/next-dispatch docs, the shared coordination board, and this audit file. Matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status, and `riftbound-dotnet.sln` remain locked.
