# Stage 4D-18BW Recovery Timing Trigger Queue Keyed Visible Source Identity Required Field Absence Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceIdentityRequiredFieldAbsenceWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key visible-source identity required-field absence when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing required-field and keyed authoritative trigger-queue parity.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload starts with `controllerId = "alice"` and `triggeredByEventKind = "OBJECT_DESTROYED"` emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test removes `controllerId` and `triggeredByEventKind` while keeping the authoritative `triggerId`, `sourceObjectId`, `sourceVisibility` and `effectKind` readable for keyed lookup.
- The test appends `trigger-extra` to force trigger-count mismatch.
- Assertions require controller/event-kind required-field diagnostics, keyed authoritative identity mismatch diagnostics, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed visible-source identity required-field absence test: `1/1`.
- Focused `TriggerQueue` filter: `394/394`.
- Focused recovery filter: `1103/1103`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1684/1684`.
- Backend full: not rerun for this first post-18BV test-only micro-slice; latest full remains Stage 4D-18BV at `7048/7048`.
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
