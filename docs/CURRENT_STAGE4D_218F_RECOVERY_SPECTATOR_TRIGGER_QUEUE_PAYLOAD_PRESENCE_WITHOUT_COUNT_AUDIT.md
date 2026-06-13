# Stage 4D-218F Recovery Spectator Trigger Queue Payload Presence Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue` top-level missing/null payload validation without relying on a trigger queue count mismatch.

## Coverage

- Renamed the existing top-level missing payload test to `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMissingPayloadWithoutCountMismatch`.
- Renamed the existing top-level null payload test to `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueNullPayloadWithoutCountMismatch`.
- Both tests keep the authoritative trigger queue count unchanged at zero and do not add spectator trigger queue entries.
- Recovery validation must emit the top-level `spectator replay frame timing trigger queue is required` diagnostic.
- Both tests now also prove the required-payload diagnostics are emitted without any spectator replay timing trigger queue count mismatch.
- The existing top-level shape payload test already carries no-count validation and remains intact.

## Validation

- Focused tests: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueMissingPayloadWithoutCountMismatch|FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueNullPayloadWithoutCountMismatch"` passed `2/2`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1851/1851`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1856/1856`.
- Backend full was not rerun for this first routine test naming/assertion shard after Stage 4D-218E; latest backend full remains Stage 4D-218E at `8139/8139`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `c035eca8` (`test: mark trigger queue payload presence without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue top-level missing/null payload validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
