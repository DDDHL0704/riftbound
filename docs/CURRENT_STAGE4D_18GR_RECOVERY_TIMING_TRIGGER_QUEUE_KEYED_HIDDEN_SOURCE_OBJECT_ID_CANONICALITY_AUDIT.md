# Stage 4D-18GR Recovery Timing Trigger Queue Keyed Hidden Source Object Id Canonicality Audit

Date: 2026-06-05 14:53 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceObjectIdCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative hidden-source trigger queue item from real `MatchState` battlefield face-down standby object state, verifies that the spectator replay-frame timing payload emits `sourceObjectId = "HIDDEN"`, `sourceVisibility = "HIDDEN"` and `effectKind = "HIDDEN"`, keeps the payload keyed to authoritative `trigger-hidden`, wraps only `sourceObjectId` in surrounding whitespace, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- source-object-id scalar canonicality diagnostic for surrounding whitespace
- keyed authoritative source-object-id mismatch for trigger id `trigger-hidden`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed hidden-source source-object-id canonicality test: `1/1`
- Focused `TriggerQueue` filter: `401/401`
- Focused `MatchRecoveryTests` filter: `1228/1228`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1809/1809`
- Backend full via tracked `Riftbound.slnx`: `7174/7174`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GR stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue hidden-source source-object-id parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
