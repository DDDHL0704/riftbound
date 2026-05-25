# Stage 4D-17IN Recovery Spectator Timing Continuous Effect List Value Shape Audit

Status: accepted by A_MAIN on 2026-05-25 23:30 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure, READY, or READY-CANDIDATE.

## Scope

Stage 4D-17IN closes the next small runtime/server closure slice after Stage 4D-17IM by aligning spectator replay-frame timing `continuousEffects` list-value validation with the player-view snapshot continuous-effect list-value validation already accepted in Stage 4D-17IB.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame `Timing["continuousEffects"][]` item list-valued fields before authoritative continuous-effect comparisons consume those payloads.

For each spectator continuous-effect item, the validator now rejects malformed string-list values in:

- `participantObjectIds`
- `sourceDependencyObjectIds`
- `targetDependencyObjectIds`
- `participantDependencyObjectIds`
- `deferredLayerEngineResiduals`

Each present list must be a string list. Blank entries, surrounding-whitespace entries, and duplicate normalized values produce explicit spectator recovery diagnostics. This preserves the existing optional-list behavior while preventing malformed values from surfacing only as generic authoritative mismatch drift.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectListValueDrift`.

The test builds a spectator replay frame from an authoritative state with one continuous effect, mutates the spectator timing continuous-effect list fields to include blank, whitespace-mutated, and duplicate normalized values, and asserts explicit diagnostics for participant object ids, source dependencies, target dependencies, participant dependencies, and deferred LayerEngine residuals.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `267/267`
- Adjacent recovery/opening/store-smoke: `848/848`
- Backend full: `6213/6213`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
