# Stage 4D-17QN Recovery Timing Continuous Effect Power Modifier Foundation Source Object Modifier Scalar Completeness Audit

Date: 2026-06-01 16:53 CST

Status: accepted for this checkpoint slice. Project remains **NOT READY**.

## Scope

A_MAIN tightened `MatchRecoveryValidator` recovery-frame validation for `continuousEffects[]` known valid-scope `POWER_MODIFIER` items carrying foundation-only LayerEngine status or readable deferred LayerEngine residuals.

Current stack/direct runtime builders serialize source-object-bearing foundation `POWER_MODIFIER` effects from tracked ledger entries, and those entries carry the tracked modifier scalar/order set together: `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower`, `resultingPower` and `appliedOrder`. Legacy foundation remainders keep `sourceObjectId` null and carry no tracked modifier scalar/order fields, so they remain compatible.

## Runtime Change

- Updated `src/Riftbound.Engine/MatchRecovery.cs`.
- Extended `ValidateContinuousEffectPowerModifierFoundationModifierScalarCompleteness`.
- The helper now treats a readable foundation `sourceObjectId` as requiring the complete tracked modifier scalar/order set.
- Existing diagnostics keep their previous wording when tracked modifier scalar/order values are already readable.

## Tests

- Added `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierFoundationSourceObjectModifierScalarCompletenessDrift`.
- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierFoundationSourceObjectModifierScalarCompletenessDrift`.
- The spectator test keeps the count-mismatch path so same-payload diagnostics still run when authoritative parity is skipped.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FoundationSourceObjectModifierScalarCompleteness"` -> `2/2`.
- Focused recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests` -> `520/520`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` -> `1101/1101`.
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `6466/6466`.
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs src tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining

This narrows P1-004 recovery/replay determinism and power-modifier foundation tracked-ledger canonicality only. Broader command/recovery/random determinism, remaining nested recovered/spectator payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
