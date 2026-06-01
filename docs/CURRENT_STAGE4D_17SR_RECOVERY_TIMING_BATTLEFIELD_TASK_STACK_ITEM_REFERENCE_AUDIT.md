# Stage 4D-17SR Recovery Timing Battlefield-Task Stack-Item Reference Audit

Date: 2026-06-02 01:56 CST

Status: accepted runtime validation slice. Project remains **NOT READY**.

## Scope

Stage 4D-17SR tightens P1-004 recovery/replay determinism for pending timing `battlefieldTasks[]` payloads. The slice only changes recovery validation and conformance tests. It does not change command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or solution files.

## Runtime Change

`MatchRecoveryValidator` now validates `battlefieldTasks[].stackItemIds` membership for both recovered player-view snapshots and spectator replay frames:

- Recovered snapshot battlefield-task `stackItemIds[]` must refer to stack item ids present in the same snapshot `stack`.
- Spectator replay-frame battlefield-task `stackItemIds[]` must refer to ids present in authoritative `StackItems`.

The spectator same-payload membership diagnostics run before authoritative battlefield-task parity comparison can skip due to count mismatch. This preserves explicit stack-item reference diagnostics for forged battlefield-task payloads even when spectator `battlefieldTasks[]` count differs from authoritative state.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskStackItemReferencesOutsideSnapshotStack`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskStackItemReferencesWithCountMismatch`

The snapshot test proves a battlefield task cannot point at a stack item absent from the recovered snapshot stack. The spectator test proves the same forged reference emits a diagnostic under a battlefield-task count mismatch where authoritative parity is skipped.

## Validation

Passed:

- Focused stack-item reference tests: `2/2`
- Focused BattlefieldTask filter: `36/36`
- Focused recovery filter: `610/610`
- Adjacent recovery/opening/store-smoke filter: `1210/1210`
- Backend full: `6556/6556`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows recovery/replay determinism for timing battlefield-task stack-item references. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
