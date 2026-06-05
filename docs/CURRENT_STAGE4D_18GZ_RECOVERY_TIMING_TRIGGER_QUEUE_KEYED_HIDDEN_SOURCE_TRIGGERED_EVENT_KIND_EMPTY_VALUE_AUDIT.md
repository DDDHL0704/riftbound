# Stage 4D-18GZ Recovery Timing Trigger Queue Keyed Hidden Source Triggered Event Kind Empty Value Audit

Date: 2026-06-05 16:15 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindEmptyValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative hidden-source trigger queue item from real `MatchState` battlefield face-down standby object state, verifies that the spectator replay-frame timing payload emits non-redacted `controllerId = "alice"` alongside redacted `sourceObjectId = "HIDDEN"`, `sourceVisibility = "HIDDEN"` and `effectKind = "HIDDEN"`, preserves `triggeredByEventKind = "BATTLEFIELD_HELD"`, keeps the payload keyed to authoritative `trigger-hidden`, changes only `triggeredByEventKind` to `string.Empty`, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- triggered-event-kind required diagnostic for an empty string payload
- keyed authoritative triggered-event-kind mismatch for trigger id `trigger-hidden`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed hidden-source triggered-event-kind empty-value test: `1/1`
- Focused `TriggerQueue` filter: `409/409`
- Focused `MatchRecoveryTests` filter: `1236/1236`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1817/1817`
- Backend full via tracked `Riftbound.slnx`: `7182/7182`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GZ stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Subagent Review

- Read-only test review reported no required fixes, no duplicate coverage, and confirmed the mutation and expected diagnostics match neighboring trigger-queue tests.
- Read-only docs review confirmed the 18GZ documentation scope, expected validation counts, and clean DOC_MATRIX_CURRENT state at `17bde0c3`.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue hidden-source triggered-event-kind empty-value parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
