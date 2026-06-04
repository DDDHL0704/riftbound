# Stage 4D-18BM Recovery Timing Trigger Queue Keyed Hidden Source Duplicate Id Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` hidden-source duplicate trigger-id validation when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing duplicate trigger-id validation and keyed authoritative trigger-queue parity.

## Evidence

- The test builds an authoritative hidden-source trigger from real `MatchState` battlefield/standby object state.
- The spectator payload starts with `sourceObjectId`, `sourceVisibility` and `effectKind` redacted to `HIDDEN`, preserving the hidden-source redaction contract.
- The test appends a duplicate payload with the same `triggerId = "trigger-hidden"` while keeping source/effect redaction intact.
- The duplicate payload drifts `controllerId` to `bob` and `triggeredByEventKind` to `OBJECT_DESTROYED`.
- Assertions require the duplicate trigger-id diagnostic, keyed controller and triggered-event-kind authoritative mismatches, and the count mismatch diagnostic.

## Validation

- Focused new keyed hidden-source duplicate-id test: `1/1`.
- Focused `TriggerQueue` filter: `388/388`.
- Focused recovery filter: `1093/1093`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1674/1674`.
- Backend full: `7039/7039`.
- Backend full was rerun because this is the third post-18BJ test-only micro-slice.
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
