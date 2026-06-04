# Stage 4D-18BQ Recovery Timing Continuous Effect Keyed Static Aura Source Order Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceOrderCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key static-aura source-order canonicality when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing positive-int canonicality and keyed authoritative continuous-effect parity.

## Evidence

- The test builds authoritative static-aura continuous effects from real `MatchState` battlefield and participant object state.
- The spectator payload starts with a positive static-aura `sourceOrder` emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test mutates that `sourceOrder` to `-1` while keeping the authoritative `effectId` readable for keyed lookup.
- The test appends `effect-extra` to force effect-count mismatch.
- Assertions require the positive source-order diagnostic, keyed authoritative source-order mismatch diagnostic, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed static-aura source-order canonicality test: `1/1`.
- Focused `ContinuousEffect` filter: `156/156`.
- Focused recovery filter: `1097/1097`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1678/1678`.
- Backend full was not rerun for this first post-18BP test-only micro-slice; latest backend full remains Stage 4D-18BP at `7042/7042`.
- Touched-file scoped whitespace format passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src` passed.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Path typo scan for `tests\.Riftbound` over `docs`, `tests` and `src` passed.

## Open

- Broader command/recovery/random determinism remains open.
- Remaining recovered/spectator/authoritative nested payload shape/value breadth remains open.
- Full LayerEngine breadth remains open.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
