# Stage 4D-17SX Recovery Timing Battlefield Task Battlefield State Membership Audit

Status: accepted server recovery validation closure slice. Project remains **NOT READY**.

Date: 2026-06-02 08:33 CST

## Scope

This slice tightens recovery timing battlefield-task validation so pending recovered player-view snapshot and spectator replay-frame `battlefieldTasks[]` `battlefieldObjectId` values must reference the current battlefield state set, not merely an object-registry member with a battlefield-card tag.

Runtime battlefield tasks are derived from `MatchState.BattlefieldStates`, and battlefield states are built only from player-zone battlefield object ids whose card objects are current battlefield cards. Stage 4D-17SW checked battlefield-card tag membership; this slice closes the remaining same-payload gap where a battlefield-card object could exist in object payloads / authoritative `CardObjects` while absent from the snapshot or authoritative battlefield state set that can actually produce pending battlefield tasks.

## Runtime Change

- Recovered snapshot battlefield-task scalar validation now reads `snapshot.Lanes["battlefields"]` and checks task `battlefieldObjectId` values against readable `battlefields[].battlefieldObjectId` state ids.
- Spectator replay battlefield-task scalar validation now checks task `battlefieldObjectId` values against authoritative `MatchState.BattlefieldStates`.
- The validator emits explicit same-payload diagnostics before spectator authoritative battlefield-task parity, including count-mismatch paths where parity is skipped.

Protocol shape changed: no. Command resolution, frontend, matrix JSON, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final readiness status are unchanged.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskBattlefieldObjectOutsideBattlefieldStates`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskBattlefieldObjectOutsideBattlefieldStates`

Validation passed:

- focused battlefield-state membership tests: `2/2`
- focused BattlefieldTask filter: `48/48`
- focused recovery: `622/622`
- adjacent recovery/opening/store-smoke filter: `1203/1203`
- backend full: `6568/6568`
- mechanical checks: `git diff --check`; anchored conflict-marker scan; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This narrows P1-004 replay/recovery determinism and timing battlefield-task battlefield-state membership enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
