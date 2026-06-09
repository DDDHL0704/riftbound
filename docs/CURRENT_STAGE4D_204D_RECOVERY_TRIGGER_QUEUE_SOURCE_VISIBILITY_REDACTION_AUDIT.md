# Stage 4D-204D Recovery Trigger Queue Source Visibility Redaction Audit

Date: 2026-06-10

Owner: A_MAIN

## Scope

Stage 4D-204D adds one server-test shard for spectator recovery replay timing trigger queue keyed visible-source source-visibility redaction validation without a trigger queue count mismatch while keyed authoritative value lookup remains active.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceVisibilityRedactionWithoutCountMismatch`

The test builds one authoritative visible trigger queue item keyed as `trigger-visible` and backed by visible source object `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue count equal to authoritative count, and changes only `sourceVisibility` to `HIDDEN`.

Assertions prove recovery validation emits:

- Hidden-source source-object-id must-be-redacted diagnostics.
- Hidden-source effect-kind must-be-redacted diagnostics.
- Keyed authoritative source-visibility mismatch diagnostics for `trigger-visible`.
- Aggregate same-count source-visibility disagreement diagnostics.

Assertions also prove this same-count source-visibility redaction path does not emit:

- Trigger queue count mismatch diagnostics.
- Source-object-id aggregate disagreement diagnostics.
- Effect-kind aggregate disagreement diagnostics.
- Source-visibility required diagnostics.
- Source-visibility invalid-value diagnostics for `HIDDEN`.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceVisibilityRedactionWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1606/1606`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1611/1611`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7881/7881`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `44a28f10`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-10 05:33 CST before docs sync.
- Project remains **NOT READY**.
