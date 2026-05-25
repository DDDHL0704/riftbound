# Stage 4D-17IT Recovery Spectator Timing Spell Duel Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 01:02 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure or final readiness.

## Scope

Stage 4D-17IT continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IS. This slice covers spectator `Timing["spellDuel"]` scalar and list fields before authoritative spell-duel parity comparison consumes that payload.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame `Timing["spellDuel"]` value shapes before authoritative spell-duel comparison consumes the payload.

Required fields now reject missing, null or non-boolean values:

- `isActive`
- `isClosed`

Optional scalar fields preserve absent/null/empty compatibility, while malformed present values now fail explicitly:

- `spellDuelId`
- `battlefieldObjectId`
- `focusPlayerId`

Required list fields now reject missing/null/non-list values, blank values, surrounding-whitespace values and duplicate normalized values:

- `passedFocusPlayerIds`
- `stackItemIds`
- `stackControllerIds`

Malformed spell-duel scalar and list payloads now fail with explicit spectator recovery diagnostics instead of surfacing only through a generic spell-duel mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingSpellDuelValueDrift`.

The test builds a spectator replay frame from an authoritative spell-duel-open state, mutates `Timing["spellDuel"]` to cover invalid booleans, missing/null boolean drift, malformed optional scalar values, missing required list payloads, invalid list shape, blank list entries, whitespace list entries and duplicate normalized list entries, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `273/273`
- Adjacent recovery/opening/store-smoke: `854/854`
- Backend full: `6219/6219`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
