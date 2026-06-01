# Stage 4D-17QI Recovery Timing Continuous Effect Power Modifier Non-Foundation Modifier Scalar Audit

Date: 2026-06-01 16:06 CST

Status: accepted for this checkpoint slice. Project remains **NOT READY**.

## Scope

A_MAIN tightened `MatchRecoveryValidator` recovery-frame validation for `continuousEffects[]` known valid-scope `POWER_MODIFIER` items that do not carry foundation-only LayerEngine status and do not carry readable deferred LayerEngine residuals.

Simple non-foundation legacy power modifiers are emitted without tracked modifier/order scalars. The validator now rejects readable non-null `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower`, `resultingPower` and `appliedOrder` values on those non-foundation items. Foundation-only power modifiers from tracked ledger entries remain the path that may carry those fields.

## Runtime Change

- Updated `src/Riftbound.Engine/MatchRecovery.cs`.
- Added `ValidateContinuousEffectPowerModifierNonFoundationModifierScalarAbsence`.
- The helper runs only for known valid-scope `POWER_MODIFIER` entries without foundation-only LayerEngine status and without readable deferred residuals.
- Existing optional-int diagnostics, applied-power-delta consistency and power-floor consistency remain in place for malformed or otherwise invalid scalar values.

## Tests

- Added `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierNonFoundationModifierScalarDrift`.
- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierNonFoundationModifierScalarDrift`.
- The spectator test keeps the count-mismatch path so same-payload diagnostics still run when authoritative parity is skipped.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "PowerModifierNonFoundationModifierScalar"` -> `2/2`.
- Focused recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests` -> `510/510`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` -> `1091/1091`.
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `6456/6456`.
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs src tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining

This narrows P1-004 recovery/replay determinism and continuous-effect non-foundation canonicality only. Broader command/recovery/random determinism, remaining nested recovered/spectator payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
