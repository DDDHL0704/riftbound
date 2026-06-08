# Stage 4D-198T Recovery Trigger Queue Visible Source Object Shape Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-198T adds one server-test shard for spectator recovery replay timing trigger-queue visible source-object-id payload-shape validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectIdShapeWithoutCountMismatch`

The test builds an authoritative state with one visible trigger queue item sourced by `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue length equal to the authoritative trigger queue length of one, verifies the visible trigger source object id is canonical, then changes only `triggerQueue[0].sourceObjectId` from `visible-source-1` to an array-shaped payload.

Assertions prove recovery validation emits:

- Required source-object-id diagnostic.
- Keyed authoritative source-object-id mismatch diagnostic for `trigger-visible`.
- Aggregate source-object-id disagreement diagnostic.
- No aggregate source-visibility disagreement diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectIdShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1466/1466`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1471/1471`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7741/7741`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `7ab0a09c test: cover trigger queue source shape recovery replay`

Push:

- `git push origin main` succeeded after the code commit.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync at `2026-06-08 23:02 CST`. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
