# Stage 4D-17IU Recovery Spectator Timing Battle Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 01:24 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure or final readiness.

## Scope

Stage 4D-17IU continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IT. This slice covers spectator `Timing["battle"]` top-level scalar, list and participant-controller map values before authoritative battle parity comparison consumes that payload.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame `Timing["battle"]` top-level value shapes before authoritative battle comparison consumes the payload.

Required fields now reject missing, null or non-boolean values:

- `isActive`

Optional scalar fields preserve absent/null/empty compatibility, while malformed present values now fail explicitly:

- `battleId`
- `battlefieldObjectId`

Required list fields now reject missing/null/non-list values, blank values, surrounding-whitespace values and duplicate normalized values:

- `attackerObjectIds`
- `defenderObjectIds`

The required participant-controller map now rejects missing/null/non-object payloads and malformed controller values:

- non-string values
- blank values
- surrounding-whitespace values

Participant-controller key canonicality remains covered by the existing spectator battle participant-controller property-name validation.

Malformed battle scalar, list and participant-controller payloads now fail with explicit spectator recovery diagnostics instead of surfacing only through a generic battle mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleValueDrift`.

The test builds a spectator replay frame from an authoritative battle damage-assignment state, mutates `Timing["battle"]` to cover invalid active flag values, malformed optional scalar values, invalid list shape, blank list entries, whitespace list entries, duplicate normalized list entries and malformed participant-controller map values, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `274/274`
- Adjacent recovery/opening/store-smoke: `855/855`
- Backend full: `6220/6220`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
