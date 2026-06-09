# Stage 4D-200V Recovery Trigger Queue Hidden Canonicalized Duplicate Id Keyed Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200V adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue trigger-id canonicality when a whitespace-padded duplicate id also carries keyed authoritative value drift and the spectator trigger queue count differs from authoritative state.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithKeyedValueMismatchAndCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item, `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, clones the spectator trigger, changes the clone `triggerId` to whitespace-padded ` trigger-hidden `, changes the clone `controllerId` to `bob`, changes the clone `triggeredByEventKind` to `OBJECT_DESTROYED`, appends the clone, and leaves authoritative state unchanged.

Assertions prove recovery validation emits:

- Surrounding-whitespace diagnostic for canonicalized `trigger-hidden`.
- Duplicate `trigger-hidden` diagnostic after trimming.
- Keyed authoritative controller-id mismatch for `trigger-hidden`.
- Keyed authoritative triggered-event-kind mismatch for `trigger-hidden`.
- Trigger queue count mismatch diagnostic for spectator count 2 versus authoritative count 1.

Assertions also prove this count-mismatch path does not emit:

- Unexpected `trigger-hidden` not-present diagnostic.
- Missing authoritative `trigger-hidden` diagnostic.
- Aggregate trigger-queue id disagreement diagnostic, because aggregate parity checks are skipped after count mismatch.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithKeyedValueMismatchAndCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1520/1520`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1525/1525`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7795/7795`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `b68dedab`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 14:37 CST before docs sync.
- Project remains **NOT READY**.
