# Stage 4D-18AL Recovery Timing Trigger Queue Keyed Duplicate Id Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedDuplicateIdWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` duplicate `triggerId` validation when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing duplicate-id and keyed authoritative validation.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload keeps the authoritative trigger and appends a second payload with the same `triggerId`.
- The duplicate payload changes `controllerId` to `bob` so keyed authoritative validation still observes the duplicated trigger id.
- Assertions require the duplicate trigger-id diagnostic, the keyed controller mismatch diagnostic for `trigger-visible` and the count mismatch diagnostic.

## Validation

- Focused new keyed duplicate-id test: `1/1`.
- Focused `TriggerQueue` filter: `365/365`.
- Focused recovery filter: `1066/1066`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1646/1646`.
- Backend full was not rerun for this first post-18AK test-only micro-slice; latest backend full remains Stage 4D-18AK at `7011/7011`.
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
