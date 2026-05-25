# Stage 4D-17IW Recovery Spectator Timing Pending Hand Choice Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 01:52 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure or final readiness.

## Scope

Stage 4D-17IW continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IV. This slice covers spectator `Timing["pendingHandChoice"]` scalar, count and state values before authoritative pending hand-choice comparison consumes that payload.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame `Timing["pendingHandChoice"]` value shapes before authoritative pending hand-choice comparison consumes the payload.

The pending hand-choice payload now checks:

- required string values `choiceId`, `choiceWindow`, `playerId` and `choiceState`
- `choiceState` must be `WAITING_FOR_CHOICE`
- required positive integer values `requiredCount` and `maxCount`
- `maxCount` must be greater than or equal to `requiredCount`
- optional-present string values `reason`, `sourceObjectId` and `effectKind`
- existing redaction validation still rejects spectator-visible `legalObjectIds`

Malformed values now fail with explicit spectator recovery diagnostics instead of surfacing only through generic pending hand-choice mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingHandChoiceValueDrift`.

The test builds a spectator replay frame from an authoritative pending hand-choice state, mutates `Timing["pendingHandChoice"]` to cover whitespace-mutated and blank strings, non-string ids, non-integer and non-positive counts, invalid choice state, malformed optional-present values and spectator-visible legal object ids, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `276/276`
- Adjacent recovery/opening/store-smoke: `857/857`
- Backend full: `6222/6222`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
