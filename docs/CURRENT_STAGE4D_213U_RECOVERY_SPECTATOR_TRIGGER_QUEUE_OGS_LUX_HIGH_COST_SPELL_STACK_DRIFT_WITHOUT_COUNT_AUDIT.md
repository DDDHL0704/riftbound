# Stage 4D-213U Recovery Spectator Trigger Queue OGS Lux High Cost Spell Stack Context Drift Without Count Audit

Date: 2026-06-13

## Scope

- Covered spectator replay timing `triggerQueue[]` OGS Lux high-cost-spell stack-context drift validation without relying on a trigger queue count mismatch.
- Strengthened one conformance test in `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Runtime changed: no, server test coverage only.
- Subagents/worktrees created: none.

## Accepted Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellStackContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative OGS Lux high-cost-spell trigger queue state.
- The test mutates the spectator trigger id from authoritative `TRIGGER-stack-1-source-1-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` to forged stackless `TRIGGER--source-1-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` while preserving the authoritative trigger queue count.
- Recovery validation emits the OGS Lux high-cost-spell trigger id stack item id is required before source object id `source-1` diagnostic while preserving no trigger queue count mismatch.
- This complements the existing OGS Lux high-cost-spell stack-context drift with count mismatch that also proves the unexpected `trigger-extra` and count-mismatch diagnostics.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellStackContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1829/1829`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1834/1834`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8117/8117`.
- `git diff --check` passed.
- Anchored conflict-marker scan `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` returned no matches.

## Commits

- Code: `debe597b` (`test: cover spectator ogs lux stack drift without count`).
- Docs: this checkpoint commit (`checkpoint: stage 4D recovery spectator ogs lux stack drift`).

## Remaining Risk

This narrows spectator replay timing trigger-queue OGS Lux high-cost-spell stack-context drift validation without count mismatch only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open. Project remains **NOT READY**.
