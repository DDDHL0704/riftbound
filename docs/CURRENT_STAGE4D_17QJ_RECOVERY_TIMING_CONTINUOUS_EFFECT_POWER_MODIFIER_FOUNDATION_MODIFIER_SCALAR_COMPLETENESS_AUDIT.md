# Stage 4D-17QJ Recovery Timing Continuous Effect Power Modifier Foundation Modifier Scalar Completeness Audit

Date: 2026-06-01 16:14 CST

Status: accepted for this checkpoint slice. Project remains **NOT READY**.

## Scope

A_MAIN tightened `MatchRecoveryValidator` recovery-frame validation for `continuousEffects[]` known valid-scope `POWER_MODIFIER` items carrying foundation-only LayerEngine status or readable deferred LayerEngine residuals.

Tracked ledger power modifiers currently serialize `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower`, `resultingPower` and `appliedOrder` together. Legacy foundation remainders carry no tracked modifier scalar/order fields. This slice rejects partial tracked scalar/order sets only after at least one tracked scalar/order value is readable, preserving legacy remainder compatibility.

## Runtime Change

- Updated `src/Riftbound.Engine/MatchRecovery.cs`.
- Added `ValidateContinuousEffectPowerModifierFoundationModifierScalarCompleteness`.
- The helper requires missing tracked scalar/order fields only for valid-scope `POWER_MODIFIER` payloads with foundation-only status or readable deferred residuals.
- Existing malformed optional-int, applied-power-delta consistency and power-floor diagnostics remain in place.

## Tests

- Added `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierFoundationModifierScalarCompletenessDrift`.
- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierFoundationModifierScalarCompletenessDrift`.
- The spectator test keeps the count-mismatch path so same-payload diagnostics still run when authoritative parity is skipped.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "PowerModifierFoundationModifierScalarCompleteness"` -> `2/2`.
- Focused recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests` -> `512/512`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` -> `1093/1093`.
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `6458/6458`.
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs src tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining

This narrows P1-004 recovery/replay determinism and continuous-effect foundation tracked-scalar canonicality only. Broader command/recovery/random determinism, remaining nested recovered/spectator payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
