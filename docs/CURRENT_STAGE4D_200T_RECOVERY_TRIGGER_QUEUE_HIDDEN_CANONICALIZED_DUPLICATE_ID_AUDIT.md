# Stage 4D-200T Recovery Trigger Queue Hidden Canonicalized Duplicate Id Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200T adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue trigger-id canonicality when trimming a spectator id creates a duplicate while the spectator trigger queue count still matches authoritative state.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithoutCountMismatch`

The test builds an authoritative state with two hidden-source trigger queue items, `trigger-hidden-a` and `trigger-hidden-b`, both controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes the second spectator trigger id from `trigger-hidden-b` to whitespace-padded ` trigger-hidden-a `, and leaves the spectator trigger queue count equal to the authoritative count.

Assertions prove recovery validation emits:

- Surrounding-whitespace diagnostic for canonicalized `trigger-hidden-a`.
- Duplicate `trigger-hidden-a` diagnostic after trimming.
- Missing authoritative `trigger-hidden-b` diagnostic.
- Aggregate trigger-queue id disagreement diagnostic because the counts still match.

Assertions also prove this same-count path does not emit:

- Unexpected `trigger-hidden-a` not-present diagnostic.
- Trigger-id must-not-be-redacted diagnostic.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1518/1518`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1523/1523`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7793/7793`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `8caf81dc`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 14:16 CST before docs sync.
- Project remains **NOT READY**.
