# Stage 4D-204A Recovery Trigger Queue Triggered Event Redaction Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-204A adds one server-test shard for spectator recovery replay timing trigger queue keyed visible-source triggered-event-kind redaction validation without a trigger queue count mismatch while keyed authoritative value lookup remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceTriggeredEventKindRedactionWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible` and backed by visible source object `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only `triggeredByEventKind` to `HIDDEN`.

Assertions prove recovery validation emits:

- Triggered-event-kind must-not-be-redacted diagnostics.
- Keyed authoritative triggered-event-kind mismatch diagnostics for `trigger-visible`.
- Aggregate same-count triggered-event-kind disagreement diagnostics.

Assertions also prove this same-count triggered-event redaction path does not emit:

- Trigger queue count mismatch diagnostics.
- Controller-id redaction diagnostics.
- Controller-id aggregate disagreement diagnostics.
- Effect-kind aggregate disagreement diagnostics.
- Triggered-event-kind required diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceTriggeredEventKindRedactionWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1603/1603`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1608/1608`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7878/7878`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `5eb55998`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 05:05 CST before docs sync.
- Project remains **NOT READY**.
