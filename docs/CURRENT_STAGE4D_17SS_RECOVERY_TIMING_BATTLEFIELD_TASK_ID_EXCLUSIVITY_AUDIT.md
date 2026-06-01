# Stage 4D-17SS Recovery Timing Battlefield-Task Id Exclusivity Audit

Date: 2026-06-02 07:29 CST

Status: accepted runtime validation slice. Project remains **NOT READY**.

## Scope

Stage 4D-17SS tightens P1-004 recovery/replay determinism for pending timing `battlefieldTasks[]` payloads. The slice only changes recovery validation and conformance tests. It does not change command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or solution files.

## Runtime Change

`MatchRecoveryValidator` now validates kind-specific battlefield-task identity exclusivity for both recovered player-view snapshots and spectator replay frames:

- `START_SPELL_DUEL` tasks reject readable non-empty `battleId` payloads.
- `START_BATTLE` tasks reject readable non-empty `spellDuelId` payloads.

Current `BuildBattlefieldTaskSnapshotView` output is kind-specific: spell-duel tasks emit `spellDuelId`, and battle tasks emit `battleId`. The new diagnostics run after required derived-id validation and before authoritative spectator battlefield-task parity comparison. This preserves explicit same-payload diagnostics for forged mutually exclusive ids even when spectator `battlefieldTasks[]` count differs from authoritative state and parity is skipped.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskKindSpecificIdentityExclusivityDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskKindSpecificIdentityExclusivityWithCountMismatch`

The snapshot test proves a spell-duel task cannot carry a battle id and a battle task cannot carry a spell-duel id. The spectator test proves the same forged fields emit diagnostics under a battlefield-task count mismatch where authoritative parity is skipped.

## Validation

Passed:

- Focused id-exclusivity tests: `2/2`
- Focused BattlefieldTask filter: `38/38`
- Focused recovery filter: `612/612`
- Adjacent recovery/opening/store-smoke filter: `1212/1212`
- Backend full: `6558/6558`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows recovery/replay determinism for timing battlefield-task kind-specific id exclusivity. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
