# Stage 4D-199C Recovery Trigger Queue Visible Source Effect Kind Value Mismatch Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-199C adds one server-test shard for spectator recovery replay timing trigger-queue visible source effect-kind value mismatch validation without a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindValueMismatchWithoutCountMismatch`

The test builds an authoritative state with one visible trigger queue item sourced by `visible-source-1`. It starts from the redacted spectator replay frame, keeps the spectator trigger queue length equal to the authoritative trigger queue length of one, verifies the visible trigger source object id, effect kind, and triggered event kind are canonical, then changes only `triggerQueue[0].effectKind` from `LAST_BREATH` to `WRONG_EFFECT`.

Assertions prove recovery validation emits:

- Keyed authoritative effect-kind mismatch diagnostic for `trigger-visible`.
- Aggregate effect-kind disagreement diagnostic.
- No required effect-kind diagnostic.
- No effect-kind surrounding-whitespace diagnostic.
- No aggregate source-visibility disagreement diagnostic.
- No trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindValueMismatchWithoutCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1475/1475`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1480/1480`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7750/7750`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `b22aa992`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` before docs sync.
- Project remains **NOT READY**.
