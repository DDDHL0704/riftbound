# Stage 4D-17IY Recovery Spectator Timing Focus Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 08:48 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure or final readiness.

## Scope

Stage 4D-17IY continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IX. This slice covers top-level spectator `Timing["focusPlayerId"]`, `Timing["winnerPlayerId"]`, `Timing["passedFocusPlayerIds"]` and `Timing["destroyedUnitOwnerIdsThisTurn"]` values before authoritative focus/winner/list comparison consumes those fields.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates top-level spectator replay-frame focus timing value shapes before authoritative focus/winner/list comparison consumes the fields.

The focus timing fields now check:

- optional-present `focusPlayerId` and `winnerPlayerId` string values while preserving absent/null/empty compatibility
- required `passedFocusPlayerIds` and `destroyedUnitOwnerIdsThisTurn` string-list payloads
- list values reject blank, surrounding-whitespace and duplicate normalized player ids

Malformed values now fail with explicit spectator recovery diagnostics instead of surfacing only through generic focus/winner/list mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingFocusValueDrift`.

The test builds a spectator replay frame from an authoritative state with focus, winner, passed-focus and destroyed-owner timing state, mutates top-level timing fields to include surrounding whitespace, invalid scalar payloads, blank list values and duplicate normalized list values, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `278/278`
- Adjacent recovery/opening/store-smoke: `859/859`
- Backend full: `6224/6224`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
