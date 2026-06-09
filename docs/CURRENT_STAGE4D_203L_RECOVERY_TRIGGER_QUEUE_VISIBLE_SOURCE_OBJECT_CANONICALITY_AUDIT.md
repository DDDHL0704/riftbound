# Stage 4D-203L Recovery Trigger Queue Visible Source Object Canonicality Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203L adds one server-test shard for spectator recovery replay timing trigger queue visible-source `sourceObjectId` canonicality validation without a trigger queue count mismatch while aggregate source-object comparison remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectIdCanonicalityWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only the spectator `sourceObjectId` field to its canonical value with surrounding whitespace.

Assertions prove recovery validation emits:

- Source-object-id surrounding-whitespace diagnostics.
- Keyed authoritative source-object-id mismatch diagnostics for `trigger-visible`.
- Aggregate same-count trigger queue source-object-id disagreement diagnostics.

Assertions also prove this same-count trigger queue visible-source source-object canonicality path does not emit:

- Source-visibility aggregate disagreement diagnostics.
- Trigger queue count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectIdCanonicalityWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1588/1588`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1593/1593`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7863/7863`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `6236bd3a`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 02:44 CST before docs sync.
- Project remains **NOT READY**.
