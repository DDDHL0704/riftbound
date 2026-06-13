# Stage 4D-218G Recovery Spectator Trigger Queue Payload Shape Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `triggerQueue` top-level payload-shape validation without relying on a trigger queue count mismatch.

## Coverage

- Renamed the existing top-level shape payload test to `RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueShapePayloadWithoutCountMismatch`.
- The authoritative trigger queue count remains unchanged at zero.
- The spectator timing map replaces `triggerQueue` with a dictionary payload instead of a list payload.
- Recovery validation must emit the top-level `spectator replay frame timing trigger queue payload is required` diagnostic.
- The existing no-count negative assertion remains intact, proving this shape diagnostic is emitted without any spectator replay timing trigger queue count mismatch.
- Stage 4D-218F already locked the companion top-level missing/null payload required paths.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingTriggerQueueShapePayloadWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1851/1851`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1856/1856`.
- Backend full was not rerun for this second routine test naming shard after Stage 4D-218E; latest backend full remains Stage 4D-218E at `8139/8139`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `e942dd58` (`test: mark trigger queue payload shape without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing trigger queue top-level payload-shape validation without trigger queue count mismatch only.
- Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
