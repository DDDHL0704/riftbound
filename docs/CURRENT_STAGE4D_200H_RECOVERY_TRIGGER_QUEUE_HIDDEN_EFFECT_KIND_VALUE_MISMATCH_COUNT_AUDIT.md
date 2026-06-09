# Stage 4D-200H Recovery Trigger Queue Hidden Effect Kind Value Mismatch Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200H adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue effect-kind value-mismatch validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceEffectKindValueMismatchWithCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes `triggerQueue[0].effectKind` from `HIDDEN` to `WRONG_EFFECT`, and appends `trigger-extra` so the spectator trigger queue count no longer matches authoritative count.

Assertions prove recovery validation emits:

- Hidden effect-kind must-be-redacted diagnostic.
- Keyed authoritative effect-kind mismatch diagnostic.
- Unexpected `trigger-extra` diagnostic.
- Trigger queue count mismatch diagnostic.

Assertions also prove this path does not emit:

- Required effect-kind diagnostic.
- Effect-kind surrounding-whitespace diagnostic for `WRONG_EFFECT`.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceEffectKindValueMismatchWithCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1506/1506`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1511/1511`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7781/7781`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `262823d3`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 12:09 CST before docs sync.
- Project remains **NOT READY**.
