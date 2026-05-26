# Stage 4D-17JC Recovery Snapshot Continuous Effect Scalar Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 10:00 CST. Project remains **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, or final readiness status.

`MatchRecoveryValidator.ValidateSnapshotShape` now validates present `Timing["continuousEffects"][]` scalar and numeric item values before continuous-effect field reads and parity checks consume those payloads.

Covered required string fields:

- `effectId`
- `scope`
- `layer`
- `duration`

Covered optional-present string fields:

- `targetObjectId`
- `sourceObjectId`
- `effectKind`
- `sourceCardNo`
- `sourcePath`
- `layerEngineStatus`
- `condition`
- `lifecycle`

Covered required integer fields:

- `powerDelta`
- `basePower`
- `effectivePower`
- `sequence`

Covered optional-present integer fields:

- `requestedPowerDelta`
- `appliedPowerDelta`
- `minimumPower`
- `resultingPower`
- `appliedOrder`
- `sourceOrder`

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now calls `ValidateSnapshotTimingContinuousEffectScalarPayloadValues` from snapshot shape validation. The snapshot helper uses shared `ValidateContinuousEffectScalarPayloadValues` logic with recovered-snapshot labels, and the existing spectator continuous-effect validation delegates to the same scalar helper before running its list-value checks.

The validation rejects malformed recovered snapshot continuous-effect scalar drift:

- required strings must be present, non-blank, string typed and free of surrounding whitespace;
- optional-present strings must be string typed, non-blank and free of surrounding whitespace when provided;
- required integers must be present, non-null and integer typed;
- optional-present integers must be integer typed when provided.

## Test Coverage

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` adds `RecoveryValidatorRejectsSnapshotTimingContinuousEffectScalarValueDrift`.

The test mutates recovered player-view snapshot `Timing["continuousEffects"][]` with invalid required strings, optional strings, required integers and optional integers, then asserts explicit recovered snapshot diagnostics for whitespace, blank, malformed and missing value drift.

## Validation

- Focused single: `RecoveryValidatorRejectsSnapshotTimingContinuousEffectScalarValueDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `282/282`.
- Adjacent recovery/opening/store-smoke filter passed `863/863`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `6228/6228`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over docs/src/tests, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered snapshot continuous-effect scalar value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
