# Stage 4D-198K Recovery Trigger Queue Trigger Id Duplicate Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-198K adds one server-test shard for spectator recovery replay timing trigger-queue duplicate trigger-id validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueDuplicateTriggerIdWithoutCountMismatch`

The test builds an authoritative state with two visible trigger queue items that share the same controller, source object, source visibility, effect kind and triggering event kind, but use distinct trigger ids: `trigger-visible-a` and `trigger-visible-b`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue length equal to the authoritative trigger queue length of two, verifies both visible trigger payloads remain canonical, then changes only the second payload's `triggerId` to `trigger-visible-a`.

Assertions prove recovery validation emits:

- Duplicate `trigger-visible-a` trigger-id diagnostic.
- Missing authoritative `trigger-visible-b` trigger-id diagnostic.
- Aggregate trigger-id disagreement diagnostic.
- No unexpected `trigger-visible-a` trigger-id diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueDuplicateTriggerIdWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1457/1457`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1462/1462`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7732/7732`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `c0f8b207 test: cover duplicate trigger id recovery replay`

Push:

- `git push origin main` succeeded after the code commit.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync at `2026-06-08 21:49 CST`. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
