# Stage 4D-203S Recovery Trigger Queue Trigger Id Null Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203S adds one server-test shard for spectator recovery replay timing trigger queue keyed `triggerId` null-value validation without a trigger queue count mismatch while aggregate trigger-id comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedTriggerIdNullWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only the spectator `triggerId` field from its canonical string to `null`.

Assertions prove recovery validation emits:

- Required trigger-id diagnostics.
- Missing authoritative trigger id `trigger-visible` diagnostics.
- Aggregate same-count trigger queue id disagreement diagnostics.

Assertions also prove this same-count trigger-id null path does not emit:

- Trigger queue count mismatch diagnostics.
- Trigger-id redaction diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedTriggerIdNullWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1595/1595`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1600/1600`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7870/7870`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `f1f78488`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 03:50 CST before docs sync.
- Project remains **NOT READY**.
