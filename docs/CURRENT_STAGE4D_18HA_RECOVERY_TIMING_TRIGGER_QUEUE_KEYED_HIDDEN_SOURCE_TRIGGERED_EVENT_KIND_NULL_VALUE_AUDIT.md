# Stage 4D-18HA Recovery Timing Trigger Queue Keyed Hidden Source Triggered Event Kind Null Value Audit

Date: 2026-06-05 16:26 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindNullValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative hidden-source trigger queue item from real `MatchState` battlefield face-down standby object state, verifies that the spectator replay-frame timing payload emits non-redacted `controllerId = "alice"` alongside redacted `sourceObjectId = "HIDDEN"`, `sourceVisibility = "HIDDEN"` and `effectKind = "HIDDEN"`, preserves `triggeredByEventKind = "BATTLEFIELD_HELD"`, keeps the payload keyed to authoritative `trigger-hidden`, changes only `triggeredByEventKind` to `null`, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- triggered-event-kind required diagnostic for a null payload
- keyed authoritative triggered-event-kind mismatch for trigger id `trigger-hidden`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed hidden-source triggered-event-kind null-value test: `1/1`
- Focused `TriggerQueue` filter: `410/410`
- Focused `MatchRecoveryTests` filter: `1237/1237`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1818/1818`
- Backend full via tracked `Riftbound.slnx`: `7183/7183`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18HA stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface and included a focused-test assertion fix for the null-value diagnostic wording.

## Subagent Review

- Read-only gap review confirmed null-value is the next natural single-field hidden-source triggered-event-kind slice after 18GZ, with shape remaining as the next likely slice.
- Read-only diagnostics review confirmed the null-value required diagnostic, keyed authoritative mismatch wording, extra-trigger diagnostic, count mismatch diagnostic and expected validation count increments.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue hidden-source triggered-event-kind null-value parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
