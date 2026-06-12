# Stage 4D-216B Recovery Spectator Trigger Queue Jhin Movement Resource Source Visibility Payload Context Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` Jhin movement resource source-visibility-payload context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceSourceVisibilityPayloadContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Jhin movement resource trigger queue state.
- The test keeps the authoritative trigger queue count while changing the spectator trigger's `sourceVisibility` payload from `VISIBLE` to `HIDDEN`.
- Recovery validation emits the hidden source object id redaction, hidden effect kind redaction, and keyed source-visibility mismatch diagnostics for trigger id `JHIN_MOVE_RESOURCE::3::source-1::BASE::BATTLEFIELD`.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same source-visibility-payload diagnostics together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueJhinMovementResourceSourceVisibilityPayloadContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1830/1830`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1835/1835`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8118/8118`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `a45d5532` (`test: cover jhin movement source visibility payload trigger drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator jhin movement source visibility payload trigger drift`)

## Status

This narrows spectator replay timing trigger-queue Jhin movement resource source-visibility-payload context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
