# Stage 4D-18FH Recovery Timing Continuous Effect Keyed Static Aura Source Path Empty Value Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura source-path empty-value drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourcePathEmptyValueWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It verifies the emitted spectator payload has `sourcePath = "CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus"`, keeps the payload keyed to the authoritative static-aura `effectId`, changes `sourcePath` to `string.Empty`, and appends `effect-extra` to force count mismatch.

This slice locks the existing source-path required-scalar path, static-aura source-path required metadata path and keyed authoritative source-path mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key source-path empty-value diagnostics:

- source-path required diagnostic
- static-aura source-path required diagnostic
- keyed authoritative source-path mismatch diagnostic
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed static-aura source-path empty-value test `1/1`
- focused `ContinuousEffect` filter `174/174`
- focused recovery filter `1192/1192`
- adjacent recovery/official-opening/Postgres recovery-store filter `1773/1773`
- backend full `7138/7138`
- touched-file scoped whitespace format

Mechanical validation passed:

- `git diff --check`
- anchored conflict-marker scan over `docs`, `tests`, and `src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- path typo scan

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Locks

Runtime validation code, matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and `riftbound-dotnet.sln` remain locked.

Project remains **NOT READY**.
