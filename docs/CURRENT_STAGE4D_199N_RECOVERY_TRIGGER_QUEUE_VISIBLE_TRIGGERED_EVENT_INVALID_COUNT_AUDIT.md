# Stage 4D-199N Recovery Trigger Queue Visible Triggered Event Invalid Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-199N adds one server-test shard for spectator recovery replay timing trigger-queue visible-source triggered-event-kind invalid-value validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceTriggeredEventKindInvalidValueWithCountMismatch`

The test builds an authoritative state with one visible trigger queue item sourced by `visible-source-1` and triggered by `OBJECT_DESTROYED`. It starts from the redacted spectator replay frame, changes `triggerQueue[0].triggeredByEventKind` from `OBJECT_DESTROYED` to invalid value `FORGED_EVENT`, clones the original spectator trigger as `trigger-extra`, and appends it so the spectator trigger queue count becomes two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Invalid triggered-event-kind diagnostic for `FORGED_EVENT`.
- Keyed authoritative triggered-event-kind mismatch diagnostic for `trigger-visible`.
- Unexpected `trigger-extra` diagnostic.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceTriggeredEventKindInvalidValueWithCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1486/1486`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1491/1491`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7761/7761`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `6f07dca1`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before docs sync.
- Project remains **NOT READY**.
