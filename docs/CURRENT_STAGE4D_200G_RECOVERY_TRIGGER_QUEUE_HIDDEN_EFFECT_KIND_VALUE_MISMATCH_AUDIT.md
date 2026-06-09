# Stage 4D-200G Recovery Trigger Queue Hidden Effect Kind Value Mismatch Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200G adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue effect-kind value-mismatch validation while the spectator trigger queue count still matches authoritative count.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceEffectKindValueMismatchWithoutCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame and changes only `triggerQueue[0].effectKind` from `HIDDEN` to `WRONG_EFFECT`, leaving the spectator trigger queue count at one.

Assertions prove recovery validation emits:

- Hidden effect-kind must-be-redacted diagnostic.
- Keyed authoritative effect-kind mismatch diagnostic.
- Aggregate effect-kinds disagreement diagnostic.

Assertions also prove this path does not emit:

- Required effect-kind diagnostic.
- Effect-kind surrounding-whitespace diagnostic for `WRONG_EFFECT`.
- Aggregate source-object-id disagreement diagnostic.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceEffectKindValueMismatchWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1505/1505`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1510/1510`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7780/7780`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `205be244`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 11:59 CST before docs sync.
- Project remains **NOT READY**.
