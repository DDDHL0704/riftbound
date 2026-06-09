# Stage 4D-200U Recovery Trigger Queue Hidden Canonicalized Duplicate Id Count Mismatch Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200U adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue trigger-id canonicality when trimming a spectator id creates a duplicate and the spectator trigger queue count also differs from authoritative state.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithMissingAuthoritativeAndCountMismatch`

The test builds an authoritative state with two hidden-source trigger queue items, `trigger-hidden-a` and `trigger-hidden-b`, both controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes the second spectator trigger id from `trigger-hidden-b` to whitespace-padded ` trigger-hidden-a `, appends an extra redacted trigger with `triggerId = trigger-extra`, and leaves authoritative state unchanged.

Assertions prove recovery validation emits:

- Surrounding-whitespace diagnostic for canonicalized `trigger-hidden-a`.
- Duplicate `trigger-hidden-a` diagnostic after trimming.
- Unexpected `trigger-extra` not-present diagnostic.
- Missing authoritative `trigger-hidden-b` diagnostic.
- Trigger queue count mismatch diagnostic for spectator count 3 versus authoritative count 2.

Assertions also prove this count-mismatch path does not emit:

- Unexpected `trigger-hidden-a` not-present diagnostic.
- Aggregate trigger-queue id disagreement diagnostic, because aggregate parity checks are skipped after count mismatch.
- Trigger-id must-not-be-redacted diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithMissingAuthoritativeAndCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1519/1519`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1524/1524`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7794/7794`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `eaa18e45`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 14:25 CST before docs sync.
- Project remains **NOT READY**.
