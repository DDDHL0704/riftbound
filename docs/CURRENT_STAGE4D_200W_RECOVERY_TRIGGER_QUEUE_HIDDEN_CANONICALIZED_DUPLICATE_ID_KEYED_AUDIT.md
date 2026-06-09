# Stage 4D-200W Recovery Trigger Queue Hidden Canonicalized Duplicate Id Keyed Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200W adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue trigger-id canonicality when trimming a spectator id creates a duplicate, that duplicate also carries keyed authoritative controller/event drift, and the spectator trigger queue count still matches authoritative state.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithKeyedValueMismatchWithoutCountMismatch`

The test builds an authoritative state with two hidden-source trigger queue items, `trigger-hidden-a` and `trigger-hidden-b`, both controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes the second spectator trigger id from `trigger-hidden-b` to whitespace-padded ` trigger-hidden-a `, changes that second spectator trigger `controllerId` to `bob`, changes `triggeredByEventKind` to `OBJECT_DESTROYED`, and leaves the spectator trigger queue count equal to authoritative count.

Assertions prove recovery validation emits:

- Surrounding-whitespace diagnostic for canonicalized `trigger-hidden-a`.
- Duplicate `trigger-hidden-a` diagnostic after trimming.
- Missing authoritative `trigger-hidden-b` diagnostic.
- Keyed authoritative controller-id mismatch for normalized trigger id `trigger-hidden-a`.
- Keyed authoritative triggered-event-kind mismatch for normalized trigger id `trigger-hidden-a`.
- Aggregate trigger-queue id disagreement diagnostic.
- Aggregate controller-id disagreement diagnostic.
- Aggregate triggered-event-kind disagreement diagnostic.

Assertions also prove this same-count path does not emit:

- Unexpected `trigger-hidden-a` not-present diagnostic.
- Trigger-id must-not-be-redacted diagnostic.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceCanonicalizedDuplicateIdWithKeyedValueMismatchWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1521/1521`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1526/1526`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7796/7796`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `1b86e80f`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 14:46 CST before docs sync.
- Project remains **NOT READY**.
