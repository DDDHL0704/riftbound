# Stage 4D-18AJ Recovery Timing Trigger Queue Keyed Trigger Id Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedTriggerIdShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` keyset validation when an otherwise same authoritative trigger payload has an unreadable `triggerId` shape while trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing scalar payload and keyed keyset validation.

## Evidence

- The test builds an authoritative visible-source trigger queue item from real `MatchState` object state.
- The spectator payload keeps the authoritative payload shape but changes `triggerId` to an unreadable string-array payload.
- A second trigger with `triggerId = "trigger-extra"` keeps trigger-count mismatch active.
- Assertions require the trigger-id required diagnostic, the unknown extra trigger-id diagnostic, the required authoritative `trigger-visible` diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed trigger-id shape test: `1/1`.
- Focused `TriggerQueue` filter: `363/363`.
- Focused recovery filter: `1064/1064`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1645/1645`.
- Backend full was not rerun for this second post-18AH test-only micro-slice; latest backend full remains Stage 4D-18AH at `7008/7008`.
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
