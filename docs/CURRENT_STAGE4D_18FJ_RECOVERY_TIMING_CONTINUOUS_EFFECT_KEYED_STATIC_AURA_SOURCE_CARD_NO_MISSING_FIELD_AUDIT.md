# Stage 4D-18FJ Recovery Timing Continuous Effect Keyed Static Aura Source Card No Missing Field Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura source-card-number missing-field drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceCardNoMissingFieldWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It verifies the emitted spectator payload has `sourceCardNo = "OGN·294/298"`, keeps the payload keyed to the authoritative static-aura `effectId`, removes `sourceCardNo`, and appends `effect-extra` to force count mismatch.

This slice locks the existing static-aura source-card-number required metadata path and keyed authoritative source-card-number mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key source-card-number missing-field diagnostics:

- static-aura source-card-number required diagnostic
- keyed authoritative source-card-number mismatch diagnostic
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed static-aura source-card-number missing-field test `1/1`
- focused `ContinuousEffect` filter `176/176`
- focused recovery filter `1194/1194`
- adjacent recovery/official-opening/Postgres recovery-store filter `1775/1775`
- backend full `7140/7140`
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
