# Stage 4D-214S Recovery Spectator Trigger Queue Standard Last-Breath Stack Context Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` standard last-breath stack-context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathStackContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Watchful Sentinel last-breath draw trigger queue state.
- The test keeps the authoritative trigger queue count while mutating the spectator trigger id from authoritative `TRIGGER-stack-1-source-1-WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1` to forged stackless `TRIGGER--source-1-WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1`.
- Recovery validation emits the Watchful Sentinel last-breath draw trigger id stack item id is required before source object id `source-1` diagnostic.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same stack-context diagnostic together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathStackContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `8d4965aa` (`test: cover spectator standard last breath stack drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator standard last breath stack drift`)

## Status

This narrows spectator replay timing trigger-queue standard last-breath stack-context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
