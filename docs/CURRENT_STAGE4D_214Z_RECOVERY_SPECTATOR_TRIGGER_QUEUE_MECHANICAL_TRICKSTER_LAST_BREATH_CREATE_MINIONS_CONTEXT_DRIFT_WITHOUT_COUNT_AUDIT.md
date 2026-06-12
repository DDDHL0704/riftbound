# Stage 4D-214Z Recovery Spectator Trigger Queue Mechanical Trickster Last-Breath Create Minions Context Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Covered spectator replay timing `triggerQueue[]` Mechanical Trickster last-breath create minions context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMechanicalTricksterLastBreathCreateMinionsContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Mechanical Trickster last-breath create minions trigger queue state.
- The test keeps the authoritative trigger queue count while mutating the spectator trigger payload to `sourceVisibility` `HIDDEN`, `effectKind` `WRONG_EFFECT`, and `triggeredByEventKind` `CARD_PLAYED`.
- Recovery validation emits the Mechanical Trickster last-breath create minions source visibility, effect kind, and triggered event kind diagnostics.
- The test also proves the validation does not rely on `spectator replay frame timing trigger queue count`.
- This complements the with-count companion, which still proves the same context diagnostics together with unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMechanicalTricksterLastBreathCreateMinionsContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- Mechanical checks: `git diff --check` passed; anchored conflict-marker scan over `docs tests src` passed.

## Commits

- Code: `9b0033b0` (`test: cover mechanical trickster trigger drift without count`)
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator mechanical trickster trigger drift`)

## Status

This narrows spectator replay timing trigger-queue Mechanical Trickster last-breath create minions context drift validation without count mismatch only. Broader P0/P1 runtime/server closure, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open. Project remains **NOT READY**.
