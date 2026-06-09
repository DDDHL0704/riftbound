# Stage 4D-200R Recovery Trigger Queue Hidden Duplicate Id Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200R adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue duplicate trigger-id validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithoutCountMismatch`

The test builds an authoritative state with two hidden-source trigger queue items, `trigger-hidden-a` and `trigger-hidden-b`, both controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes only the second spectator trigger id from `trigger-hidden-b` to duplicate `trigger-hidden-a`, and keeps the spectator trigger queue count at two so it still matches authoritative count.

Assertions prove recovery validation emits:

- Duplicate `trigger-hidden-a` diagnostic.
- Missing authoritative `trigger-hidden-b` diagnostic.
- Aggregate trigger-queue id disagreement diagnostic.

Assertions also prove this path does not emit:

- Unexpected `trigger-hidden-a` not-present diagnostic.
- Trigger-id must-not-be-redacted diagnostic.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceDuplicateIdWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1516/1516`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1521/1521`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7791/7791`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `25bde8b7`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 13:47 CST before docs sync.
- Project remains **NOT READY**.
