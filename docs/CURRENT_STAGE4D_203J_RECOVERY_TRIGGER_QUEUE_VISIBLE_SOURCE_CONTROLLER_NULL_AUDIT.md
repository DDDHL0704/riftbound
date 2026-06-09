# Stage 4D-203J Recovery Trigger Queue Visible Source Controller Null Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203J adds one server-test shard for spectator recovery replay timing trigger queue visible-source `controllerId` null-value validation without a trigger queue count mismatch while aggregate controller comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceControllerIdNullValueWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only the spectator `controllerId` field to `null`.

Assertions prove recovery validation emits:

- Controller-id required diagnostics.
- Keyed authoritative controller-id mismatch diagnostics for `trigger-visible`.
- Aggregate same-count trigger queue controller-id disagreement diagnostics.

Assertions also prove this same-count trigger queue visible-source controller null-value path does not emit:

- Trigger queue count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceControllerIdNullValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1586/1586`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1591/1591`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7861/7861`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `6cb8f6c2`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 02:26 CST before docs sync.
- Project remains **NOT READY**.
