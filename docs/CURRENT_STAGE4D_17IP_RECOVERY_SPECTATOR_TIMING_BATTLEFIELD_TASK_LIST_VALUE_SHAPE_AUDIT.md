# Stage 4D-17IP Recovery Spectator Timing Battlefield Task List Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 00:00 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure, READY, or READY-CANDIDATE.

## Scope

Stage 4D-17IP continues spectator replay-frame timing payload value-shape closure after the 17IK-17IO spectator pending-payment, temporary-payment-resource, trigger-queue and continuous-effect value-shape slices. This slice covers spectator `Timing["battlefieldTasks"][]` list-valued fields before authoritative battlefield-task comparisons consume those payloads.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame `Timing["battlefieldTasks"][]` list value shapes before authoritative battlefield-task comparisons consume those payloads.

Present list-valued fields must be string lists, and blank values, surrounding-whitespace values and duplicate normalized values produce explicit spectator recovery diagnostics:

- `participantControllerIds`
- `participantObjectIds`
- `stackItemIds`

Malformed present list payloads now fail with explicit invalid-list diagnostics instead of surfacing only through later generic task mismatch comparisons.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskListValueDrift`.

The test builds a spectator replay frame from an authoritative contested battlefield state, mutates `participantControllerIds`, `participantObjectIds` and `stackItemIds` to cover surrounding-whitespace values, duplicate normalized values, blank values and malformed non-string-list payloads, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `269/269`
- Adjacent recovery/opening/store-smoke: `850/850`
- Backend full: `6215/6215`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
