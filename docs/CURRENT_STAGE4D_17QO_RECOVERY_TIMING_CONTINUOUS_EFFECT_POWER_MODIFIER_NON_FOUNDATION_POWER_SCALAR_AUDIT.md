# Stage 4D-17QO Recovery Timing Continuous Effect Power Modifier Non-Foundation Power Scalar Audit

Date: 2026-06-01 17:00 CST

Status: accepted for this checkpoint slice. Project remains **NOT READY**.

## Scope

A_MAIN tightened `MatchRecoveryValidator` recovery-frame validation for `continuousEffects[]` known valid-scope non-foundation `POWER_MODIFIER` items.

Current simple legacy runtime builders serialize non-foundation `POWER_MODIFIER` effects only for aggregate until-end-of-turn power modifiers. In that path, `basePower` is resolved before temporary modifiers, `powerDelta` is the aggregate temporary modifier, and `effectivePower` is the resulting object power, so `effectivePower` must equal `basePower + powerDelta`. Foundation ledger and foundation remainder payloads are excluded because they can represent one component of a larger aggregate.

## Runtime Change

- Updated `src/Riftbound.Engine/MatchRecovery.cs`.
- Added `ValidateContinuousEffectPowerModifierNonFoundationPowerScalarConsistency`.
- The helper checks only known valid-scope `POWER_MODIFIER` payloads without foundation-only LayerEngine status and without readable deferred LayerEngine residuals.
- Existing malformed integer, nonzero power-delta, foundation, non-foundation metadata and tracked-scalar diagnostics remain in place.

## Tests

- Added `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierNonFoundationPowerScalarConsistencyDrift`.
- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierNonFoundationPowerScalarConsistencyDrift`.
- The spectator test keeps the count-mismatch path so same-payload diagnostics still run when authoritative parity is skipped.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "NonFoundationPowerScalarConsistency"` -> `2/2`.
- Focused recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests` -> `522/522`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` -> `1103/1103`.
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `6468/6468`.
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs src tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining

This narrows P1-004 recovery/replay determinism and power-modifier simple legacy canonicality only. Broader command/recovery/random determinism, remaining nested recovered/spectator payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
