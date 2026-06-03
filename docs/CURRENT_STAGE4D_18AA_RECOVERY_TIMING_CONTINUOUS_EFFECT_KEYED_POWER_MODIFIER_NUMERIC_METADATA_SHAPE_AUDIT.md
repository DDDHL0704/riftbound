# Stage 4D-18AA Recovery Timing Continuous Effect Keyed PowerModifier Numeric Metadata Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedPowerModifierNumericMetadataShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key authoritative validation for expected-present PowerModifier LayerEngine numeric metadata fields with unreadable shapes.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed validation.

## Evidence

- The test builds an authoritative tracked PowerModifier continuous effect from real `MatchState` object state and `PowerModifierLedgerEntry`.
- The same spectator `effectId` payload mutates `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower`, `resultingPower` and `appliedOrder` to non-int shapes.
- An extra continuous effect is appended so effect-count mismatch remains active and broad ordered parity is skipped.
- Assertions require same-payload shape diagnostics plus keyed authoritative mismatch diagnostics for all mutated optional numeric metadata fields.

## Validation

- Focused new keyed PowerModifier numeric metadata shape test: `1/1`.
- Focused `ContinuousEffect` filter: `140/140`.
- Focused recovery filter: `1055/1055`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1636/1636`.
- Touched-file scoped whitespace format passed.
- `git diff --check` passed.
- Anchored conflict-marker scan over `docs`, `tests` and `src` passed.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.
- Backend full was not rerun for this second post-17ZY test-only micro-slice; latest full remains Stage 4D-17ZY at `6999/6999`.

## Open

- Broader command/recovery/random determinism remains open.
- Remaining recovered/spectator/authoritative nested payload shape/value breadth remains open.
- Full LayerEngine breadth remains open.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial` and final readiness remain open.
