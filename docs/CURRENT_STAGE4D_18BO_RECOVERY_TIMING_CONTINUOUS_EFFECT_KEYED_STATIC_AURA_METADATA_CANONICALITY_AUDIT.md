# Stage 4D-18BO Recovery Timing Continuous Effect Keyed Static Aura Metadata Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedStaticAuraMetadataCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key static-aura metadata canonicality when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar canonicality and keyed authoritative continuous-effect parity.

## Evidence

- The test builds authoritative static-aura continuous effects from real `MatchState` battlefield and participant object state.
- The spectator payload starts with canonical static-aura `condition` and `lifecycle` values emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test reads those emitted values and mutates them to whitespace-padded strings while keeping the authoritative `effectId` readable for keyed lookup.
- The test appends `effect-extra` to force effect-count mismatch.
- Assertions require condition/lifecycle surrounding-whitespace diagnostics, keyed authoritative condition/lifecycle mismatch diagnostics, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed static-aura metadata canonicality test: `1/1`.
- Focused `ContinuousEffect` filter: `154/154`.
- Focused recovery filter: `1095/1095`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1676/1676`.
- Backend full was not rerun for this second post-18BM test-only micro-slice; latest backend full remains Stage 4D-18BM at `7039/7039`.
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
