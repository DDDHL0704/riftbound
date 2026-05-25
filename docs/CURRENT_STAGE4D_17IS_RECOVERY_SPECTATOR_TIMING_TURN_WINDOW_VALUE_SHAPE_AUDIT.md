# Stage 4D-17IS Recovery Spectator Timing Turn Window Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 00:43 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure, READY, or READY-CANDIDATE.

## Scope

Stage 4D-17IS continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IR. This slice covers spectator `Timing["turnWindow"]` scalar and boolean fields before authoritative turn-window parity comparison consumes that payload.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame `Timing["turnWindow"]` value shapes before authoritative turn-window comparison consumes the payload.

Required fields now reject missing, null, blank, non-string, non-boolean or surrounding-whitespace values as appropriate:

- `state`
- `isSpellDuel`
- `isClosed`
- `hasStack`

The optional `actingPlayerId` field preserves absent/null/empty compatibility, while malformed present values now fail explicitly.

Malformed turn-window scalar and boolean payloads now fail with explicit spectator recovery diagnostics instead of surfacing only through a generic turn-window mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingTurnWindowValueDrift`.

The test builds a spectator replay frame from an authoritative neutral-open state, mutates `Timing["turnWindow"]` to cover surrounding-whitespace state and acting-player values, invalid boolean payloads and a missing/null boolean value, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `272/272`
- Adjacent recovery/opening/store-smoke: `853/853`
- Backend full: `6218/6218`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
