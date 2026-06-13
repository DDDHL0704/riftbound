# Stage 4D-218H Recovery Spectator Trigger Queue Identity Redaction Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue[]` identity redaction-sentinel validation without relying on a trigger queue count mismatch.

## Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueIdentityRedactionSentinelWithoutCountMismatch`.
- The test builds a natural authoritative `triggerQueue[]` item with visible source object state and keeps the spectator trigger queue length equal to the authoritative trigger queue length of one.
- The spectator payload mutates `triggerId`, `controllerId` and `triggeredByEventKind` to the redaction sentinel `HIDDEN` in the same payload.
- Recovery validation must emit all three identity redaction-sentinel diagnostics for trigger id, controller id and triggered event kind.
- Recovery validation must also emit the missing/unknown trigger-id diagnostics and ordered trigger-id disagreement while avoiding any spectator replay timing trigger queue count mismatch.
- Existing single-field and count-mismatch identity redaction sentinel tests remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueIdentityRedactionSentinelWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1852/1852`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1857/1857`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8140/8140`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `092bb797` (`test: cover trigger queue identity redaction without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue identity redaction-sentinel validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
