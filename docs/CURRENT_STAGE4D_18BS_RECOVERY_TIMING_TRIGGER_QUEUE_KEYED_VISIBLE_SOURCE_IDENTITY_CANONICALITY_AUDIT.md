# Stage 4D-18BS Recovery Timing Trigger Queue Keyed Visible Source Identity Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceIdentityCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key visible-source identity canonicality when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar canonicality and keyed authoritative trigger-queue parity.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload starts with `controllerId = "alice"` and `triggeredByEventKind = "OBJECT_DESTROYED"` emitted by `MatchReplayRedactor.BuildSpectatorFrame`.
- The test mutates those fields to `" alice "` and `" OBJECT_DESTROYED "` while keeping the authoritative `triggerId` readable for keyed lookup.
- The test appends `trigger-extra` to force trigger-count mismatch.
- Assertions require controller/event-kind surrounding-whitespace diagnostics, keyed authoritative controller/event-kind mismatch diagnostics, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed visible-source identity canonicality test: `1/1`.
- Focused `TriggerQueue` filter: `390/390`.
- Focused recovery filter: `1099/1099`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1680/1680`.
- Backend full: `7045/7045`.
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
