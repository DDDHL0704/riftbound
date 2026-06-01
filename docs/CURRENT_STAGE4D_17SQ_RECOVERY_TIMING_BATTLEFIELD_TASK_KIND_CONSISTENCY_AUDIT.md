# Stage 4D-17SQ Recovery Timing Battlefield-Task Kind Consistency Audit

Date: 2026-06-02 01:46 CST

Status: accepted runtime validation slice. Project remains **NOT READY**.

## Scope

Stage 4D-17SQ tightens P1-004 recovery/replay determinism for pending timing `battlefieldTasks[]` payloads. The slice only changes recovery validation and conformance tests. It does not change command resolution, protocol shape, frontend, matrix JSON, official catalog, browser/Chrome/formal E2E scripts, `fullOfficial`, final readiness status or solution files.

## Runtime Change

`MatchRecoveryValidator` now validates same-payload consistency between battlefield-task `kind`, `status` and `reason` for both recovered player-view snapshots and spectator replay frames:

- `START_SPELL_DUEL` tasks require reason `BATTLEFIELD_CONTESTED`.
- `START_SPELL_DUEL` tasks reject status `WAITING_FOR_SPELL_DUEL`.
- `START_BATTLE` tasks require reason `SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST`.
- `START_BATTLE` tasks reject status `COMPLETED`.

The consistency diagnostics run after scalar known-value and derived-identity checks, and before spectator authoritative battlefield-task parity checks can skip due to count mismatch. This preserves explicit same-payload diagnostics for forged status/reason combinations even when spectator `battlefieldTasks[]` count differs from authoritative state.

## Tests

New coverage:

- `RecoveryValidatorRejectsSnapshotTimingBattlefieldTaskKindConsistencyDrift`
- `RecoveryValidatorRejectsSpectatorReplayTimingBattlefieldTaskKindConsistencyWithCountMismatch`

The snapshot test covers the four invalid same-payload combinations with otherwise valid derived ids. The spectator test covers the same four payloads under a battlefield-task count mismatch where authoritative parity is skipped, proving the new consistency diagnostics still run.

## Validation

Passed:

- Focused kind-consistency tests: `2/2`
- Focused BattlefieldTask filter: `34/34`
- Focused recovery filter: `608/608`
- Adjacent recovery/opening/store-smoke filter: `1208/1208`
- Backend full: `6554/6554`
- Mechanical checks: `git diff --check`, anchored conflict-marker scan over `docs`/`src`/`tests`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

## Remaining

This narrows recovery/replay determinism for timing battlefield-task kind/status/reason combinations. Broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload shape/value breadth, full LayerEngine breadth, P0/P1, frontend build, Chrome smoke, formal E2E, `fullOfficial` and final status remain open. Project remains **NOT READY**.
