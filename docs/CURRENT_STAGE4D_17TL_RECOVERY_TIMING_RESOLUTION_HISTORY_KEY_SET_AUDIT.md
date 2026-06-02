# Stage 4D-17TL Recovery Timing Resolution History Key Set Audit

Date: 2026-06-02

Status: accepted for A_MAIN checkpoint. Project remains **NOT READY**.

## Scope

Stage 4D-17TL narrows P1-004 recovery/replay determinism for spectator replay-frame timing `battlefieldResolutions[]` and `battleResolutions[]` payloads. The slice targets the same count-mismatch gap recently closed for trigger queue, continuous effects and temporary payment resources: broad index-based authoritative parity is skipped when spectator and authoritative list counts differ, so missing or forged resolution identities needed explicit key-set validation before that gate.

Runtime files changed:

- `src/Riftbound.Engine/MatchRecovery.cs`
- `tests/Riftbound.ConformanceTests/MatchRecoveryTests.cs`

## Runtime Behavior

`MatchRecoveryValidator` now builds authoritative resolution-history indexes keyed by `resolutionId` from:

- `MatchState.BattlefieldResolutions`
- `MatchState.BattleResolutions`

Before broad count-equal parity checks, spectator replay-frame timing resolution-history payloads now report:

- battlefield resolution ids that are not present in authoritative battlefield resolutions
- authoritative battlefield resolution ids that are missing from the spectator payload
- battle resolution ids that are not present in authoritative battle resolutions
- authoritative battle resolution ids that are missing from the spectator payload

This check runs alongside existing same-payload shape/value validation, duplicate resolution-id validation and count mismatch diagnostics. The broad index-based authoritative parity checks still remain behind the count-equal gate.

## Coverage

Added `RecoveryValidatorRejectsSpectatorReplayTimingResolutionHistoryKeySetWithCountMismatch`.

The test builds a spectator replay frame from authoritative resolution histories containing `battlefield-resolution-1` and `battle-resolution-1`, replaces the spectator ids with `battlefield-resolution-extra-a` / `battle-resolution-extra-a`, adds `battlefield-resolution-extra-b` / `battle-resolution-extra-b`, and keeps both spectator count-mismatch paths active. Validation now reports all forged resolution ids and the missing authoritative resolution ids before count-equal parity would run.

## Validation

- Focused new test: `1/1`
- Focused ResolutionHistory/Resolution filter: `78/78`
- Focused recovery filter: `640/640`
- Adjacent recovery/opening/store-smoke filter: `1220/1220`
- Backend full: `6585/6585`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Open

This narrows recovery/replay determinism only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial`, and final readiness remain open. Project remains **NOT READY**.
