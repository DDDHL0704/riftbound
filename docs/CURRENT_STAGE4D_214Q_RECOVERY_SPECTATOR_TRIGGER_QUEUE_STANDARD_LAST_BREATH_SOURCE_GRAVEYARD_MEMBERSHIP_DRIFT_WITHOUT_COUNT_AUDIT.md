# Stage 4D-214Q Recovery Spectator Trigger Queue Standard Last-Breath Source Graveyard Membership Context Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` standard last-breath source graveyard-membership context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceGraveyardMembershipContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Watchful Sentinel last-breath draw trigger queue state.
- The test keeps the authoritative trigger queue count while source object id `source-1` is located in `alice`'s `GRAVEYARD` but omitted from `alice`'s authoritative player-zone graveyard list.
- Recovery validation emits the Watchful Sentinel last-breath draw source object id `source-1` location player id `alice` must include source object in authoritative state player zones graveyard diagnostic.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same graveyard-membership diagnostic together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueStandardLastBreathSourceGraveyardMembershipContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `379eb27c` (`test: cover spectator standard last breath graveyard membership drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator standard last breath graveyard membership drift`)

## Status

This narrows spectator replay timing trigger-queue standard last-breath source graveyard-membership context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
