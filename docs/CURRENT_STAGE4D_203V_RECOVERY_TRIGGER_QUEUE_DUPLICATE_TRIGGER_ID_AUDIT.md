# Stage 4D-203V Recovery Trigger Queue Duplicate Trigger Id Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203V adds one server-test shard for spectator recovery replay timing trigger queue keyed duplicate `triggerId` validation without a trigger queue count mismatch while keyed authoritative value lookup remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedDuplicateIdWithoutCountMismatch`

The test builds two authoritative visible trigger queue items keyed as `trigger-visible-a` and `trigger-visible-b`, backed by different visible source objects. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only the second spectator trigger `triggerId` from `trigger-visible-b` to duplicate `trigger-visible-a`.

Assertions prove recovery validation emits:

- Duplicate trigger-id diagnostics for `trigger-visible-a`.
- Missing authoritative trigger-id diagnostics for `trigger-visible-b`.
- Aggregate same-count trigger queue id disagreement diagnostics.
- Keyed authoritative source-object-id mismatch diagnostics for duplicated `trigger-visible-a`.

Assertions also prove this same-count duplicate-trigger-id path does not emit:

- Unexpected trigger-id diagnostics for `trigger-visible-a`.
- Trigger queue count mismatch diagnostics.
- Required trigger-id diagnostics.
- Trigger-id redaction diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedDuplicateIdWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1598/1598`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1603/1603`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7873/7873`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `63c7be02`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 04:18 CST before docs sync.
- Project remains **NOT READY**.
