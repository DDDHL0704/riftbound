# Stage 4D-214M Recovery Spectator Trigger Queue Sad Poro Nonisolated Source Context Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` Sad Poro last-breath nonisolated-source context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSadPoroLastBreathNonIsolatedSourceContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Sad Poro last-breath draw trigger queue state.
- The test keeps the authoritative trigger queue count while source object id `source-1` has another friendly face-up unit in controller `alice`'s base zone.
- Recovery validation emits the Sad Poro last-breath draw source object id `source-1` must be isolated from other friendly face-up units in authoritative state player zones base for controller id `alice` diagnostic.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same nonisolated-source diagnostic together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueSadPoroLastBreathNonIsolatedSourceContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `a9895b18` (`test: cover spectator sad poro nonisolated source drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator sad poro nonisolated source drift`)

## Status

This narrows spectator replay timing trigger-queue Sad Poro last-breath nonisolated-source context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
