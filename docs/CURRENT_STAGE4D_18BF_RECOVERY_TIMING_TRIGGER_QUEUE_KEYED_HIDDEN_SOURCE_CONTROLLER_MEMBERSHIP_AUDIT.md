# Stage 4D-18BF Recovery Timing Trigger Queue Keyed Hidden Source Controller Membership Audit

Date: 2026-06-04

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceControllerMembershipWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Covered spectator replay-frame timing `triggerQueue[]` same-key hidden-source controller membership when trigger-count mismatch skips broad ordered parity.
- Runtime validation code was not changed; this is a recovery conformance coverage slice for existing controller seat membership checks and keyed authoritative trigger-queue parity.

## Evidence

- The test builds an authoritative hidden-source trigger from real `MatchState` battlefield/standby object state.
- The spectator payload starts with `sourceObjectId`, `sourceVisibility` and `effectKind` redacted to `HIDDEN`, preserving the hidden-source redaction contract.
- The same-key payload keeps the authoritative `triggerId`, mutates `controllerId` to `charlie`, and adds a second payload with `triggerId = "trigger-extra"` to keep trigger-count mismatch active.
- Assertions require the missing-seat controller diagnostic, the same-key controller authoritative mismatch, the unknown extra-id diagnostic and the count mismatch diagnostic.

## Validation

- Focused new keyed hidden-source controller membership test: `1/1`.
- Focused `TriggerQueue` filter: `381/381`.
- Focused recovery filter: `1086/1086`.
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1667/1667`.
- Backend full: `7032/7032`.
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
