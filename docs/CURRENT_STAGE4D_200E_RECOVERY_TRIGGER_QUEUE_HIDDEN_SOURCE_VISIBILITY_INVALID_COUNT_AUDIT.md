# Stage 4D-200E Recovery Trigger Queue Hidden Source Visibility Invalid Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200E adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue source-visibility invalid-value validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceVisibilityInvalidValueWithCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes only `triggerQueue[0].sourceVisibility` from `HIDDEN` to invalid value `UNKNOWN`, clones the original spectator trigger as `trigger-extra`, and appends it so the spectator trigger queue count becomes two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Source-visibility invalid-value diagnostic.
- Keyed authoritative source-visibility mismatch diagnostic.
- Unexpected `trigger-extra` diagnostic.
- Trigger queue count mismatch diagnostic.

Maintenance note:

- The existing hidden-source triggered-event-kind with-count invalid-value test method was renamed from `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindCanonicalityWithCountMismatch` to `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindInvalidValueWithCountMismatch` so the method name matches its `FORGED_EVENT` behavior. No runtime behavior changed.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceVisibilityInvalidValueWithCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1503/1503`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1508/1508`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7778/7778`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `7b6ba211`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 11:39 CST before docs sync.
- Project remains **NOT READY**.
