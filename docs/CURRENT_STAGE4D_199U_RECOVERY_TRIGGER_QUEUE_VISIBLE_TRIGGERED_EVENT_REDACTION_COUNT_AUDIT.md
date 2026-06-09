# Stage 4D-199U Recovery Trigger Queue Visible Triggered Event Redaction Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-199U adds one server-test shard for spectator recovery replay timing trigger-queue visible-source triggered-event-kind redaction-sentinel validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceTriggeredEventKindRedactionWithCountMismatch`

The test builds an authoritative state with one visible trigger queue item sourced by `visible-source-1`, `sourceVisibility = VISIBLE`, `effectKind = LAST_BREATH`, and `triggeredByEventKind = OBJECT_DESTROYED`. It starts from the redacted spectator replay frame, changes only `triggerQueue[0].triggeredByEventKind` from `OBJECT_DESTROYED` to `HIDDEN`, clones the original spectator trigger as `trigger-extra`, and appends it so the spectator trigger queue count becomes two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Triggered-event-kind must-not-be-redacted diagnostic.
- Keyed authoritative triggered-event-kind mismatch diagnostic for `trigger-visible`.
- Unexpected `trigger-extra` diagnostic.
- Trigger queue count mismatch diagnostic.

Assertions also prove this path does not emit:

- Controller-id must-not-be-redacted diagnostic.
- Required triggered-event-kind diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedVisibleSourceTriggeredEventKindRedactionWithCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1493/1493`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Recovery"` -> `1498/1498`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj -m:1 -p:UseSharedCompilation=false` -> `7768/7768`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `302d54c2`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 10:00 CST before docs sync.
- Project remains **NOT READY**.
