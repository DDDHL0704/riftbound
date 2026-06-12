# Stage 4D-213X Recovery Spectator Trigger Queue Teemo On-Play Self-Power Source Card Context Drift Without Count Audit

Date: 2026-06-13

## Scope

- Covered spectator replay timing `triggerQueue[]` Teemo on-play self-power source-card context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerSourceCardContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative Teemo on-play self-power trigger queue state.
- The test keeps the authoritative trigger queue count while the source object state exposes card no `WRONG-CARD` for source object id `source-1`.
- Recovery validation emits the Teemo on-play self-power source object id `source-1` card no `WRONG-CARD` must be `OGN·197/298` in authoritative state object registry diagnostic while preserving no trigger queue count mismatch.
- This complements the existing Teemo on-play self-power source-card context drift with count mismatch that also proves the unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueTeemoOnPlaySelfPowerSourceCardContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- `git diff --check` passed.
- Anchored conflict-marker scan `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Commits

- Code: `849771e1` (`test: cover spectator teemo source card drift without count`).
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator teemo source card drift`).

## Remaining Risk

This narrows spectator replay timing trigger-queue Teemo on-play self-power source-card context drift validation without count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open. Project remains **NOT READY**.
