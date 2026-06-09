# Stage 4D-203H Recovery Trigger Queue Payload Shape Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203H adds one server-test shard for spectator recovery replay timing trigger queue item payload-shape validation without a trigger queue count mismatch while keyed authoritative trigger-id-required validation remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueuePayloadShapeWithoutCountMismatch`

The test builds one authoritative trigger queue item keyed as `trigger-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and replaces only the single spectator trigger queue item with the invalid non-object payload `not-a-trigger-payload`.

Assertions prove recovery validation emits:

- Trigger queue item payload-required diagnostics.
- Keyed authoritative trigger-id-required diagnostics for `trigger-1`.

Assertions also prove this same-count trigger queue item payload-shape path does not emit:

- Trigger queue count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueuePayloadShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1584/1584`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1589/1589`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7859/7859`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `aab9c4c8`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 02:08 CST before docs sync.
- Project remains **NOT READY**.
