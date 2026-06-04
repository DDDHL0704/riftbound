# Stage 4D-18BL Recovery Timing Trigger Queue Keyed Hidden Source Trigger Id Shape Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggerIdShapeWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` hidden-source trigger-id required-field shape validation when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing trigger-id required-field validation and authoritative trigger-queue keyset parity.

## Evidence

- The test builds an authoritative hidden-source trigger from real `MatchState` battlefield/standby object state.
- The spectator payload starts with `sourceObjectId`, `sourceVisibility` and `effectKind` redacted to `HIDDEN`, preserving the hidden-source redaction contract.
- The payload changes `triggerId` to an unreadable array shape while keeping public identity and source/effect redaction intact.
- A second payload with `triggerId = "trigger-extra"` keeps trigger-count mismatch active.
- Assertions require the trigger-id required diagnostic, the missing authoritative `trigger-hidden` diagnostic, the unknown `trigger-extra` diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed hidden-source trigger-id shape test: `1/1`.
- Focused `TriggerQueue` filter: `387/387`.
- Focused recovery filter: `1092/1092`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1673/1673`.
- Backend full was not rerun for this second post-18BJ test-only micro-slice; latest full remains Stage 4D-18BJ at `7036/7036`.
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
