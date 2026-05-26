# Stage 4D-17JA Recovery Spectator Pending Task Queue Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 09:21 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure or final readiness.

## Scope

Stage 4D-17JA continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IZ. This slice covers `Timing["pendingTaskQueue"]` queue, metadata and task item values before authoritative pending-task queue comparison consumes those fields.

Covered fields:

- `Timing["pendingTaskQueue"]["hasTasks"]`
- `Timing["pendingTaskQueue"]["isBlocking"]`
- `Timing["pendingTaskQueue"]["phase"]`
- `Timing["pendingTaskQueue"]["activeTaskId"]`
- `Timing["pendingTaskQueue"]["tasks"]`
- `Timing["pendingTaskQueue"]["metadata"]["taskCount"]`
- `Timing["pendingTaskQueue"]["metadata"]["stateBasedTaskKinds"]`
- `Timing["pendingTaskQueue"]["tasks"][]["taskId"]`
- `Timing["pendingTaskQueue"]["tasks"][]["kind"]`
- `Timing["pendingTaskQueue"]["tasks"][]["reason"]`
- `Timing["pendingTaskQueue"]["tasks"][]["playerId"]`
- `Timing["pendingTaskQueue"]["tasks"][]["battlefieldObjectId"]`
- `Timing["pendingTaskQueue"]["tasks"][]["objectId"]`
- `Timing["pendingTaskQueue"]["tasks"][]["hiddenObject"]`
- `Timing["pendingTaskQueue"]["tasks"][]["hiddenObjectKind"]`

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame pending task queue value shapes before pending-task queue comparison consumes the fields.

The pending task queue fields now check:

- required queue booleans reject missing, null and non-boolean values
- required queue/task strings reject missing, blank, surrounding-whitespace and non-string drift
- optional queue/task strings preserve absent/null/empty compatibility while rejecting malformed present values
- `metadata.taskCount` rejects missing, null, non-integer and negative values
- `metadata.stateBasedTaskKinds` rejects missing, null, non-list, blank, surrounding-whitespace and duplicate normalized values
- optional-present `hiddenObject` rejects non-boolean values

Malformed values now fail with explicit spectator recovery diagnostics instead of surfacing only through generic pending-task queue mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingPendingTaskQueueValueDrift`.

The test builds a spectator replay frame from an authoritative state with state-based cleanup tasks, mutates the pending task queue, metadata and task item payloads to include invalid booleans, null required booleans, whitespace-mutated strings, malformed optional strings, negative metadata counts, blank/duplicate state-based task kinds and malformed hidden-object fields, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `280/280`
- Adjacent recovery/opening/store-smoke: `861/861`
- Backend full: `6226/6226`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
