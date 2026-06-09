# Stage 4D-200K Recovery Trigger Queue Hidden Triggered Event Kind Redaction Count Audit

Date: 2026-06-09

Owner: A_MAIN

## Scope

Stage 4D-200K adds one server-test shard for spectator recovery replay timing hidden-source trigger-queue triggered-event-kind redaction-sentinel validation with a trigger queue count mismatch.

Runtime changed: no. Test coverage only.

Files changed:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage

New test:

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindRedactionWithCountMismatch`

The test builds an authoritative state with one hidden-source trigger queue item `trigger-hidden`, controlled by `alice`, sourced by the face-down object `hidden-source-1`, with redacted spectator `sourceObjectId = HIDDEN`, `sourceVisibility = HIDDEN`, `effectKind = HIDDEN`, and `triggeredByEventKind = BATTLEFIELD_HELD`. It starts from the redacted spectator replay frame, changes only `triggerQueue[0].triggeredByEventKind` from `BATTLEFIELD_HELD` to redaction sentinel `HIDDEN`, then appends an otherwise copied spectator trigger with `triggerId = trigger-extra` so the spectator trigger queue count is two while the authoritative trigger queue count remains one.

Assertions prove recovery validation emits:

- Triggered-event-kind must-not-be-redacted diagnostic.
- Keyed authoritative triggered-event-kind mismatch diagnostic.
- Unexpected `trigger-extra` diagnostic.
- Trigger queue count mismatch diagnostic.

Assertions also prove this path does not emit:

- Controller-id must-not-be-redacted diagnostic.
- Required triggered-event-kind diagnostic.

## Validation

Passed:

- Focused: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKeyedHiddenSourceTriggeredEventKindRedactionWithCountMismatch"` -> `1/1`.
- Changed class: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> `1509/1509`.
- Adjacent recovery: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~Recovery"` -> `1514/1514`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx` -> `7784/7784`.
- Mechanical: `git diff --check` passed.
- Mechanical: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no conflict markers.

## Coordination

- Code commit: `3f851748`.
- Push after the code commit succeeded.
- No subagent and no new worktree were created.
- DOC_MATRIX_CURRENT was observed clean at `17bde0c3` on 2026-06-09 12:38 CST before docs sync.
- Project remains **NOT READY**.
