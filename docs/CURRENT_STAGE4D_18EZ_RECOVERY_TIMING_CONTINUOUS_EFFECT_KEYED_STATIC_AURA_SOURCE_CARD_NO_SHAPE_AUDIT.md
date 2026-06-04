# Stage 4D-18EZ Recovery Timing Continuous Effect Keyed Static Aura Source Card No Shape Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura source-card-number shape drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceCardNoShapeWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It verifies the emitted spectator payload has the expected `sourceCardNo`, keeps the payload keyed to the authoritative static-aura `effectId`, changes `sourceCardNo` to an unreadable array shape, and appends `effect-extra` to force count mismatch.

This slice locks the existing source-card-number payload-shape validation and keyed authoritative source-card-number mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key source-card-number shape diagnostics:

- source-card-number invalid diagnostic
- keyed authoritative source-card-number mismatch diagnostic
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed static-aura source-card-number shape test `1/1`
- focused `ContinuousEffect` filter `166/166`
- focused recovery filter `1184/1184`
- adjacent recovery/official-opening/Postgres recovery-store filter `1765/1765`
- backend full `7130/7130`
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
