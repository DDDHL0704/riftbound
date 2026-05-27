# Stage 4D-17MO Recovery Snapshot Timing Battlefield Task Scalar Value Audit

Date: 2026-05-28 02:11 CST

Scope: server P1-004 replay/recovery determinism slice in `src/Riftbound.Engine/MatchRecovery.cs` and `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`.

## Change

- `MatchRecoveryValidator.ValidateSnapshotShape` now validates scalar/id values inside recovered player-view snapshot timing `battlefieldTasks[]` object entries before later battlefield-task consumers consume those entries.
- Required `taskId`, `kind`, `status`, `reason` and `battlefieldObjectId` reject missing/null, non-string, blank and surrounding-whitespace drift.
- Optional-present `actingPlayerId`, `spellDuelId` and `battleId` reject malformed non-string or surrounding-whitespace drift while preserving absent/null/empty compatibility.
- Duplicate normalized recovered snapshot battlefield task `taskId` values now fail explicitly.

## Test Coverage

- Added `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskScalarValueDrift`.
- The test mutates recovered player-view snapshot `Timing["battlefieldTasks"][]` object entries and asserts explicit diagnostics for whitespace-normalized task ids, duplicate task ids, blank required strings, malformed required strings, malformed optional ids and surrounding-whitespace optional ids.

## Validation

- Focused single:
  `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskScalarValueDrift --no-restore`
  - Passed: `1/1`.
- Focused recovery:
  `/Users/dinghaolin/.dotnet/dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter FullyQualifiedName~MatchRecoveryTests --no-restore`
  - Passed: `372/372`.
- Adjacent recovery/opening/store-smoke:
  `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MatchRecovery|FullyQualifiedName~PostgresMatchRecoveryStoreSmoke|FullyQualifiedName~OfficialOpening"`
  - Passed: `953/953`.
- Backend full:
  `/Users/dinghaolin/.dotnet/dotnet test Riftbound.slnx --no-restore`
  - Passed: `6318/6318`.
- Mechanical checks:
  - `git diff --check`
  - `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src`
  - `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Status

This narrows replay/recovery determinism only. It does not change protocol shape, frontend behavior, matrix scope, official catalog data, browser/Chrome/formal E2E scripts, `fullOfficial`, final status or `riftbound-dotnet.sln`. Project remains **NOT READY**.
