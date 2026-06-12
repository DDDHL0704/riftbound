# Stage 4D-212Z Recovery Spectator Trigger Queue Controller Membership Count Audit

Timestamp: 2026-06-12 21:58 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` controller membership and authoritative controller mismatch validation when a trigger queue count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueAuthoritativeControllerMismatchWithCountMismatch` builds a spectator replay frame from authoritative trigger queue state with one visible trigger controlled by `alice`.
- The test changes the redacted spectator payload's `controllerId` to `charlie`, appends a cloned `trigger-extra`, and forces spectator trigger queue count drift from `1` to `2`.
- Recovery validation emits the missing-seat diagnostic for `charlie`, the keyed authoritative controller mismatch for `trigger-visible`, the unexpected `trigger-extra` diagnostic, and the trigger queue count mismatch.
- This complements the existing narrower controller membership count-drift test that injects an unexpected trigger into an empty authoritative queue.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1823/1823`.
- Adjacent recovery filter `MatchRecovery`: `1828/1828`.
- Backend full: `8111/8111`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `8e9e3b2` (`test: cover spectator trigger queue controller membership with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-queue controller membership validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
