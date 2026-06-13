# Stage 4D-216R Recovery Spectator Trigger Queue Kogmaw Last Breath Source Visibility State Context Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` Kogmaw last-breath source visibility state context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathSourceVisibilityStateContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Kogmaw last-breath trigger queue state.
- The test keeps the authoritative trigger queue count while changing the spectator trigger id from `TRIGGER-stack-1-source-1-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::battlefield-1` to forged source-visibility-state trigger id `TRIGGER-stack-1-wrong-source-OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT::BATTLEFIELD::battlefield-1`, changing the spectator trigger payload source object id from `source-1` to `wrong-source`, and setting source visibility to `HIDDEN`.
- Recovery validation emits the Kogmaw last-breath source visibility must be `VISIBLE` diagnostic.
- Recovery validation also emits the Kogmaw last-breath source object id `wrong-source` must not be face down diagnostic.
- Recovery validation also emits the Kogmaw last-breath source object id `wrong-source` must not be a standby card diagnostic.
- The test proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same Kogmaw last-breath source visibility state diagnostics together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueKogmawLastBreathSourceVisibilityStateContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1830/1830`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1835/1835`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8118/8118`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `6c18105c` (`test: cover kogmaw last breath source visibility state trigger drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator kogmaw source visibility state trigger drift`)

## Status

This narrows spectator replay timing trigger-queue Kogmaw last-breath source visibility state context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
