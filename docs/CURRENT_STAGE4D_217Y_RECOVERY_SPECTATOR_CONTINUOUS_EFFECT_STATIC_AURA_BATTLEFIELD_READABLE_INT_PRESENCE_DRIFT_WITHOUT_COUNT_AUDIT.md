# Stage 4D-217Y Recovery Spectator Continuous Effect Static Aura Battlefield Readable Int Presence Drift Without Count Audit

Date: 2026-06-13

Owner: A_MAIN

## Scope

- Direct single-agent server-test shard.
- Runtime changed: no, server test coverage only.
- Touched test file: `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.
- Dedicated closure surface: spectator replay timing `continuousEffects[]` battlefield static-aura forbidden readable-int field validation without relying on a continuous-effect count mismatch.

## Coverage

- `RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldReadableIntPresenceDriftWithoutCountMismatch` builds a spectator replay frame from authoritative battlefield static-aura state.
- The authoritative continuous-effect count remains unchanged at one naturally generated `BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE` static aura.
- The fixture uses battlefield source object `battlefield-1` with card number `OGN·294/298` and participant unit `participant-1` at the same battlefield, causing recovery redaction to emit a battlefield static-aura effect for the participant.
- The spectator continuous effect keeps the existing effect item, count, scope, duration, target object id and source order, but injects forbidden readable-int fields: `requestedPowerDelta`, `appliedPowerDelta`, `minimumPower`, `resultingPower` and `appliedOrder`.
- Recovery validation must emit all five static-aura readable-int absence diagnostics.
- The test also proves the diagnostics are emitted without any spectator replay timing continuous-effect count mismatch.
- Existing synthetic multi-item static-aura applied-order and modifier-scalar consistency tests with count mismatch remain intact.

## Validation

- Focused test: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingContinuousEffectStaticAuraBattlefieldReadableIntPresenceDriftWithoutCountMismatch"` passed `1/1`.
- Changed-class filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecoveryTests"` passed `1848/1848`.
- Adjacent recovery filter: `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --no-restore --filter "FullyQualifiedName~MatchRecovery"` passed `1853/1853`.
- Backend full: `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore` passed `8136/8136`.
- Mechanical checks passed before docs sync: `git diff --check`; `rg -n "^(<<<<<<<|=======|>>>>>>>)" tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs docs src`.

## Commits

- Code: `d779fe11` (`test: cover battlefield static aura readable ints without count`)
- Docs: this checkpoint.

## Remaining

- This narrows spectator replay timing continuous-effect battlefield static-aura readable-int absence validation without count mismatch only.
- Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final status remain open.
- Project remains **NOT READY**.
