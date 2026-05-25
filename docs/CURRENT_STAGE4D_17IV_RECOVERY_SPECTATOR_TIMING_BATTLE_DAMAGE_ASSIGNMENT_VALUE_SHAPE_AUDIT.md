# Stage 4D-17IV Recovery Spectator Timing Battle Damage Assignment Value Shape Audit

Status: accepted by A_MAIN on 2026-05-26 01:38 CST.

Project status: **NOT READY**. This slice narrows recovery/spectator replay validation only. It does not pass or claim frontend build, Chrome smoke, formal E2E, `fullOfficial`, full official catalog gates, P0/P1 closure or final readiness.

## Scope

Stage 4D-17IV continues spectator replay-frame timing payload value-shape closure after Stage 4D-17IU. This slice covers spectator `Timing["battle"]["damageAssignment"]` nested scalar, map and required-assignment values before authoritative battle comparison consumes that payload.

The allowed runtime/test scope was:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`
- current checkpoint / completion / P0-P1 closure / next-dispatch / shared-board docs
- this dedicated audit/evidence doc

The following remain locked and unchanged: matrix JSON, `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`, frontend, protocol, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status, and `riftbound-dotnet.sln`.

## Runtime Change

`MatchRecoveryValidator.ValidateSpectatorReplayFrame` now validates spectator replay-frame `Timing["battle"]["damageAssignment"]` nested value shapes before authoritative battle comparison consumes the payload.

The damage-assignment payload now rejects missing/null/non-object payloads, and always validates required `isPending` as a boolean. When the authoritative state has an open battle damage-assignment window, or when the spectator payload claims one is pending, the validator also checks:

- required scalar fields `phase`, `battleId`, `battlefieldId` and `assigningPlayerId`
- required non-negative integer maps `damagePool`, `existingDamage` and `lethalDamageThreshold`
- required string-list map `legalTargets`
- required `requiredAssignments` list item values: `sourceObjectId`, `damage` and `legalTargetObjectIds`

Malformed nested values now fail with explicit spectator recovery diagnostics instead of surfacing only through a generic battle mismatch comparison.

## Test Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingBattleDamageAssignmentValueDrift`.

The test builds a spectator replay frame from an authoritative battle damage-assignment state, mutates `Timing["battle"]["damageAssignment"]` to cover invalid pending flags, malformed required scalar values, malformed non-negative integer maps, malformed legal-target string-list maps and malformed required-assignment item values, then asserts explicit spectator recovery diagnostics.

## Validation

Passed:

- Focused single test: `1/1`
- Focused `MatchRecoveryTests`: `275/275`
- Adjacent recovery/opening/store-smoke: `856/856`
- Backend full: `6221/6221`
- `git diff --check`
- Anchored conflict-marker scan over `docs`, `src`, and `tests`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This is not final readiness. Broader command/recovery/random determinism, remaining recovered/spectator nested payload shape breadth, full LayerEngine breadth, P0/P1 closure, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open.
