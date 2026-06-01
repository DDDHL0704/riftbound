# Stage 4D-17QK Recovery Timing Continuous Effect Power Modifier Foundation Tracked Source Object Audit

Date: 2026-06-01 16:25 CST

Status: accepted for this checkpoint slice. Project remains **NOT READY**.

## Scope

A_MAIN tightened `MatchRecoveryValidator` recovery-frame validation for `continuousEffects[]` known valid-scope `POWER_MODIFIER` items carrying foundation-only LayerEngine status or readable deferred LayerEngine residuals.

Tracked ledger power modifiers currently serialize a non-null `sourceObjectId` from stack/direct runtime paths together with `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower`, `resultingPower` and `appliedOrder`. Legacy foundation remainders carry no tracked modifier scalar/order fields and may keep `sourceObjectId` null. This slice rejects missing source object ids only after at least one tracked scalar/order value is readable, preserving legacy remainder compatibility.

## Runtime Change

- Updated `src/Riftbound.Engine/MatchRecovery.cs`.
- Extended `ValidateContinuousEffectPowerModifierFoundationModifierScalarCompleteness`.
- The helper now requires a non-null `sourceObjectId` for foundation `POWER_MODIFIER` payloads when tracked modifier scalar/order fields are present.
- Existing malformed optional-int, scalar-completeness, applied-power-delta consistency and power-floor diagnostics remain in place.

## Tests

- Added `RecoveryValidatorRejectsSnapshotTimingContinuousEffectPowerModifierFoundationTrackedSourceObjectDrift`.
- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectPowerModifierFoundationTrackedSourceObjectDrift`.
- The spectator test keeps the count-mismatch path so same-payload diagnostics still run when authoritative parity is skipped.

## Validation

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "PowerModifierFoundationTrackedSourceObject"` -> `2/2`.
- Focused recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter MatchRecoveryTests` -> `514/514`.
- Adjacent: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "MatchRecoveryTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests"` -> `1095/1095`.
- Full backend: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `6460/6460`.
- Mechanical: `git diff --check`, anchored conflict-marker scan over `docs src tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Remaining

This narrows P1-004 recovery/replay determinism and continuous-effect foundation tracked-source canonicality only. Broader command/recovery/random determinism, remaining nested recovered/spectator payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
