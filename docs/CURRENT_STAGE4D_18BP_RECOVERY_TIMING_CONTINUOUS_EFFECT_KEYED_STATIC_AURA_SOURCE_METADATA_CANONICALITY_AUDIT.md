# Stage 4D-18BP Recovery Timing Continuous Effect Keyed Static Aura Source Metadata Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraSourceMetadataCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key static-aura source metadata canonicality when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar canonicality and keyed authoritative continuous-effect parity.

## Evidence

- The test builds authoritative static-aura continuous effects from real `MatchState` battlefield and participant object state.
- The spectator payload starts with canonical static-aura `sourceCardNo` and `layerEngineStatus` values emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test reads those emitted values and mutates them to whitespace-padded strings while keeping the authoritative `effectId` readable for keyed lookup.
- The test appends `effect-extra` to force effect-count mismatch.
- Assertions require source-card/layer-engine-status surrounding-whitespace diagnostics, keyed authoritative source-card/layer-engine-status mismatch diagnostics, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed static-aura source metadata canonicality test: `1/1`.
- Focused `ContinuousEffect` filter: `155/155`.
- Focused recovery filter: `1096/1096`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1677/1677`.
- Backend full: `7042/7042`.
- Backend full was rerun because this is the third post-18BM test-only micro-slice.
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
