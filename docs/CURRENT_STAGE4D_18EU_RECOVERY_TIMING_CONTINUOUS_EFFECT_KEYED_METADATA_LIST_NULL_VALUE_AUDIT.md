# Stage 4D-18EU Recovery Timing Continuous Effect Keyed Metadata List Null-Value Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura metadata-list null-value drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListNullValueWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It keeps the emitted spectator payload keyed to the authoritative static-aura `effectId`, changes `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds`, `participantDependencyObjectIds` and `deferredLayerEngineResiduals` to `null`, and appends `effect-extra` to force count mismatch.

This slice locks the existing null-as-absence static-aura required-list validation and keyed authoritative metadata-list mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key metadata-list null-value diagnostics:

- static-aura required-list diagnostics for source-dependency, target-dependency and battlefield participant object lists
- keyed authoritative mismatch diagnostics for all five metadata-list fields
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed metadata-list null-value test `1/1`
- focused `ContinuousEffect` filter `161/161`
- focused recovery filter `1179/1179`
- adjacent recovery/official-opening/Postgres recovery-store filter `1760/1760`
- backend full `7125/7125`
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
