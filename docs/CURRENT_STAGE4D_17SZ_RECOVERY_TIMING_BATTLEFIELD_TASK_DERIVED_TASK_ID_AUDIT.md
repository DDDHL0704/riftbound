# Stage 4D-17SZ Recovery Timing Battlefield Task Derived Task Id Audit

Status: accepted server recovery validation closure slice. Project remains **NOT READY**.

Date: 2026-06-02 09:04 CST

## Scope

This slice tightens recovery timing battlefield-task validation so pending recovered player-view snapshot and spectator replay-frame `battlefieldTasks[]` `taskId` values must match the runtime-derived battlefield task id for the payload `kind` and `battlefieldObjectId`.

Runtime battlefield tasks are derived from `MatchState.BattlefieldStates`: `START_SPELL_DUEL` uses `task:start-spell-duel:{battlefieldObjectId}` and `START_BATTLE` uses `task:start-battle:{battlefieldObjectId}`. Earlier Stage 4D slices checked task payload shape, known values, kind/reason/status semantics, derived spell-duel and battle ids, battlefield-card membership, battlefield-state membership and participant memberships. This slice closes the same-payload gap where a recovered or spectator battlefield task could carry a valid kind and battlefield id but an unrelated task id, relying only on spectator authoritative parity when the task count happened to match.

## Runtime Change

- Recovered snapshot battlefield-task scalar validation now checks `taskId` against the expected runtime task id derived from `kind` and `battlefieldObjectId`.
- Spectator replay battlefield-task scalar validation uses the same task-id derivation before authoritative battlefield-task parity comparison.
- The validator emits explicit same-payload task-id drift diagnostics, including spectator count-mismatch paths where authoritative battlefield-task parity is skipped.

Protocol shape changed: no. Command resolution, frontend, matrix JSON, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final readiness status are unchanged.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskDerivedTaskIdDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskDerivedTaskIdWithCountMismatch`

Validation passed:

- focused derived task-id tests: `2/2`
- focused BattlefieldTask filter: `52/52`
- focused recovery: `626/626`
- adjacent recovery/opening/store-smoke filter: `1207/1207`
- backend full: `6572/6572`
- mechanical checks: `git diff --check`; anchored conflict-marker scan; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This narrows P1-004 replay/recovery determinism and timing battlefield-task derived task-id enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
