# Stage 4D-200F Recovery Trigger Queue Hidden Source Visibility Invalid Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200F adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue source-visibility invalid-value validation while the spectator trigger queue count still matches authoritative count.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceVisibilityInvalidValueWithoutCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame and changes only `triggerQueue[0].sourceVisibility` from `HIDDEN` to invalid value `UNKNOWN`, leaving the spectator trigger queue count at one.

Assertions prove recovery validation emits:

- Source-visibility invalid-value diagnostic.
- Keyed authoritative source-visibility mismatch diagnostic.
- Aggregate source-visibilities disagreement diagnostic.

Assertions also prove this path does not emit:

- Required source-visibility diagnostic.
- Source-visibility surrounding-whitespace diagnostic for `UNKNOWN`.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceVisibilityInvalidValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1504/1504`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1509/1509`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7779/7779`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `ac332f45`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 11:48 CST before docs sync.
- Project remains **NOT READY**.
