# Stage 4D-204E Recovery Trigger Queue Source Membership Audit

Date: 2026-06-10 05:43 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated a single-agent server-test slice for recovery spectator replay timing trigger queue keyed visible-source object membership validation without a trigger queue count mismatch.

Runtime changed: no. This is server test coverage only.

## Code Change

Touched file:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

Added coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectMembershipWithoutCountMismatch`

The test starts with one visible spectator trigger keyed to authoritative `trigger-visible`, changes only `sourceObjectId` from `visible-source-1` to `missing-source`, keeps spectator and authoritative trigger queue counts equal, and validates that recovery rejects the frame through visible source-object missing-registry diagnostics, keyed authoritative source-object-id mismatch diagnostics and aggregate same-count source-object-id disagreement diagnostics.

The test also proves the failure is not coming from trigger queue count mismatch, trigger-id aggregate drift, required source-object-id validation, source-visibility aggregate drift or effect-kind aggregate drift.

## Validation

- Focused test: `1/1`
- Changed class filter: `1607/1607`
- Adjacent recovery filter: `1612/1612`
- Backend full solution: `7882/7882`
- `git diff --check`: passed
- Conflict-marker scan over `docs`, `tests`, `src`: passed

## Commits

- Code commit: `6ca1dbdd` (`test: cover trigger queue source membership recovery replay`)
- Push after code commit: succeeded

## Remaining Work

This narrows recovery spectator replay timing trigger queue keyed visible-source object membership validation without count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
