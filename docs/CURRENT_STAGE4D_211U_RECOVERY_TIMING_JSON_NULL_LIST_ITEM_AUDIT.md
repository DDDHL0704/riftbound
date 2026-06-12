# Stage 4D-211U Recovery Timing JSON Null List Item Audit

Date: 2026-06-12 17:53 CST

Status: accepted / write lock closed. Project remains **NOT READY**.

## Scope

A_MAIN directly integrated one single-agent server-test slice for snapshot-level recovery timing list item payload-shape validation. Runtime code did not change.

Touched test file:

- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Coverage Added

`RecoveryValidatorRejectsSnapshotTimingJsonNullListItemPayloadShapeDrift` now covers a recovered snapshot timing map whose `continuousEffects[]` and `triggerQueue[]` each contain a single JSON null item via `RawJson("null")`.

The test proves:

- JSON null entries in `continuousEffects[]` are rejected as missing continuous-effect object payloads;
- JSON null entries in `triggerQueue[]` are rejected as missing trigger-queue item object payloads;
- the snapshot-level recovery validator keeps the same object-payload boundary for JSON null items as it already enforces for scalar list items.

## Validation

- Focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~RecoveryValidatorRejectsSnapshotTimingJsonNullListItemPayloadShapeDrift"` -> passed `1/1`.
- Changed class: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecoveryTests"` -> passed `1792/1792`.
- Adjacent recovery: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery"` -> passed `1797/1797`.
- Backend full: `dotnet test` -> passed `8080/8080`.
- Mechanical: `git diff --check` -> passed.
- Conflict marker scan: `rg -n "^(<<<<<<<|=======|>>>>>>>)" docs tests src` -> no matches.

## Commits

- Code: `54492225 test: cover recovery timing json null list items`

## Remaining Open Gates

This closes only the snapshot-level recovery timing JSON null list item payload-shape shard for `continuousEffects[]` and `triggerQueue[]`. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.
