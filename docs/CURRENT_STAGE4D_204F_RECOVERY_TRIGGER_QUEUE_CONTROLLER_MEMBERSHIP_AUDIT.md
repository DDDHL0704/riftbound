# Stage 4D-204F Recovery Trigger Queue Controller Membership Audit

Date: 2026-06-10 05:52 CST

Status: accepted. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated a single-agent server-test slice for recovery spectator replay timing trigger queue keyed controller membership validation without a trigger queue count mismatch.

Runtime changed: no. This is server test coverage only.

## Code Change

Touched file:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

Added coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedControllerMembershipWithoutCountMismatch`

The test starts with one visible spectator trigger keyed to authoritative `trigger-visible`, changes only `controllerId` from `alice` to `charlie`, keeps spectator and authoritative trigger queue counts equal, and validates that recovery rejects the frame through missing-seat controller diagnostics, keyed authoritative controller-id mismatch diagnostics and aggregate same-count controller-id disagreement diagnostics.

The test also proves the failure is not coming from trigger queue count mismatch, trigger-id aggregate drift, required controller-id validation, source-object aggregate drift or triggered-event aggregate drift.

## Validation

- Focused test: `1/1`
- Changed class filter: `1608/1608`
- Adjacent recovery filter: `1613/1613`
- Backend full solution: `7883/7883`
- `git diff --check`: passed
- Conflict-marker scan over `docs`, `tests`, `src`: passed

## Commits

- Code commit: `be321505` (`test: cover trigger queue controller membership recovery replay`)
- Push after code commit: succeeded

## Remaining Work

This narrows recovery spectator replay timing trigger queue keyed controller membership validation without count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
