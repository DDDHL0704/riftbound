# Stage 4D-18GU Recovery Timing Trigger Queue Keyed Hidden Source Controller Id Canonicality Audit

Date: 2026-06-05 15:20 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceControllerIdCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative hidden-source trigger queue item from real `MatchState` battlefield face-down standby object state, verifies that the spectator replay-frame timing payload emits non-redacted `controllerId = "alice"` alongside redacted `sourceObjectId = "HIDDEN"`, `sourceVisibility = "HIDDEN"` and `effectKind = "HIDDEN"`, keeps the payload keyed to authoritative `trigger-hidden`, wraps only `controllerId` in surrounding whitespace, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- controller-id scalar canonicality diagnostic for surrounding whitespace
- keyed authoritative controller-id mismatch for trigger id `trigger-hidden`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed hidden-source controller-id canonicality test: `1/1`
- Focused `TriggerQueue` filter: `404/404`
- Focused `MatchRecoveryTests` filter: `1231/1231`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1812/1812`
- Backend full via tracked `Riftbound.slnx`: `7177/7177`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GU stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue hidden-source controller-id parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
