# Stage 4D-17IX Recovery Spectator Timing Priority Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 08:40 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure or final readiness.

## Scope

Stage 4D-17IX continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IW. This slice covers top-level spectator `Timing["priorityPlayerId"]` and `Timing["passedPriorityPlayerIds"]` values before authoritative priority comparison consumes those fields.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates top-level spectator replay-frame priority timing value shapes before authoritative priority comparison consumes the fields.

The priority timing fields now check:

- optional-present `priorityPlayerId` string values while preserving absent/null/empty compatibility
- required `passedPriorityPlayerIds` string-list payloads
- passed-priority list values reject blank, surrounding-whitespace and duplicate normalized player ids

Malformed values now fail with explicit spectator recovery diagnostics instead of surfacing only through generic priority mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingPriorityValueDrift`.

The test builds a spectator replay frame from an authoritative state with active priority and passed-priority state, mutates top-level timing priority fields to include surrounding whitespace, blank values and duplicate normalized list values, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `277/277`
- Adjacent recovery/opening/store-smoke: `858/858`
- Backend full: `6223/6223`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
