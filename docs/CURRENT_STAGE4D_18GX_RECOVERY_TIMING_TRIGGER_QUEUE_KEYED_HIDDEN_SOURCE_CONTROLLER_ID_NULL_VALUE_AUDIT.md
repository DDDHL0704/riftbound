# Stage 4D-18GX Recovery Timing Trigger Queue Keyed Hidden Source Controller Id Null Value Audit

Date: 2026-06-05 15:54 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceControllerIdNullValueWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative hidden-source trigger queue item from real `MatchState` battlefield face-down standby object state, verifies that the spectator replay-frame timing payload emits non-redacted `controllerId = "alice"` alongside redacted `sourceObjectId = "HIDDEN"`, `sourceVisibility = "HIDDEN"` and `effectKind = "HIDDEN"`, preserves `triggeredByEventKind = "BATTLEFIELD_HELD"`, keeps the payload keyed to authoritative `trigger-hidden`, changes only `controllerId` to `null`, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- controller-id required diagnostic for a null scalar value
- keyed authoritative controller-id mismatch for trigger id `trigger-hidden`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed hidden-source controller-id null-value test: `1/1`
- Focused `TriggerQueue` filter: `407/407`
- Focused `MatchRecoveryTests` filter: `1234/1234`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1815/1815`
- Backend full via tracked `Riftbound.slnx`: `7180/7180`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GX stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface and included a focused-test assertion fix for the null-value diagnostic wording.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue hidden-source controller-id null-value parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
