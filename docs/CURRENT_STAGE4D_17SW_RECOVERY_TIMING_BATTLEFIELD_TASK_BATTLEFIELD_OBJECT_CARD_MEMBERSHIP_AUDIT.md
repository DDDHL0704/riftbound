# Stage 4D-17SW Recovery Timing Battlefield Task Battlefield Object Card Membership Audit

Status: accepted server recovery validation closure slice. Project remains **NOT READY**.

Date: 2026-06-02 08:19 CST

## Scope

This slice tightens recovery timing battlefield-task validation so pending recovered player-view snapshot and spectator replay-frame `battlefieldTasks[]` `battlefieldObjectId` values must reference objects tagged with `P6TokenFactoryCatalog.BattlefieldCardTag`.

Runtime battlefield tasks are derived from `MatchState.BattlefieldStates`, and battlefield states are built only from player-zone battlefield object ids whose `CardObjectState.Tags` contain the battlefield-card tag. Prior slices checked battlefield object registry membership, derived ids, participant object location membership, participant unit-card membership and participant controller consistency; this slice closes the remaining same-payload gap where a non-battlefield object could be used as the task battlefield id while all participant data remained otherwise coherent.

## Runtime Change

- Recovered snapshot battlefield-task scalar validation now builds the snapshot object-tag index and checks `battlefieldObjectId` tag membership.
- Spectator replay battlefield-task scalar validation now reuses the authoritative object-tag index and checks `battlefieldObjectId` tag membership before authoritative task parity.
- The validator emits explicit diagnostics when a battlefield task battlefield object is missing readable tags or is not a battlefield card.

Protocol shape changed: no. Command resolution, frontend, matrix JSON, official catalog, `fullOfficial`, Chrome/browser/formal E2E and final readiness status are unchanged.

## Coverage

New tests:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskBattlefieldObjectWithoutBattlefieldTag`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskBattlefieldObjectWithoutBattlefieldTag`

Validation passed:

- focused battlefield-object card membership tests: `2/2`
- focused BattlefieldTask filter: `46/46`
- focused recovery: `620/620`
- adjacent recovery/opening/store-smoke filter: `1201/1201`
- backend full: `6566/6566`
- mechanical checks: `git diff --check`; anchored conflict-marker scan; `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining Risk

This narrows P1-004 replay/recovery determinism and timing battlefield-task battlefield-object card membership enforcement only. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open.
