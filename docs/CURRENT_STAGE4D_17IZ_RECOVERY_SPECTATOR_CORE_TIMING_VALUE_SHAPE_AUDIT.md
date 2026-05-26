# Stage 4D-17IZ Recovery Spectator Core Timing Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 08:59 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure or final readiness.

## Scope

Stage 4D-17IZ continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IY. This slice covers top-level spectator core timing values before authoritative core timing comparison consumes those fields.

Covered fields:

- `Timing["phase"]`
- `Timing["timingState"]`
- `Timing["turnPlayerId"]`
- `Timing["roomStatus"]`
- `Timing["readyPlayerIds"]`
- `Timing["winningScore"]`

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates top-level spectator replay-frame core timing value shapes before authoritative core timing comparison consumes the fields.

The core timing fields now check:

- required `phase`, `timingState` and `roomStatus` string values against known values
- required `turnPlayerId` string values
- required `readyPlayerIds` string-list payloads
- required positive integer `winningScore` values
- ready-player list values reject blank, surrounding-whitespace and duplicate normalized player ids

Malformed values now fail with explicit spectator recovery diagnostics instead of surfacing only through generic core timing mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingCoreValueDrift`.

The test builds a spectator replay frame from an authoritative state with core timing state, mutates top-level timing fields to include whitespace-mutated and unknown enum values, a whitespace-mutated turn player, blank and duplicate ready-player list values and a non-positive winning score, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `279/279`
- Adjacent recovery/opening/store-smoke: `860/860`
- Backend full: `6225/6225`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
