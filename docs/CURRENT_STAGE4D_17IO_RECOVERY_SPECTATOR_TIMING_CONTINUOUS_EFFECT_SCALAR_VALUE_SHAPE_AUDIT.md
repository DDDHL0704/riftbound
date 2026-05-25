# Stage 4D-17IO Recovery Spectator Timing Continuous Effect Scalar Value Shape Audit

Status: accepted by A_MAIN on 2026-05-25 23:43 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure, READY, or READY-CANDIDATE.

## Scope

Stage 4D-17IO continues the spectator replay-frame timing `continuousEffects` closure after Stage 4D-17IN. The prior slice made list-valued continuous-effect fields explicit; this slice makes scalar and numeric value-shape drift explicit before authoritative continuous-effect comparisons consume those payloads.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame `Timing["continuousEffects"][]` scalar and numeric value shapes before authoritative continuous-effect comparisons consume those payloads.

Required string fields now reject missing, blank, non-string, and surrounding-whitespace values:

- `effectId`
- `scope`
- `layer`
- `duration`

Optional string fields now reject malformed present values while preserving absent/null/empty compatibility:

- `targetObjectId`
- `sourceObjectId`
- `effectKind`
- `sourceCardNo`
- `sourcePath`
- `layerEngineStatus`
- `condition`
- `lifecycle`

Required integer fields now reject missing/null/non-integer values:

- `powerDelta`
- `basePower`
- `effectivePower`
- `sequence`

Optional integer fields now reject malformed present non-integer values:

- `requestedPowerDelta`
- `appliedPowerDelta`
- `minimumPower`
- `resultingPower`
- `appliedOrder`
- `sourceOrder`

The integer guard is intentionally shape-only; it does not add range constraints to power fields whose valid values can be negative or context-dependent.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectScalarValueDrift`.

The test builds a spectator replay frame from an authoritative state with one emitted continuous effect, mutates required strings, optional strings, required ints, and optional ints to malformed present values, and asserts explicit spectator recovery diagnostics for each malformed value-shape category.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `268/268`
- Adjacent recovery/opening/store-smoke: `849/849`
- Backend full: `6214/6214`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
