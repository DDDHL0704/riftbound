# Stage 4D-17IQ Recovery Spectator Timing Resolution History List Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 00:13 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure, READY, or READY-CANDIDATE.

## Scope

Stage 4D-17IQ continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IP. This slice covers spectator `Timing["battlefieldResolutions"][]` and `Timing["battleResolutions"][]` list-valued fields before authoritative resolution-history comparisons consume those payloads.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame resolution-history list value shapes before authoritative resolution comparisons consume those payloads.

For `Timing["battlefieldResolutions"][]`, present list-valued fields must be string lists, and blank values, surrounding-whitespace values and duplicate normalized values produce explicit spectator recovery diagnostics:

- `participantObjectIds`
- `relatedEventKinds`

For `Timing["battleResolutions"][]`, the same explicit validation applies to:

- `attackerObjectIds`
- `defenderObjectIds`
- `survivingAttackerObjectIds`
- `survivingDefenderObjectIds`
- `destroyedObjectIds`
- `relatedEventKinds`

Malformed present list payloads now fail with explicit invalid-list diagnostics instead of surfacing only through later generic resolution-history mismatch comparisons.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryListValueDrift`.

The test builds a spectator replay frame from an authoritative state with one battlefield resolution and one battle resolution, mutates every spectator resolution-history list field to cover surrounding-whitespace values, duplicate normalized values, blank values or malformed non-string-list payloads, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `270/270`
- Adjacent recovery/opening/store-smoke: `851/851`
- Backend full: `6216/6216`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
