# Stage 4D-18GL Recovery Timing Continuous Effect Keyed Static Aura Condition Canonicality Audit

Date: 2026-06-05 11:23 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraConditionCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state, verifies that the spectator replay-frame timing payload emits the authoritative condition `SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD`, keeps the payload keyed to the authoritative static-aura `effectId`, wraps `condition` in surrounding whitespace, then appends `effect-extra` to force spectator effect-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- condition scalar canonicality diagnostic for surrounding whitespace
- keyed authoritative condition mismatch for the authoritative static-aura `effectId`
- unknown extra-effect diagnostic for `effect-extra`
- continuous-effect count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed static-aura condition canonicality test: `1/1`
- Focused `ContinuousEffect` filter: `204/204`
- Focused `MatchRecoveryTests` filter: `1222/1222`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1803/1803`
- Backend full via tracked `Riftbound.slnx`: `7168/7168`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GL stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing continuous-effect static-aura condition parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
