# Stage 4D-204B Recovery Trigger Queue Controller Redaction Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-204B adds one server-test shard for spectator recovery replay timing trigger queue keyed visible-source controller-id redaction validation without a trigger queue count mismatch while keyed authoritative value lookup remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceControllerRedactionWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible` and backed by visible source object `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only `controllerId` to `HIDDEN`.

Assertions prove recovery validation emits:

- Controller-id must-not-be-redacted diagnostics.
- Keyed authoritative controller-id mismatch diagnostics for `trigger-visible`.
- Aggregate same-count controller-id disagreement diagnostics.

Assertions also prove this same-count controller redaction path does not emit:

- Trigger queue count mismatch diagnostics.
- Triggered-event-kind redaction diagnostics.
- Triggered-event-kind aggregate disagreement diagnostics.
- Effect-kind aggregate disagreement diagnostics.
- Controller-id required diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceControllerRedactionWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1604/1604`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1609/1609`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7879/7879`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `a1b42964`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 05:14 CST before docs sync.
- Project remains **NOT READY**.
