# Stage 4D-18FP Recovery Timing Continuous Effect Keyed Static Aura Effect Kind Shape Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura effect-kind shape drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraEffectKindShapeWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It verifies the emitted spectator payload has `effectKind = "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE"`, keeps the payload keyed to the authoritative static-aura `effectId`, changes `effectKind` to an unreadable array payload, and appends `effect-extra` to force count mismatch.

This slice locks the existing generic effect-kind payload-shape path, static-aura effect-kind required metadata path, and keyed authoritative effect-kind mismatch path under the count-mismatch branch.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key static-aura effect-kind shape diagnostics:

- effect-kind invalid diagnostic
- static-aura effect-kind required diagnostic
- keyed authoritative effect-kind mismatch diagnostic
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed static-aura effect-kind shape test `1/1`
- focused `ContinuousEffect` filter `182/182`
- focused recovery filter `1200/1200`
- adjacent recovery/official-opening/Postgres recovery-store filter `1781/1781`
- backend full `7146/7146`
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
