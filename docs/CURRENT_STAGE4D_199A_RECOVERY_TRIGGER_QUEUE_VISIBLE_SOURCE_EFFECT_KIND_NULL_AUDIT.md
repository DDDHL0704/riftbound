# Stage 4D-199A Recovery Trigger Queue Visible Source Effect Kind Null Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-199A adds one server-test shard for spectator recovery replay timing trigger-queue visible source effect-kind null-value validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindNullValueWithoutCountMismatch`

The test builds an authoritative state with one visible trigger queue item sourced by `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue length equal to the authoritative trigger queue length of one, verifies the visible trigger source object id, effect kind, and triggered event kind are canonical, then changes only `triggerQueue[0].effectKind` from `LAST_BREATH` to `null`.

Assertions prove recovery validation emits:

- Required effect-kind diagnostic.
- Keyed authoritative effect-kind mismatch diagnostic for `trigger-visible`.
- Aggregate effect-kind disagreement diagnostic.
- No aggregate source-visibility disagreement diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindNullValueWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1473/1473`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1478/1478`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7748/7748`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `5b8e7e9e`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before docs sync.
- Project remains **NOT READY**.
