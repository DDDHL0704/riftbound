# Stage 4D-199Z Recovery Trigger Queue Trigger Id Null Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-199Z adds one server-test shard for spectator recovery replay timing trigger-queue trigger-id null-value validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedTriggerIdNullWithCountMismatch`

The test builds an authoritative state with one visible trigger queue item `trigger-visible`, controlled by `alice`, sourced by `visible-source-1`, with `sourceVisibility = VISIBLE`, `effectKind = LAST_BREATH`, and `triggeredByEventKind = OBJECT_DESTROYED`. It starts from the redacted spectator replay frame, changes only `triggerQueue[0].triggerId` from `trigger-visible` to `null`, clones the original spectator trigger as `trigger-extra`, and appends it so the spectator trigger queue count becomes two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Required trigger-id diagnostic.
- Unexpected `trigger-extra` diagnostic.
- Missing authoritative `trigger-visible` diagnostic.
- Trigger queue count mismatch diagnostic.

Assertions also prove this path does not emit:

- Trigger-id must-not-be-redacted diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedTriggerIdNullWithCountMismatch` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~MatchRecoveryTests` -> `1498/1498`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1503/1503`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7773/7773`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `07eb9ac7`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 10:46 CST before docs sync.
- Project remains **NOT READY**.
