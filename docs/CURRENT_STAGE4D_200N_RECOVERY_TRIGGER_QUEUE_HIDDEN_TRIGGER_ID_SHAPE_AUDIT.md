# Stage 4D-200N Recovery Trigger Queue Hidden Trigger Id Shape Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200N adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue trigger-id payload-shape validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggerIdShapeWithoutCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes only `triggerQueue[0].triggerId` from `trigger-hidden` to array-shaped payload `new[] { "trigger-hidden" }`, and keeps the spectator trigger queue count at one so it still matches authoritative count.

Assertions prove recovery validation emits:

- Required trigger-id diagnostic.
- Missing authoritative `trigger-hidden` diagnostic.
- Aggregate trigger-queue id disagreement diagnostic.

Assertions also prove this path does not emit:

- Trigger-id must-not-be-redacted diagnostic.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggerIdShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1512/1512`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1517/1517`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7787/7787`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `a9ce5fd4`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 13:09 CST before docs sync.
- Project remains **NOT READY**.
