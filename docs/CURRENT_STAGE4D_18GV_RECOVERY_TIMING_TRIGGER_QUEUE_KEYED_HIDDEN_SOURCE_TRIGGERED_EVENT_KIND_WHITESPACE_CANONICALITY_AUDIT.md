# Stage 4D-18GV Recovery Timing Trigger Queue Keyed Hidden Source Triggered Event Kind Whitespace Canonicality Audit

Date: 2026-06-05 15:32 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindWhitespaceCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative hidden-source trigger queue item from real `MatchState` battlefield face-down standby object state, verifies that the spectator replay-frame timing payload emits non-redacted `controllerId = "alice"` alongside redacted `sourceObjectId = "HIDDEN"`, `sourceVisibility = "HIDDEN"` and `effectKind = "HIDDEN"`, keeps `triggeredByEventKind = "BATTLEFIELD_HELD"`, keeps the payload keyed to authoritative `trigger-hidden`, wraps only `triggeredByEventKind` in surrounding whitespace, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- triggered-event-kind scalar canonicality diagnostic for surrounding whitespace
- keyed authoritative triggered-event-kind mismatch for trigger id `trigger-hidden`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed hidden-source triggered-event-kind whitespace canonicality test: `1/1`
- Focused `TriggerQueue` filter: `405/405`
- Focused `MatchRecoveryTests` filter: `1232/1232`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1813/1813`
- Backend full via tracked `Riftbound.slnx`: `7178/7178`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GV stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue hidden-source triggered-event-kind parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
