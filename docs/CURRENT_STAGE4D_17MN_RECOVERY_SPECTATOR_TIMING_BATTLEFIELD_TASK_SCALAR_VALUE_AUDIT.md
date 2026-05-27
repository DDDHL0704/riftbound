# Stage 4D-17MN Recovery Spectator Timing Battlefield Task Scalar Value Audit

Date: 2026-05-28 02:04 CST

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Change

- `MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates scalar/id values inside spectator replay-frame timing `battlefieldTasks[]` object entries before battlefield-task parity consumers compare those entries against authoritative state.
- Required `taskId`, `kind`, `status`, `reason` and `battlefieldObjectId` reject missing/null, non-string, blank and surrounding-whitespace drift.
- Optional-present `actingPlayerId`, `spellDuelId` and `battleId` reject malformed non-string or surrounding-whitespace drift while preserving absent/null/empty compatibility.
- Duplicate normalized spectator battlefield task `taskId` values now fail explicitly.

## Test Coverage

- Added `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskScalarValueDrift`.
- The test mutates a spectator replay frame with object-shaped `battlefieldTasks[]` entries and asserts explicit diagnostics for whitespace-normalized task ids, duplicate task ids, blank required strings, malformed required strings, malformed optional ids and surrounding-whitespace optional ids.

## Validation

- Focused single:
  `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskScalarValueDrift --no-restore`
  - Passed: `1/1`.
- Focused recovery:
  `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~MatchRecoveryTests --no-restore`
  - Passed: `371/371`.
- Adjacent recovery/opening/store-smoke:
  `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Passed: `952/952`.
- Backend full:
  `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore`
  - Passed: `6317/6317`.
- Mechanical checks:
  - `git diff --check`
  - `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Status

This narrows replay/recovery determinism only. It does not change protocol shape, frontend behavior, matrix scope, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final status or `riftbound-dotnet.sln`. Project remains **NOT READY**.
