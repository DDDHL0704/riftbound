# Stage 4D-17TM Recovery Timing Resolution History Keyed Value Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TM narrows P1-004 recovery/replay determinism for spectator replay-frame timing `battlefieldResolutions[]` and `battleResolutions[]` payloads. The slice targets the gap left after 17TL: count mismatch now names missing and extra `resolutionId` keys, but same-key authoritative value drift still relied on broad index-based parity that is skipped when counts differ.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now validates same-key spectator resolution-history items against authoritative resolution-history state before count-equal parity checks.

Battlefield resolution keyed validation covers:

- `tick`, `kind`, `reason` and `battlefieldObjectId`
- optional `playerId`, `previousControllerId`, `controllerId` and `sourceObjectId`
- `participantObjectIds`
- `relatedEventKinds`

Battle resolution keyed validation covers:

- `tick`, `kind`, `reason` and `battlefieldId`
- optional `attackingPlayerId`, `defendingPlayerId` and `winnerPlayerId`
- `attackerObjectIds`, `defenderObjectIds`, `survivingAttackerObjectIds`, `survivingDefenderObjectIds` and `destroyedObjectIds`
- `relatedEventKinds`

This check runs alongside the 17TL key-set validation, same-payload shape/value validation, duplicate resolution-id validation and count mismatch diagnostics. The broad index-based authoritative parity checks still remain behind the count-equal gate.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryKeyedValuesWithCountMismatch`.

The test builds a spectator replay frame from authoritative resolution histories containing `battlefield-resolution-1` and `battle-resolution-1`, keeps those ids stable, mutates all same-key scalar/reference/list fields, and adds extra spectator battlefield/battle resolution items to keep both count-mismatch paths active. Validation now reports explicit same-key battlefield and battle resolution value diagnostics before broad parity would run.

## Validation

- Focused new test: `1/1`
- Focused ResolutionHistory/Resolution filter: `79/79`
- Focused recovery filter: `641/641`
- Adjacent recovery/opening/store-smoke filter: `1221/1221`
- Backend full: `6586/6586`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
