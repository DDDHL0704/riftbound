# Stage 4D-18AN Recovery Timing Trigger Queue Keyed Known Value Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedKnownValueCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key known-value and canonicality validation when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing known-value, canonicality and keyed authoritative validation.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload keeps a readable `triggerId = "trigger-visible"` so it remains keyed to the authoritative trigger.
- The same-key payload changes `controllerId` to `bob`, adds surrounding whitespace to `sourceObjectId` and `effectKind`, sets `sourceVisibility` to `UNKNOWN`, and sets `triggeredByEventKind` to `FORGED_EVENT`, then adds `trigger-extra` to force count mismatch.
- Assertions require canonicality diagnostics, known-value diagnostics, keyed authoritative mismatch diagnostics for `trigger-visible`, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed known-value/canonicality test: `1/1`.
- Focused `TriggerQueue` filter: `367/367`.
- Focused recovery filter: `1068/1068`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1649/1649`.
- Backend full: `7014/7014`.
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
