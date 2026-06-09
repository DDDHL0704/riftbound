# Stage 4D-200D Recovery Trigger Queue Hidden Trigger Id Redaction Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200D adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue trigger-id redaction-sentinel validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggerIdRedactionSentinelWithCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes only `triggerQueue[0].triggerId` from `trigger-hidden` to redaction sentinel `HIDDEN`, clones the original spectator trigger as `trigger-extra`, and appends it so the spectator trigger queue count becomes two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Trigger-id must-not-be-redacted diagnostic.
- Unexpected `HIDDEN` diagnostic.
- Unexpected `trigger-extra` diagnostic.
- Missing authoritative `trigger-hidden` diagnostic.
- Trigger queue count mismatch diagnostic.

Assertions also prove this path does not emit:

- Required trigger-id diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggerIdRedactionSentinelWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~MatchRecoveryTests` -> `1502/1502`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~Recovery` -> `1507/1507`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj` -> `7777/7777`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `0f75e6cc`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 11:28 CST before docs sync.
- Project remains **NOT READY**.
