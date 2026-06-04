# Stage 4D-18BY Recovery Timing Trigger Queue Keyed Visible Source Duplicate Id Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceDuplicateIdWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` duplicate same-key visible-source trigger id when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing duplicate-id and keyed authoritative trigger-queue parity.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload starts with `triggerId = "trigger-visible"`, `controllerId = "alice"`, `sourceObjectId = "visible-source-1"`, `sourceVisibility = "VISIBLE"`, `effectKind = "LAST_BREATH"` and `triggeredByEventKind = "OBJECT_DESTROYED"` emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test appends a duplicate payload with the same `triggerId` but readable drift values: `controllerId = "bob"`, `sourceObjectId = "other-visible-source-1"`, `effectKind = "AMBUSH_REVEALED"` and `triggeredByEventKind = "BATTLEFIELD_HELD"`.
- The test keeps the alternate visible source in the object registry so the assertions target duplicate-id and keyed authoritative validation instead of membership or shape validation.
- Assertions require duplicate-id, keyed authoritative controller/source/effect/event mismatch and count mismatch diagnostics.

## Validation

- Focused new keyed visible-source duplicate-id test: `1/1`.
- Focused `TriggerQueue` filter: `396/396`.
- Focused recovery filter: `1105/1105`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1686/1686`.
- Backend full: `7051/7051`.
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
