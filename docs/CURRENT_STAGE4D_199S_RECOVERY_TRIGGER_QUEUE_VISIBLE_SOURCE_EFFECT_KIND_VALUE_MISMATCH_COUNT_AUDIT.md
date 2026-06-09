# Stage 4D-199S Recovery Trigger Queue Visible Source Effect Kind Value Mismatch Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-199S adds one server-test shard for spectator recovery replay timing trigger-queue visible-source effect-kind value-mismatch validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindValueMismatchWithCountMismatch`

The test builds an authoritative state with one visible trigger queue item sourced by `visible-source-1`, `effectKind = LAST_BREATH`, and `triggeredByEventKind = OBJECT_DESTROYED`. It starts from the redacted spectator replay frame, changes `triggerQueue[0].effectKind` from `LAST_BREATH` to `WRONG_EFFECT`, clones the original spectator trigger as `trigger-extra`, and appends it so the spectator trigger queue count becomes two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Keyed authoritative effect-kind mismatch diagnostic for `trigger-visible`.
- Unexpected `trigger-extra` diagnostic.
- Trigger queue count mismatch diagnostic.

Assertions also prove this path does not emit:

- Required effect-kind diagnostic.
- Surrounding-whitespace diagnostic for `WRONG_EFFECT`.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindValueMismatchWithCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1491/1491`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1496/1496`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7766/7766`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `a34d1116`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 09:39 CST before docs sync.
- Project remains **NOT READY**.
