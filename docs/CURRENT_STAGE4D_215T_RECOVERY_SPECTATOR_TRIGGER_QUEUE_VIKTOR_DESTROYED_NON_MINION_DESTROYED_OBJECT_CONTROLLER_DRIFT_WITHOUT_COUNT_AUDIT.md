# Stage 4D-215T Recovery Spectator Trigger Queue Viktor Destroyed Non-Minion Destroyed Object Controller Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` Viktor destroyed non-minion create minion destroyed object controller context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectControllerContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Viktor destroyed non-minion create minion trigger queue state.
- The test keeps the authoritative trigger queue count while destroyed object id `destroyed-1` is a unit card in `alice`'s graveyard but has controller id `bob` while the trigger controller remains `alice`.
- Recovery validation emits the Viktor destroyed non-minion create minion destroyed object id `destroyed-1` controller id `bob` must match trigger controller id `alice` in authoritative state object registry diagnostic.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same destroyed object controller diagnostic together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueViktorDestroyedNonMinionDestroyedObjectControllerContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `2c54bcab` (`test: cover viktor destroyed object controller drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator viktor destroyed object controller drift`)

## Status

This narrows spectator replay timing trigger-queue Viktor destroyed non-minion create minion destroyed object controller context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
