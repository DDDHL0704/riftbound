# Stage 4D-17IR Recovery Spectator Timing Resolution History Scalar Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 00:26 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure, READY, or READY-CANDIDATE.

## Scope

Stage 4D-17IR continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IQ. This slice covers spectator `Timing["battlefieldResolutions"][]` and `Timing["battleResolutions"][]` scalar and numeric item fields before authoritative resolution-history comparisons consume those payloads.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame resolution-history scalar and numeric value shapes before authoritative resolution comparisons consume those payloads.

For `Timing["battlefieldResolutions"][]`, required fields now reject missing, blank, non-string or surrounding-whitespace values as appropriate, and `tick` must be an integer non-negative long value:

- `resolutionId`
- `tick`
- `kind`
- `reason`
- `battlefieldObjectId`

Optional battlefield resolution references preserve absent/null/empty compatibility, while malformed present values now fail explicitly:

- `playerId`
- `previousControllerId`
- `controllerId`
- `sourceObjectId`

For `Timing["battleResolutions"][]`, the same explicit validation applies to required fields:

- `resolutionId`
- `tick`
- `kind`
- `reason`
- `battlefieldId`

Optional battle resolution references preserve absent/null/empty compatibility, while malformed present values now fail explicitly:

- `attackingPlayerId`
- `defendingPlayerId`
- `winnerPlayerId`

Malformed scalar and numeric payloads now fail with explicit spectator recovery diagnostics instead of surfacing only through later generic resolution-history mismatch comparisons.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryScalarValueDrift`.

The test builds a spectator replay frame from an authoritative state with one battlefield resolution and one battle resolution, mutates spectator resolution-history scalar and numeric fields to cover blank values, surrounding-whitespace values, non-string payloads, malformed optional present values, invalid numeric values and negative tick values, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `271/271`
- Adjacent recovery/opening/store-smoke: `852/852`
- Backend full: `6217/6217`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
