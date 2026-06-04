# Stage 4D-18AU Recovery Timing Trigger Queue Keyed Hidden Source Full Reveal Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceFullRevealWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key hidden source full reveal validation when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing keyed authoritative redaction validation.

## Evidence

- The test builds an authoritative hidden-source trigger queue item from real `MatchState` object state.
- The hidden source object is face-down, unit tagged and standby tagged in a battlefield location, so the spectator replay frame must redact `sourceObjectId`, `sourceVisibility` and `effectKind` to `HIDDEN`.
- The same-key spectator payload changes `sourceObjectId` to the hidden object id, `sourceVisibility` to `VISIBLE` and `effectKind` to `AMBUSH_REVEALED`, then adds `trigger-extra` to force count mismatch.
- Assertions require keyed authoritative source-object, source-visibility and effect-kind mismatch diagnostics for `trigger-hidden`, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed hidden-source full-reveal test: `1/1`.
- Focused `TriggerQueue` filter: `374/374`.
- Focused recovery filter: `1075/1075`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1656/1656`.
- Backend full was not rerun for this first post-18AT test-only micro-slice; latest backend full remains Stage 4D-18AT at `7020/7020`.
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
