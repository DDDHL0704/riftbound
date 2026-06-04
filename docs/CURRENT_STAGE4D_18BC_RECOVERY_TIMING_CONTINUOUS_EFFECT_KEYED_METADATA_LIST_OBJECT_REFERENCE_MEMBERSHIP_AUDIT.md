# Stage 4D-18BC Recovery Timing Continuous Effect Keyed Metadata List Object Reference Membership Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectKeyedMetadataListObjectReferenceMembershipWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `continuousEffects[]` same-key static-aura metadata-list object-reference membership when effect-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing object-registry membership checks and keyed authoritative validation.

## Evidence

- The test builds an authoritative battlefield static-aura continuous effect from real `MatchState` battlefield/unit object state.
- The spectator payload keeps the authoritative `effectId`, then rewrites `participantObjectIds`, `sourceDependencyObjectIds`, `targetDependencyObjectIds` and `participantDependencyObjectIds` with object ids absent from the authoritative object registry.
- A second effect with `effectId = "effect-extra"` and `sequence = 2` keeps effect-count mismatch active.
- Assertions require object-registry membership diagnostics for all four object-reference metadata lists, keyed authoritative mismatch diagnostics for those metadata-list fields, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed metadata-list object-reference membership test: `1/1`.
- Focused `ContinuousEffect` filter: `151/151`.
- Focused recovery filter: `1083/1083`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1664/1664`.
- Backend full: `7029/7029`.
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
