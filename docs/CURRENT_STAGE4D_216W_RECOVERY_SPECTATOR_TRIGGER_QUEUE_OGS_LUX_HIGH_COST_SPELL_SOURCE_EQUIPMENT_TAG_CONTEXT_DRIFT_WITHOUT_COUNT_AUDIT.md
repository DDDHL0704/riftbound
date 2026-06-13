# Stage 4D-216W Recovery Spectator Trigger Queue OGS Lux High Cost Spell Source Equipment Tag Context Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` OGS Lux high-cost spell source equipment-tag context drift validation without relying on a trigger queue count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceEquipmentTagContextDriftWithoutCountMismatch` builds a spectator replay frame from authoritative OGS Lux high-cost spell trigger queue state.
- The authoritative trigger queue count remains unchanged.
- The spectator trigger keeps the authoritative trigger id `TRIGGER-stack-1-source-1-OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3` and visible source payload while authoritative source card tags include both `UnitCard` and `EquipmentCard`.
- Recovery validation must emit the OGS Lux high-cost spell source object id `source-1` must not be an equipment card diagnostic.
- The test also proves the diagnostic is emitted without any spectator replay timing trigger queue count mismatch.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueOgsLuxHighCostSpellSourceEquipmentTagContextDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1830/1830`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1835/1835`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8118/8118`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`.

## Commits

- Code: `6895ff35` (`test: cover ogs lux source equipment trigger drift without count`)
- Docs: pending this checkpoint.

## Remaining

- This narrows spectator replay timing trigger-queue OGS Lux high-cost spell source equipment-tag context drift validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
