# Stage 4D-203U Recovery Trigger Queue Trigger Id Canonicality Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203U adds one server-test shard for spectator recovery replay timing trigger queue keyed `triggerId` canonicality validation without a trigger queue count mismatch while aggregate trigger-id comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedTriggerIdCanonicalityWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only the spectator `triggerId` field from its canonical string to the same value with surrounding whitespace.

Assertions prove recovery validation emits:

- Trigger-id surrounding-whitespace diagnostics.
- Aggregate same-count trigger queue id disagreement diagnostics.

Assertions also prove this same-count trigger-id canonicality path does not emit:

- Missing authoritative trigger id diagnostics for `trigger-visible`.
- Unexpected trigger id diagnostics for `trigger-visible`.
- Trigger queue count mismatch diagnostics.
- Required trigger-id diagnostics.
- Trigger-id redaction diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedTriggerIdCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1597/1597`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1602/1602`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7872/7872`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `77f8a1c1`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 04:08 CST before docs sync.
- Project remains **NOT READY**.
