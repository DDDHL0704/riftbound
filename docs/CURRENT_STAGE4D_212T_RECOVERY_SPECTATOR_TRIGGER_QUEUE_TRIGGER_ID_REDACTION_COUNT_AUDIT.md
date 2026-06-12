# Stage 4D-212T Recovery Spectator Trigger Queue Trigger Id Redaction Count Audit

Timestamp: 2026-06-12 21:14 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` trigger-id redaction sentinel validation when a trigger queue count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdRedactionSentinelWithCountMismatch` builds a spectator replay frame from authoritative visible trigger queue state.
- The test changes the spectator trigger's `triggerId` from `trigger-visible` to `HIDDEN`, appends a cloned `trigger-extra`, and forces spectator trigger queue count drift from `1` to `2`.
- Recovery validation emits the trigger-id redaction sentinel diagnostic, reports unexpected trigger ids `HIDDEN` and `trigger-extra`, reports missing authoritative trigger id `trigger-visible`, and emits the trigger queue count mismatch.
- The test intentionally does not expect positional trigger-id parity diagnostics because the validator skips positional parity when authoritative and spectator trigger queue counts differ.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1817/1817`.
- Adjacent recovery filter `MatchRecovery`: `1822/1822`.
- Backend full: `8105/8105`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `e0fc3dfd` (`test: cover spectator trigger queue trigger id redaction with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-id redaction validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
