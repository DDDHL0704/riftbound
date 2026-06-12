# Stage 4D-215K Recovery Spectator Trigger Queue Friendly-Destroyed Source Field Zone Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` Ghostly Centaur friendly-destroyed power source field-zone context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedSourceFieldZoneContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Ghostly Centaur friendly-destroyed power trigger queue state.
- The test keeps the authoritative trigger queue count while source object id `source-1` is located in `alice`'s `BASE` but absent from the trigger controller field zone in authoritative state player zones.
- Recovery validation emits the Ghostly Centaur friendly-destroyed power source object must be in trigger controller field zone diagnostic.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same source field-zone diagnostic together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedSourceFieldZoneContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `485df374` (`test: cover friendly destroyed source field zone drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator friendly destroyed source field zone drift`)

## Status

This narrows spectator replay timing trigger-queue Ghostly Centaur friendly-destroyed power source field-zone context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
