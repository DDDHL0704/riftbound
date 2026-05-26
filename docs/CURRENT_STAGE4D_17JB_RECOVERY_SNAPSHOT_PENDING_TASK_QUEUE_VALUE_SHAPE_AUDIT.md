# Stage 4D-17JB Recovery Snapshot Pending Task Queue Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 09:44 CST. Project remains **NOT READY**.

## Scope

This slice covers recovered player-view snapshot validation only. It does not change protocol shape, frontend behavior, matrix JSON, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, or final readiness status.

`MatchRecoveryValidator.ValidateSnapshotShape` now validates present `Timing["pendingTaskQueue"]` payload values before pending-task queue field reads and parity checks consume that payload.

Covered queue fields:

- `hasTasks`
- `isBlocking`
- `phase`
- `activeTaskId`
- `tasks`

Covered metadata fields:

- `taskCount`
- `stateBasedTaskKinds`

Covered task item fields:

- `taskId`
- `kind`
- `reason`
- `playerId`
- `battlefieldObjectId`
- `objectId`
- `hiddenObject`
- `hiddenObjectKind`

## Runtime Change

`src/Riftbound.Engine/MatchRecovery.cs` now calls `ValidateSnapshotTimingPendingTaskQueuePayloadValues` from snapshot shape validation. The snapshot-specific helper reuses shared pending-task queue value helpers with recovered-snapshot labels, while the existing spectator helper now delegates to the same shared validation path.

The validation rejects malformed recovered snapshot pending task queue value drift:

- required booleans must be present and boolean;
- required strings must be present, non-blank, string typed and free of surrounding whitespace;
- optional-present strings must be string typed, non-blank and free of surrounding whitespace when provided;
- `tasks` must be an object list when present for item validation;
- `metadata.taskCount` must be a non-negative integer;
- `metadata.stateBasedTaskKinds` must be a string list without blank, surrounding-whitespace or duplicate normalized entries;
- optional-present `hiddenObject` must be boolean.

## Test Coverage

`tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs` adds `RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueValueDrift`.

The test mutates recovered player-view snapshot `Timing["pendingTaskQueue"]` with invalid queue scalars, metadata values and task item fields, then asserts explicit recovered snapshot diagnostics for malformed booleans, missing required booleans, whitespace strings, malformed optional strings, negative task counts, malformed state-based task-kind lists, malformed task item scalars and malformed hidden-object fields.

## Validation

- Focused single: `RecoveryValidatorRejectsSnapshotTimingPendingTaskQueueValueDrift` passed `1/1`.
- Focused recovery: `MatchRecoveryTests` passed `281/281`.
- Adjacent recovery/opening/store-smoke filter passed `862/862`.
- Backend full: `dotnet test Riftbound.slnx --no-restore` passed `6227/6227`.
- Mechanical checks passed: `git diff --check`, anchored conflict-marker scan over docs/src/tests, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Risk

This narrows P1-004 replay/recovery determinism for recovered snapshot pending task queue value shape only. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
