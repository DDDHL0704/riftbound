# Stage 4D-18FU Recovery Timing Continuous Effect Keyed Static Aura Layer Engine Status Value Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura layer-engine-status value drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLayerEngineStatusValueWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It verifies the emitted spectator payload has `layerEngineStatus = "FOUNDATION_ONLY"`, keeps the payload keyed to the authoritative static-aura `effectId`, changes `layerEngineStatus` to `UNKNOWN_STATUS`, and appends `effect-extra` to force count mismatch.

This slice locks the existing layer-engine-status known-value rule, static-aura foundation-only layer-engine-status rule and keyed authoritative layer-engine-status mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key static-aura layer-engine-status value diagnostics:

- layer-engine-status unknown-value diagnostic for `UNKNOWN_STATUS`
- static-aura requires foundation-only layer-engine-status diagnostic
- keyed authoritative layer-engine-status mismatch diagnostic
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed static-aura layer-engine-status value test `1/1`
- focused `ContinuousEffect` filter `187/187`
- focused recovery filter `1205/1205`
- adjacent recovery/official-opening/Postgres recovery-store filter `1786/1786`
- backend full `7151/7151`
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
