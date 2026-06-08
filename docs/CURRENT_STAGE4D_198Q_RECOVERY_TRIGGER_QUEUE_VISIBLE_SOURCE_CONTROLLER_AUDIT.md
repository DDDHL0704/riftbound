# Stage 4D-198Q Recovery Trigger Queue Visible Source Controller Audit

Date: 2026-06-08

Owner: A_MAIN

## Scope

Stage 4D-198Q adds one server-test shard for spectator recovery replay timing trigger-queue visible source controller redaction validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceControllerRedactionWithoutCountMismatch`

The test builds an authoritative state with one visible trigger queue item controlled by `alice` and sourced by `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue length equal to the authoritative trigger queue length of one, verifies the visible trigger payload is canonical, then changes only `triggerQueue[0].controllerId` from `alice` to `HIDDEN` while leaving source, effect and triggered-event fields unchanged.

Assertions prove recovery validation emits:

- Controller id must-not-be-redacted diagnostic.
- Keyed authoritative controller-id mismatch diagnostic for `trigger-visible`.
- Aggregate controller-id disagreement diagnostic.
- No aggregate triggered-event-kind disagreement diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueVisibleSourceControllerRedactionWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1463/1463`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1468/1468`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7738/7738`.
- `git diff --check` passed.
- Conflict-marker scan over `docs`, `tests` and `src` passed.

Code commit:

- `ba9bc6fa test: cover trigger queue controller recovery replay`

Push:

- `git push origin main` succeeded after the code commit.

## Coordination

A_MAIN created no subagent and no new worktree. DOC_MATRIX_CURRENT was clean at HEAD `17bde0c3` when checked before the docs sync at `2026-06-08 22:33 CST`. No DOC_MATRIX handoff or unresolved question was pending.

Project remains **NOT READY**. This slice does not close broader P0/P1 runtime/server closure, command/recovery/random determinism, remaining recovery payload breadth, LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, or final readiness.
