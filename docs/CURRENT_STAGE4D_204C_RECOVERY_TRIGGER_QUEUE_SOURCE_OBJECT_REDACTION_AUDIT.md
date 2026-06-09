# Stage 4D-204C Recovery Trigger Queue Source Object Redaction Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-204C adds one server-test shard for spectator recovery replay timing trigger queue keyed visible-source source-object-id redaction validation without a trigger queue count mismatch while keyed authoritative value lookup remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectRedactionWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible` and backed by visible source object `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only `sourceObjectId` to `HIDDEN`.

Assertions prove recovery validation emits:

- Visible source-object-id must-not-be-redacted diagnostics.
- Keyed authoritative source-object-id mismatch diagnostics for `trigger-visible`.
- Aggregate same-count source-object-id disagreement diagnostics.

Assertions also prove this same-count source-object redaction path does not emit:

- Trigger queue count mismatch diagnostics.
- Visible effect-kind redaction diagnostics.
- Effect-kind aggregate disagreement diagnostics.
- Source-visibility aggregate disagreement diagnostics.
- Source-object-id required diagnostics.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectRedactionWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1605/1605`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1610/1610`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7880/7880`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `f6a5d7f7`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 05:24 CST before docs sync.
- Project remains **NOT READY**.
