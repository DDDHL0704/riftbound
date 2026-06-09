# Stage 4D-203N Recovery Trigger Queue Key Set Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203N adds one server-test shard for spectator recovery replay timing trigger queue key-set validation without a trigger queue count mismatch while aggregate trigger-id comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeySetWithoutCountMismatch`

The test builds two authoritative trigger queue items keyed as `trigger-visible` and `trigger-hidden`. It starts from the redacted spectator replay frame, removes the spectator `trigger-hidden` item, adds unexpected `trigger-extra-a`, and keeps the spectator trigger queue count equal to the authoritative count.

Assertions prove recovery validation emits:

- Unexpected trigger diagnostics for `trigger-extra-a`.
- Missing authoritative trigger diagnostics for `trigger-hidden`.
- Aggregate same-count trigger queue id disagreement diagnostics.

Assertions also prove this same-count trigger queue key-set path does not emit:

- Trigger queue count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeySetWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1590/1590`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1595/1595`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7865/7865`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `deccad40`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 03:06 CST before docs sync.
- Project remains **NOT READY**.
