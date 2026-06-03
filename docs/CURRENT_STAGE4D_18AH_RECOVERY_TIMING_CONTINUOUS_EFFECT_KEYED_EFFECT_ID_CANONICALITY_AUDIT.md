# Stage 4D-18AH Recovery Timing Continuous Effect Keyed Effect Id Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedEffectIdCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key authoritative validation when `effectId` is readable but non-canonical due to surrounding whitespace.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed validation.

## Evidence

- The test builds an authoritative tracked PowerModifier continuous effect from real `MatchState` object state.
- The spectator payload keeps the same authoritative effect id only after trimming, using `effectId = " effect-1 "`.
- The same payload mutates `sequence` to `2`, and an extra effect with `sequence = 3` keeps effect-count mismatch active so broad ordered parity is skipped.
- Assertions require effect-id surrounding-whitespace diagnostics, keyed authoritative sequence mismatch diagnostics and the count mismatch diagnostic.

## Validation

- Focused new keyed effect-id canonicality test: `1/1`.
- Focused `ContinuousEffect` filter: `147/147`.
- Focused recovery filter: `1062/1062`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1643/1643`.
- Backend full passed: `7008/7008`.
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
