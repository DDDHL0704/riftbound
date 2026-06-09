# Stage 4D-200I Recovery Trigger Queue Hidden Triggered Event Kind Value Mismatch Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200I adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue triggered-event-kind value-mismatch validation while the spectator trigger queue count still matches authoritative count.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindValueMismatchWithoutCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame and changes only `triggerQueue[0].triggeredByEventKind` from `BATTLEFIELD_HELD` to known different event kind `OBJECT_DESTROYED`, leaving the spectator trigger queue count at one.

Assertions prove recovery validation emits:

- Keyed authoritative triggered-event-kind mismatch diagnostic.
- Aggregate triggered-event-kinds disagreement diagnostic.

Assertions also prove this path does not emit:

- Invalid-value diagnostic for `OBJECT_DESTROYED`.
- Required triggered-event-kind diagnostic.
- Triggered-event-kind surrounding-whitespace diagnostic for `OBJECT_DESTROYED`.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindValueMismatchWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1507/1507`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1512/1512`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7782/7782`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `201ac27c`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 12:19 CST before docs sync.
- Project remains **NOT READY**.
