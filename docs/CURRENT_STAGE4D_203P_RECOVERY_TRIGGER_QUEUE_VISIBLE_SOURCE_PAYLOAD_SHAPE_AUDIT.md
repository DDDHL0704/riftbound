# Stage 4D-203P Recovery Trigger Queue Visible Source Payload Shape Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-203P adds one server-test shard for spectator recovery replay timing trigger queue visible-source `sourceObjectId`, `sourceVisibility` and `effectKind` required-field payload-shape validation without a trigger queue count mismatch while aggregate source-object/source-visibility/effect-kind comparisons remain active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourcePayloadRequiredFieldShapeWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only the spectator `sourceObjectId`, `sourceVisibility` and `effectKind` fields to array-shaped payloads.

Assertions prove recovery validation emits:

- Required-field diagnostics for `sourceObjectId`, `sourceVisibility` and `effectKind`.
- Keyed authoritative source-object-id, source-visibility and effect-kind mismatch diagnostics for `trigger-visible`.
- Aggregate same-count trigger queue source-object-id, source-visibility and effect-kind disagreement diagnostics.

Assertions also prove this same-count visible-source payload-shape path does not emit:

- Trigger queue count mismatch diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourcePayloadRequiredFieldShapeWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1592/1592`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1597/1597`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7867/7867`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `1cc46ef9`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 03:24 CST before docs sync.
- Project remains **NOT READY**.
