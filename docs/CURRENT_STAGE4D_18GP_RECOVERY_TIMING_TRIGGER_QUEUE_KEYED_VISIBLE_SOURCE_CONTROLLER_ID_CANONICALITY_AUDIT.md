# Stage 4D-18GP Recovery Timing Trigger Queue Keyed Visible Source Controller Id Canonicality Audit

Date: 2026-06-05 14:36 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceControllerIdCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative visible-source trigger queue item from real `MatchState` base-zone object state, verifies that the spectator replay-frame timing payload emits `controllerId = "alice"` and `triggeredByEventKind = "OBJECT_DESTROYED"`, keeps the payload keyed to authoritative `trigger-visible`, wraps only `controllerId` in surrounding whitespace, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- controller-id scalar canonicality diagnostic for surrounding whitespace
- keyed authoritative controller-id mismatch for trigger id `trigger-visible`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed visible-source controller-id canonicality test: `1/1`
- Focused `TriggerQueue` filter: `399/399`
- Focused `MatchRecoveryTests` filter: `1226/1226`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1807/1807`
- Backend full via tracked `Riftbound.slnx`: `7172/7172`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GP stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue visible-source controller-id parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
