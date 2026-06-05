# Stage 4D-18HI-18HK Recovery Timing Trigger Queue Keyed Hidden Source Redaction Shapes Audit

Date: 2026-06-05

Owner: A_MAIN

Status: accepted on main after integrating three parallel worker commits. Project remains **NOT READY**.

## Scope

Stage 4D-18HI-18HK adds server recovery regression coverage for spectator replay-frame timing `triggerQueue[]` same-key hidden-source redaction payload-shape drift under trigger-count mismatch. Runtime validation code, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and solution files remain unchanged.

Worker source commits:

- 18HI: `80ea4910c4e4e39e487d5bfaf9880318f7204137` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hi`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceObjectIdShapeWithCountMismatch`.
- 18HJ: `15cb843e3c1a8d8740dc52b76ce6edade20017bc` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hj`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceVisibilityShapeWithCountMismatch`.
- 18HK: `d726d6acbfe4d33b318f9c059a5f6e7b5cd82475` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hk`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceEffectKindShapeWithCountMismatch`.

Each test builds an authoritative hidden-source trigger from real `MatchState` battlefield face-down standby object state, verifies the spectator payload redacts `sourceObjectId`, `sourceVisibility` and `effectKind` as `HIDDEN`, mutates exactly one redacted field to an unreadable array payload, appends `trigger-extra` to force trigger-count mismatch, and asserts the required diagnostic, keyed authoritative mismatch, unknown extra-trigger and count-mismatch diagnostics. The tests intentionally do not assert an `invalid` diagnostic because unreadable shape values are not readable strings in this validator path.

## Validation

- Worker-local validation passed for each slice: focused new test `1/1`, `FullyQualifiedName~TriggerQueue` `418/418`, scoped whitespace format and `git diff --check`.
- A_MAIN focused new shape tests: `3/3`.
- A_MAIN focused `TriggerQueue` filter: `420/420`.
- A_MAIN focused `MatchRecoveryTests` filter: `1247/1247`.
- A_MAIN adjacent recovery/official-opening/Postgres recovery-store filter: `1828/1828`.
- A_MAIN backend full via tracked `Riftbound.slnx`: `7193/7193`.
- Mechanical checks passed: scoped whitespace verification, `git diff HEAD --check`, anchored conflict-marker scan, matrix JSON parse and stale/path typo scan.

## Coordination Note

The 18HJ worker initially wrote the `sourceVisibility` shape candidate into the main worktree by mistake. A_MAIN interrupted the worker, required the work to be reproduced and committed in `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hj`, and verified main was clean before cherry-picking the official worker commits. A_MAIN integrated only the committed worker sources listed above.

## Residual Risk

This is recovery test coverage only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness.
