# Stage 4D-18GO Recovery Timing Trigger Queue Keyed Visible Source Object Id Canonicality Audit

Date: 2026-06-05 14:26 CST

Status: accepted on A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectIdCanonicalityWithCountMismatch` in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

The test builds an authoritative visible-source trigger queue item from real `MatchState` base-zone object state, verifies that the spectator replay-frame timing payload emits the authoritative `sourceObjectId` value `visible-source-1`, keeps the payload keyed to authoritative `trigger-visible`, wraps only `sourceObjectId` in surrounding whitespace, then appends `trigger-extra` to force spectator trigger-count mismatch.

## Locked Behavior

The regression proves the existing recovery validator still emits all of these diagnostics before count mismatch can hide broad ordered parity:

- source-object-id scalar canonicality diagnostic for surrounding whitespace
- keyed authoritative source-object-id mismatch for trigger id `trigger-visible`
- unknown extra-trigger diagnostic for `trigger-extra`
- trigger-queue count mismatch `2` vs `1`

Runtime changed: no. This is server recovery validation coverage only.

Protocol shape changed: no.

Frontend, matrix, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final status changed: no.

## Validation

- Focused new keyed visible-source source-object-id canonicality test: `1/1`
- Focused `TriggerQueue` filter: `398/398`
- Focused `MatchRecoveryTests` filter: `1225/1225`
- Adjacent recovery/official-opening/Postgres recovery-store filter: `1806/1806`
- Backend full via tracked `Riftbound.slnx`: `7171/7171`
- Touched-file scoped whitespace format passed.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan, matrix JSON parse and Stage 4D-18GO stale/typo scan.

Backend full was rerun because this batch touched the `MatchRecoveryTests` surface.

## Remaining

This narrows P1-004 replay/recovery determinism test coverage for spectator timing trigger-queue visible-source source-object-id parity only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
