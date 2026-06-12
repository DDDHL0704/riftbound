# Stage 4D-213C Recovery Spectator Trigger Queue Visible Source Visibility Redaction Count Audit

Timestamp: 2026-06-12 22:20 CST

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` visible source visibility redaction and authoritative source-visibility mismatch validation when a trigger queue count mismatch is also present.
- Added one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueAuthoritativeVisibleSourceVisibilityRedactionWithCountMismatch` builds a spectator replay frame from authoritative trigger queue state with one visible source object `visible-source-1`.
- The test changes the redacted spectator payload's `sourceVisibility` to `HIDDEN`, leaves the visible source object id and effect kind exposed, appends a cloned `trigger-extra`, and forces spectator trigger queue count drift from `1` to `2`.
- Recovery validation emits the hidden-source source-object redaction diagnostic, the hidden-source effect-kind redaction diagnostic, the keyed authoritative source-visibility mismatch for `trigger-visible`, the unexpected `trigger-extra` diagnostic, and the trigger queue count mismatch.
- This complements the existing visible source visibility redaction test that verifies the same redaction and keyed mismatch path without a count mismatch.

## Validation

- Focused test: `1/1`.
- Changed-class filter `MatchRecoveryTests`: `1826/1826`.
- Adjacent recovery filter `MatchRecovery`: `1831/1831`.
- Backend full: `8114/8114`.
- `git diff --check`: passed.
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: passed.

## Commits

- Code commit: `68493167` (`test: cover spectator trigger queue source visibility redaction with count drift`).
- Docs checkpoint: pending at document creation time.

## Remaining Risk

- This narrows spectator replay timing trigger-queue visible source visibility redaction validation with count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.
- Project remains **NOT READY**.
