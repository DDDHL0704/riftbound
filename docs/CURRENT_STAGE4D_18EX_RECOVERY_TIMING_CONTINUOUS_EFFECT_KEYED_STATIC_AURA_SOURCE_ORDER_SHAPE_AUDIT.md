# Stage 4D-18EX Recovery Timing Continuous Effect Keyed Static Aura Source Order Shape Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura source-order shape drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceOrderShapeWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It verifies the emitted spectator payload has a positive `sourceOrder`, keeps the payload keyed to the authoritative static-aura `effectId`, changes `sourceOrder` to an unreadable array shape, and appends `effect-extra` to force count mismatch.

This slice locks the existing source-order payload-shape validation and keyed authoritative source-order mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key source-order shape diagnostics:

- source-order invalid diagnostic
- keyed authoritative source-order mismatch diagnostic
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed static-aura source-order shape test `1/1`
- focused `ContinuousEffect` filter `164/164`
- focused recovery filter `1182/1182`
- adjacent recovery/official-opening/Postgres recovery-store filter `1763/1763`
- backend full `7128/7128`
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
