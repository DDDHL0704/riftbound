# Stage 4D-18ET Recovery Timing Continuous Effect Keyed Metadata List Empty-Value Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura metadata-list empty-value drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListEmptyValueWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It keeps the emitted spectator payload keyed to the authoritative static-aura `effectId`, changes `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds`, `participantDependencyObjectIds` and `deferredLayerEngineResiduals` to empty arrays, and appends `effect-extra` to force count mismatch.

This slice locks the metadata-list non-empty validation and keyed authoritative metadata-list mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key metadata-list empty-value diagnostics:

- non-empty diagnostics for participant/source-dependency/target-dependency/participant-dependency object-id lists
- keyed authoritative mismatch diagnostics for all five metadata-list fields
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed metadata-list empty-value test `1/1`
- focused `ContinuousEffect` filter `160/160`
- focused recovery filter `1178/1178`
- adjacent recovery/official-opening/Postgres recovery-store filter `1759/1759`
- backend full `7124/7124`
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
