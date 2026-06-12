# Stage 4D-213A Recovery Spectator Trigger Queue Visible Source Object Membership Count Audit

Timestamp: 2026-06-12 22:07 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` visible source object membership and authoritative source-object mismatch validation when a trigger queue count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueAuthoritativeVisibleSourceObjectMismatchWithCountMismatch` builds a spectator replay frame from authoritative trigger queue state with one visible source object `visible-source-1`.
- The test changes the redacted spectator payload's `sourceObjectId` to `missing-source`, appends a cloned `trigger-extra`, and forces spectator trigger queue count drift from `1` to `2`.
- Recovery validation emits the missing-object-registry diagnostic for `missing-source`, the keyed authoritative source-object mismatch for `trigger-visible`, the unexpected `trigger-extra` diagnostic, and the trigger queue count mismatch.
- This complements the existing narrower visible source object membership count-drift test that injects an unexpected trigger into an empty authoritative queue.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1824/1824`.
- Adjacent recovery filter `MatchRecovery`: `1829/1829`.
- Backend full: `8112/8112`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `7554803f` (`test: cover spectator trigger queue visible source membership with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-queue visible source object membership validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
