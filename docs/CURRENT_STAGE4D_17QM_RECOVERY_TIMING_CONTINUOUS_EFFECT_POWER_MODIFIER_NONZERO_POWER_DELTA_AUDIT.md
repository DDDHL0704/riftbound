# Stage 4D-17QM Recovery Timing Continuous Effect Power Modifier Nonzero Power Delta Audit

Date: 2026-06-01 16:43 CST

Status: accepted for this checkpoint slice. Project remains **NOT READY**.

## Scope

A_MAIN tightened `MatchRecoveryValidator` recovery-frame validation for `continuousEffects[]` known valid-scope `POWER_MODIFIER` items.

Current runtime builders only serialize `POWER_MODIFIER` continuous effects when a temporary power modifier has a nonzero delta. Tracked ledger entries are appended only when applied power delta is nonzero, and simple legacy power modifiers are emitted only when the aggregate until-end-of-turn power modifier is nonzero. This slice rejects readable `powerDelta` value `0` for valid-scope `POWER_MODIFIER` payloads while preserving unknown layer/scope compatibility and valid `RULE_TEXT` zero-power payloads.

## Runtime Change

- Updated `src/Riftbound.Engine/MatchRecovery.cs`.
- Added `ValidateContinuousEffectPowerModifierPowerDeltaConsistency`.
- The helper checks only known valid-scope `POWER_MODIFIER` payloads with readable integer `powerDelta`.
- Existing malformed integer, rule-text power-scalar, applied-power-delta, power-floor, foundation and non-foundation diagnostics remain in place.

## Tests

- Added `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierZeroPowerDeltaDrift`.
- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierZeroPowerDeltaDrift`.
- The spectator test keeps the count-mismatch path so same-payload diagnostics still run when authoritative parity is skipped.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "PowerModifierZeroPowerDelta"` -> `2/2`.
- Focused recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests` -> `518/518`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` -> `1099/1099`.
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `6464/6464`.
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs src tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining

This narrows P1-004 recovery/replay determinism and power-modifier continuous-effect canonicality only. Broader command/recovery/random determinism, remaining nested recovered/spectator payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
