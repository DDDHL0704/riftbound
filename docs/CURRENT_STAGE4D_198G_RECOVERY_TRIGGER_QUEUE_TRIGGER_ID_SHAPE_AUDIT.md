# Stage 4D-198G Recovery Trigger Queue Trigger Id Shape Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-198G adds one server-test shard for spectator recovery replay timing trigger-queue trigger-id payload-shape validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdShapeWithoutCountMismatch`

The test builds an authoritative state with one visible trigger queue item whose trigger id is `trigger-visible`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue length equal to the authoritative trigger queue length of one, verifies the visible trigger fields remain canonical, then changes only `triggerQueue[0].triggerId` to an array-shaped payload.

Assertions prove recovery validation emits:

- Trigger-id required diagnostic.
- Missing authoritative trigger id `trigger-visible` diagnostic.
- Aggregate trigger-id disagreement diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTriggerIdShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1453/1453`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1458/1458`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7728/7728`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `8e9fdff9 test: cover trigger id payload shape`

Push:

- `git push origin main` succeeded after the code commit via SSH.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync at `2026-06-08 21:16 CST`. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
