# Stage 4D-17TO Recovery Timing Pending Task Queue Keyed Value Audit

Date: 2026-06-02

Owner: A_MAIN

Status: accepted. Project remains **NOT READY**.

## Scope

Stage 4D-17TO tightens spectator replay-frame recovery validation for `timing.pendingTaskQueue.tasks[]` under pending-task count mismatch. Stage 4D-17TN already made spectator-visible pending task ids a key set; this slice validates same-key task values before the existing broad parity gate can be skipped.

Runtime changed:

- `src/Riftbound.Engine/MatchRecovery.cs`

Tests changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Notes

`MatchRecoveryValidator` now builds an authoritative pending-task index keyed by spectator-visible task id and validates matching spectator task payloads against authoritative `PendingTaskQueue.Tasks` values before the `validateAuthoritativeParity` early return. The new keyed validation covers:

- `kind`
- `reason`
- optional `playerId`
- optional `battlefieldObjectId`
- visible `objectId`
- hidden object redaction shape: missing `objectId`, `hiddenObject=true`, and `hiddenObjectKind=BATTLEFIELD_STANDBY`

The existing broad list parity still runs when spectator and authoritative task counts match. The new keyed path preserves missing/extra task-id diagnostics from 17TN and adds same-key value diagnostics when task counts differ.

## Coverage

New coverage:

- `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueTaskKeyedValuesWithTaskCountMismatch`

The test uses a spectator replay-frame pending-task queue containing the existing hidden standby and unattached equipment cleanup tasks, mutates the visible equipment task values while keeping its task id stable, and appends a forged extra task to force task-count mismatch. It asserts keyed diagnostics for kind, reason, player id, battlefield object id, object id, hidden object flag, hidden object kind, forged extra task id and task-count mismatch.

Validation passed:

- focused pending-task-queue keyed-value test `1/1`
- focused PendingTaskQueue filter `34/34`
- focused recovery `643/643`
- adjacent recovery/opening/store-smoke filter `1223/1223`
- backend full `6588/6588`
- `git diff --check`
- anchored conflict-marker scan over `docs`, `src` and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Locks

Protocol shape, frontend, matrix JSON, official catalog, `PaymentEngineCoverageAuditTests`, browser/Chrome/formal E2E, `fullOfficial`, final readiness status and `riftbound-dotnet.sln` remain locked.

This slice narrows P1-004 replay/recovery determinism only. Project remains **NOT READY**.
