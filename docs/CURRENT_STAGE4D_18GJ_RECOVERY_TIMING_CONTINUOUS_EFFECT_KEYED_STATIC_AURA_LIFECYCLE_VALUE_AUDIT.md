# Stage 4D-18GJ Recovery Timing Continuous Effect Keyed Static Aura Lifecycle Value Audit

Date: 2026-06-05 11:01 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLifecycleValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state, verifies that the spectator replay-frame timing payload emits the authoritative lifecycle `DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS`, keeps the payload keyed to the authoritative static-aura `effectId`, changes `lifecycle` to the readable wrong value `WRONG_LIFECYCLE`, then appends `effect-extra` to force spectator effect-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- battlefield static-aura lifecycle fixed-value diagnostic requiring `DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS`
- keyed authoritative lifecycle mismatch for the authoritative static-aura `effectId`
- unknown extra-effect diagnostic for `effect-extra`
- continuous-effect count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed static-aura lifecycle value test: `1/1`
- Focused `ContinuousEffect` filter: `202/202`
- Focused `MatchRecoveryTests` filter: `1220/1220`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1801/1801`
- Backend full via tracked `Riftbound.slnx`: `7166/7166`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GJ stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing continuous-effect static-aura lifecycle parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
