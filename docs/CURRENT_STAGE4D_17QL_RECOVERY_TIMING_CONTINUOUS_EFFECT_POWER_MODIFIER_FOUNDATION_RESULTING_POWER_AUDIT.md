# Stage 4D-17QL Recovery Timing Continuous Effect Power Modifier Foundation Resulting Power Audit

Date: 2026-06-01 16:33 CST

Status: accepted for this checkpoint slice. Project remains **NOT READY**.

## Scope

A_MAIN tightened `MatchRecoveryValidator` recovery-frame validation for `continuousEffects[]` known valid-scope `POWER_MODIFIER` items carrying foundation-only LayerEngine status or readable deferred LayerEngine residuals.

Tracked ledger power modifiers currently serialize `effectivePower` and `resultingPower` from the same resolved runtime result. Legacy foundation remainders carry no tracked `resultingPower` scalar. This slice rejects readable foundation `resultingPower` values that differ from required `effectivePower`, preserving legacy remainder compatibility.

## Runtime Change

- Updated `src/Riftbound.Engine/MatchRecovery.cs`.
- Added `ValidateContinuousEffectPowerModifierFoundationResultingPowerConsistency`.
- The helper checks only valid-scope foundation `POWER_MODIFIER` payloads with readable integer `effectivePower` and `resultingPower`.
- Existing malformed integer, applied-power-delta, power-floor, tracked-scalar completeness and tracked source-object diagnostics remain in place.

## Tests

- Added `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierFoundationResultingPowerDrift`.
- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierFoundationResultingPowerDrift`.
- The spectator test keeps the count-mismatch path so same-payload diagnostics still run when authoritative parity is skipped.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "PowerModifierFoundationResultingPower"` -> `2/2`.
- Focused recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests` -> `516/516`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` -> `1097/1097`.
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `6462/6462`.
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs src tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining

This narrows P1-004 recovery/replay determinism and continuous-effect foundation resulting-power canonicality only. Broader command/recovery/random determinism, remaining nested recovered/spectator payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
