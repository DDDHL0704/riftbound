# Stage 4D-215M Recovery Spectator Trigger Queue Friendly-Destroyed Destroyed Object Id Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` Ghostly Centaur friendly-destroyed power destroyed object id context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectIdContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Ghostly Centaur friendly-destroyed power trigger queue state.
- The test keeps the authoritative trigger queue count while the trigger id encodes destroyed object id `destroyed-404`, which is absent from the authoritative object registry.
- Recovery validation emits the Ghostly Centaur friendly-destroyed power destroyed object id is missing from object registry diagnostic.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same destroyed object-id diagnostic together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueFriendlyDestroyedDestroyedObjectIdContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `d2291d07` (`test: cover friendly destroyed destroyed object id drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator friendly destroyed destroyed object id drift`)

## Status

This narrows spectator replay timing trigger-queue Ghostly Centaur friendly-destroyed power destroyed object id context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
