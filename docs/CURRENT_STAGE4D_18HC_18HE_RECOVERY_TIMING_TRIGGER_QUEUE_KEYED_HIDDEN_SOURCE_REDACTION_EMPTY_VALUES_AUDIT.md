# Stage 4D-18HC-18HE Recovery Timing Trigger Queue Keyed Hidden Source Redaction Empty Values Audit

Date: 2026-06-05 16:54 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN integrated three parallel worker-produced recovery tests into `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`:

- 18HC: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceObjectIdEmptyValueWithCountMismatch` from worker commit `8e8aca977325b15792eded5632b52f4683b9b688`
- 18HD: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceVisibilityEmptyValueWithCountMismatch` from worker commit `c39db948123271e53abf7363e9ce35003443fa1d`
- 18HE: `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceEffectKindEmptyValueWithCountMismatch` from worker commit `39a76cde3ae91cbd3a81d98685daab14c10485ad`

Each test builds an authoritative hidden-source trigger queue item from real `MatchState` battlefield face-down standby object state, verifies that the spectator replay-frame timing payload emits non-redacted `controllerId = "alice"` alongside redacted `sourceObjectId = "HIDDEN"`, `sourceVisibility = "HIDDEN"` and `effectKind = "HIDDEN"`, keeps the payload keyed to authoritative `trigger-hidden`, changes exactly one redacted field to `string.Empty`, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The bundle proves the existing recovery validator still emits the relevant required diagnostic, keyed authoritative mismatch, unknown extra-trigger diagnostic and trigger-queue count mismatch for all three redacted hidden-source fields:

- `sourceObjectId = string.Empty`
- `sourceVisibility = string.Empty`
- `effectKind = string.Empty`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new redaction empty-value tests: `3/3`
- Focused `TriggerQueue` filter: `414/414`
- Focused `MatchRecoveryTests` filter: `1241/1241`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1822/1822`
- Backend full via tracked `Riftbound.slnx`: `7187/7187`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18HC-18HE stale/typo scan.

Backend full was rerun because this bundle touched the `MatchRecoveryTests` surface. A_MAIN recorded actual runner counts; the current runner totals did not move linearly with the three focused tests, but all three focused tests were discovered and passed in the main worktree.

## Subagent Review

- Euclid produced 18HC in `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hc`, committed as `8e8aca977325b15792eded5632b52f4683b9b688`, with focused `1/1`, `TriggerQueue` `411/411`, whitespace format and `git diff --check` passing in that worktree.
- Archimedes produced 18HD in `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hd`, committed as `c39db948123271e53abf7363e9ce35003443fa1d`, with focused `1/1`, `TriggerQueue` `411/411`, whitespace format and `git diff --check` passing in that worktree.
- Lorentz produced 18HE in `/Users/dinghaolin/MyProjects/riftbound-stage4d-18he`, committed as `39a76cde3ae91cbd3a81d98685daab14c10485ad`, with focused `1/1`, `TriggerQueue` `411/411`, whitespace format and `git diff --check` passing in that worktree.
- A_MAIN resolved the expected adjacent insertion conflict between 18HC and 18HD, preserved all three tests, reran main-worktree validation and owns this bundle commit.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue hidden-source redaction empty-value parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
