# Stage 4D-203Y Recovery Trigger Queue Controller Canonicality Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203Y adds one server-test shard for spectator recovery replay timing trigger queue keyed visible-source controller-id canonicality validation without a trigger queue count mismatch while keyed authoritative value lookup remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceControllerIdCanonicalityWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible` and backed by visible source object `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only `controllerId` to `" alice "`.

Assertions prove recovery validation emits:

- Controller-id surrounding-whitespace diagnostics.
- Keyed authoritative controller-id mismatch diagnostics for `trigger-visible`.
- Aggregate same-count controller-id disagreement diagnostics.

Assertions also prove this same-count controller canonicality path does not emit:

- Trigger queue count mismatch diagnostics.
- Triggered-event-kind aggregate disagreement diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceControllerIdCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1601/1601`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1606/1606`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7876/7876`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `2086d1db`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 04:47 CST before docs sync.
- Project remains **NOT READY**.
