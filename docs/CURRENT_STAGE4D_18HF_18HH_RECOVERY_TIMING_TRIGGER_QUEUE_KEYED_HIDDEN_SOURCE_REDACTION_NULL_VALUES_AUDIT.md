# Stage 4D-18HF-18HH Recovery Timing Trigger Queue Keyed Hidden Source Redaction Null Values Audit

Date: 2026-06-05

Owner: A_MAIN

Status: accepted on main after integrating three parallel worker commits. Project remains **NOT READY**.

## Scope

Stage 4D-18HF-18HH adds server recovery regression coverage for spectator replay-frame timing `triggerQueue[]` same-key hidden-source redaction null-value drift under trigger-count mismatch. Runtime validation code, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E, `fullOfficial`, final status and solution files remain unchanged.

Worker source commits:

- 18HF: `2f0962a97d437ce04389e3e2ac160e35bb34c447` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hf`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceObjectIdNullValueWithCountMismatch`.
- 18HG: `5417b4da5d8cb2502c839a5e9fd71364dc090516` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hg`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceVisibilityNullValueWithCountMismatch`.
- 18HH: `598c544f71ae04d422fcbe14e7d8081f312096bf` from `/Users/dinghaolin/MyProjects/riftbound-stage4d-18hh`, adding `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceEffectKindNullValueWithCountMismatch`.

Each test builds an authoritative hidden-source trigger from real `MatchState` battlefield face-down standby object state, verifies the spectator payload redacts `sourceObjectId`, `sourceVisibility` and `effectKind` as `HIDDEN`, mutates exactly one redacted field to `null`, appends `trigger-extra` to force trigger-count mismatch, and asserts the required diagnostic, keyed authoritative mismatch, unknown extra-trigger and count-mismatch diagnostics. The keyed mismatch text intentionally preserves the validator's current empty actual-value slot for `null` values.

## Validation

- Worker-local validation passed for each slice: focused new test `1/1`, `FullyQualifiedName~TriggerQueue` `415/415`, scoped whitespace format and `git diff --check`.
- A_MAIN focused new null-value tests: `3/3`.
- A_MAIN focused `TriggerQueue` filter: `417/417`.
- A_MAIN focused `MatchRecoveryTests` filter: `1244/1244`.
- A_MAIN adjacent recovery/official-opening/Postgres recovery-store filter: `1825/1825`.
- A_MAIN backend full via tracked `Riftbound.slnx`: `7190/7190`.
- Mechanical checks passed: scoped whitespace verification, `git diff HEAD --check`, anchored conflict-marker scan, matrix JSON parse and stale/path typo scan.

## Residual Risk

This is recovery test coverage only. It does not close broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, `fullOfficial` or final readiness.
