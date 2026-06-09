# Stage 4D-199R Recovery Trigger Queue Visible Source Effect Kind Shape Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-199R adds one server-test shard for spectator recovery replay timing trigger-queue visible-source effect-kind payload-shape validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindShapeWithCountMismatch`

The test builds an authoritative state with one visible trigger queue item sourced by `visible-source-1` and effect kind `LAST_BREATH`. It starts from the redacted spectator replay frame, changes `triggerQueue[0].effectKind` from `LAST_BREATH` to an array-shaped payload, clones the original spectator trigger as `trigger-extra`, and appends it so the spectator trigger queue count becomes two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Required effect-kind diagnostic for the array-shaped payload.
- Keyed authoritative effect-kind mismatch diagnostic for `trigger-visible`.
- Unexpected `trigger-extra` diagnostic.
- Trigger queue count mismatch diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceEffectKindShapeWithCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1490/1490`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1495/1495`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7765/7765`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `ae36c6b5`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 09:27 CST before docs sync.
- Project remains **NOT READY**.
