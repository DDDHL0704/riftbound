# Stage 4D-18FM Recovery Timing Continuous Effect Keyed Static Aura Source Card No Canonicality Audit

Date: 2026-06-05
Owner: A_MAIN
Status: Accepted / write lock closed

## Scope

A_MAIN added a focused server recovery regression test for spectator replay-frame timing `continuousEffects[]` same-key static-aura source-card-number canonicality drift under effect-count mismatch.

The new `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceCardNoCanonicalityWithCountMismatch` coverage builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state. It verifies the emitted spectator payload has `sourceCardNo = "OGN·294/298"`, keeps the payload keyed to the authoritative static-aura `effectId`, wraps `sourceCardNo` in surrounding whitespace, and appends `effect-extra` to force count mismatch.

This slice locks the existing source-card-number surrounding-whitespace canonicality path and keyed authoritative source-card-number mismatch path under the count-mismatch branch. It intentionally does not expect source-card-number required or static-aura source-card-number required diagnostics for the surrounding-whitespace case because scalar normalization preserves the trimmed non-empty value for same-payload metadata checks.

## Expected Diagnostics

The test locks the existing recovery validator behavior that count mismatch must not hide same-key source-card-number canonicality diagnostics:

- source-card-number surrounding-whitespace diagnostic
- keyed authoritative source-card-number mismatch diagnostic
- unknown extra continuous-effect id `effect-extra`
- effect-count mismatch `2` vs `1`

## Validation

Validation passed:

- focused new keyed static-aura source-card-number canonicality test `1/1`
- focused `ContinuousEffect` filter `179/179`
- focused recovery filter `1197/1197`
- adjacent recovery/official-opening/Postgres recovery-store filter `1778/1778`
- backend full `7143/7143`
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
