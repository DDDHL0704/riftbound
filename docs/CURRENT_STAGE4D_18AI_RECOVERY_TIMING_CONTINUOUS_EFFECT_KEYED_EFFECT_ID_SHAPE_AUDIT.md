# Stage 4D-18AI Recovery Timing Continuous Effect Keyed Effect Id Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedEffectIdShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` keyset validation when an otherwise same authoritative effect payload has an unreadable `effectId` shape while effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar payload and keyed keyset validation.

## Evidence

- The test builds an authoritative tracked PowerModifier continuous effect from real `MatchState` object state.
- The spectator payload keeps the authoritative payload shape but changes `effectId` to an unreadable string-array payload.
- A second effect with `effectId = "effect-extra"` and `sequence = 2` keeps effect-count mismatch active.
- Assertions require the effect-id required diagnostic, the unknown extra effect-id diagnostic, the required authoritative `effect-1` diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed effect-id shape test: `1/1`.
- Focused `ContinuousEffect` filter: `148/148`.
- Focused recovery filter: `1063/1063`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1644/1644`.
- Backend full was not rerun for this first post-18AH test-only micro-slice; latest backend full remains Stage 4D-18AH at `7008/7008`.
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
