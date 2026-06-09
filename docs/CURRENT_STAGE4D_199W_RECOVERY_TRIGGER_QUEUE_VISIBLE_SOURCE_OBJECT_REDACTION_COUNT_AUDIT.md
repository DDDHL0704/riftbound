# Stage 4D-199W Recovery Trigger Queue Visible Source Object Redaction Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-199W adds one server-test shard for spectator recovery replay timing trigger-queue visible-source source-object-id redaction-sentinel validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectRedactionWithCountMismatch`

The test builds an authoritative state with one visible trigger queue item sourced by `visible-source-1`, controlled by `alice`, `sourceVisibility = VISIBLE`, `effectKind = LAST_BREATH`, and `triggeredByEventKind = OBJECT_DESTROYED`. It starts from the redacted spectator replay frame, changes only `triggerQueue[0].sourceObjectId` from `visible-source-1` to `HIDDEN`, clones the original spectator trigger as `trigger-extra`, and appends it so the spectator trigger queue count becomes two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Visible source-object-id must-not-be-redacted diagnostic.
- Keyed authoritative source-object-id mismatch diagnostic for `trigger-visible`.
- Unexpected `trigger-extra` diagnostic.
- Trigger queue count mismatch diagnostic.

Assertions also prove this path does not emit:

- Visible effect-kind must-not-be-redacted diagnostic.
- Required source-object-id diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceObjectRedactionWithCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1495/1495`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1500/1500`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7770/7770`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `6307f8a4`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 10:16 CST before docs sync.
- Project remains **NOT READY**.
