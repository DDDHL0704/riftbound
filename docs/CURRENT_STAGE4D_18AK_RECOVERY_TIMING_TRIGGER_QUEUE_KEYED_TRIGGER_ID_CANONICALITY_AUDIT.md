# Stage 4D-18AK Recovery Timing Trigger Queue Keyed Trigger Id Canonicality Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedTriggerIdCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` keyed authoritative validation when an otherwise same authoritative trigger payload has a readable but non-canonical `triggerId` with surrounding whitespace while trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar canonicality and keyed authoritative validation.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload keeps the same trigger payload but changes `triggerId` to `" trigger-visible "` and changes `controllerId` to `bob`.
- A second trigger with `triggerId = "trigger-extra"` keeps trigger-count mismatch active.
- Assertions require the trigger-id surrounding-whitespace diagnostic, the trimmed-key controller mismatch diagnostic for `trigger-visible`, the unknown extra trigger-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed trigger-id canonicality test: `1/1`.
- Focused `TriggerQueue` filter: `364/364`.
- Focused recovery filter: `1065/1065`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1645/1645`.
- Backend full: `7011/7011`.
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
