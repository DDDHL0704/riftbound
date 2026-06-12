# Stage 4D-213V Recovery Spectator Trigger Queue Teemo On-Play Self-Power Context Drift Without Count Audit

Date: 2026-06-13

## Scope

- Covered spectator replay timing `triggerQueue[]` Teemo on-play self-power effect/event context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Teemo on-play self-power trigger queue state.
- The test mutates the spectator trigger `effectKind` to `WRONG_EFFECT` and `triggeredByEventKind` to `CARD_PLAYED` while preserving the authoritative trigger queue count.
- Recovery validation emits the Teemo on-play self-power effect-kind diagnostic requiring `TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3` and the triggered-event-kind diagnostic requiring `UNIT_PLAYED_TO_BASE` while preserving no trigger queue count mismatch.
- This complements the existing Teemo on-play self-power context drift with count mismatch that also proves the unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- `git diff --check` passed.
- Anchored conflict-marker scan `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Commits

- Code: `0e6eec9c` (`test: cover spectator teemo context drift without count`).
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator teemo context drift`).

## Remaining Risk

This narrows spectator replay timing trigger-queue Teemo on-play self-power effect/event context drift validation without count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open. Project remains **NOT READY**.
