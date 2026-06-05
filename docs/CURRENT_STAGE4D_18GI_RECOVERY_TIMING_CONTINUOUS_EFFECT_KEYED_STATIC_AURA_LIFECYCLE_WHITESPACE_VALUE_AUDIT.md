# Stage 4D-18GI Recovery Timing Continuous Effect Keyed Static Aura Lifecycle Whitespace Value Audit

Date: 2026-06-05 10:49 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraLifecycleWhitespaceValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state, verifies that the spectator replay-frame timing payload emits the authoritative lifecycle `DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS`, keeps the payload keyed to the authoritative static-aura `effectId`, changes `lifecycle` to the whitespace-only string `"   "`, then appends `effect-extra` to force spectator effect-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- `spectator replay frame timing continuous effect item lifecycle is required`
- `spectator replay frame timing continuous effect item static aura lifecycle is required`
- keyed authoritative lifecycle mismatch for the authoritative static-aura `effectId`
- unknown extra-effect diagnostic for `effect-extra`
- continuous-effect count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed static-aura lifecycle whitespace-value test: `1/1`
- Focused `ContinuousEffect` filter: `201/201`
- Focused `MatchRecoveryTests` filter: `1219/1219`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1800/1800`
- Backend full via tracked `Riftbound.slnx`: `7165/7165`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GI stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing continuous-effect static-aura lifecycle parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
