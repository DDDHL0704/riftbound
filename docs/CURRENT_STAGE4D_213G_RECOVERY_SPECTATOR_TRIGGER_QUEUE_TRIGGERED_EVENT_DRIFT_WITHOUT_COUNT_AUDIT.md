# Stage 4D-213G Recovery Spectator Trigger Queue Triggered Event Drift Without Count Audit

Timestamp: 2026-06-12 22:49 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` triggered-event-kind drift and authoritative triggered-event-kind mismatch validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggeredEventKindDriftWithoutCountMismatch` builds a spectator replay frame from authoritative trigger queue state with one visible source object `source-1`.
- The test changes the spectator trigger's `triggeredByEventKind` to invalid `FORGED_EVENT` while preserving the authoritative trigger queue count.
- Recovery validation emits the invalid triggered-event-kind diagnostic, the keyed authoritative triggered-event-kind mismatch for `trigger-1`, and the aggregate triggered-event-kind disagreement diagnostic while preserving no trigger queue count mismatch.
- This complements the existing triggered-event-kind drift test with count mismatch that also proves the unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1829/1829`.
- Adjacent recovery filter `MatchRecovery`: `1834/1834`.
- Backend full: `8117/8117`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `fb21ecab` (`test: cover spectator trigger queue event drift without count`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-queue triggered-event-kind drift validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
