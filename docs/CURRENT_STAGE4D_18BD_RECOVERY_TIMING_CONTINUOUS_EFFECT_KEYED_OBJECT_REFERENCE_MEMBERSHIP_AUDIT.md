# Stage 4D-18BD Recovery Timing Continuous Effect Keyed Object Reference Membership Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedObjectReferenceMembershipWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key PowerModifier target/source object-reference membership when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing object-registry membership checks and keyed authoritative validation.

## Evidence

- The test builds an authoritative PowerModifier continuous effect from real `MatchState` source/target base object state.
- The spectator payload keeps the authoritative `effectId`, then rewrites `targetObjectId` and `sourceObjectId` with object ids absent from the authoritative object registry.
- A second effect with `effectId = "effect-extra"` and `sequence = 2` keeps effect-count mismatch active.
- Assertions require object-registry membership diagnostics for target/source object ids, keyed authoritative mismatch diagnostics for those same fields, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed object-reference membership test: `1/1`.
- Focused `ContinuousEffect` filter: `152/152`.
- Focused recovery filter: `1084/1084`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1665/1665`.
- Backend full was not rerun for this first post-18BC test-only micro-slice; latest backend full remains Stage 4D-18BC at `7029/7029`.
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
