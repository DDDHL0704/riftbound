# Stage 4D-18OW/18OX/18OY GameHub/Mapper/Layer Audit

Date: 2026-06-06 09:52 CST

Owner: A_MAIN

Project status: **NOT READY**

## Scope

A_MAIN integrated three parallel Goal-mode worker slices from independent worktrees:

- 18OW `codex/stage4d-18ow-gamehub-moveunit-after-finished`: `GameHubJoinTests` now proves raw `MOVE_UNIT` after a finished GameHub match returns stable `MatchFinished`, redacts client-intent/raw/sentinel/internal/debug command strings, emits no caller/group broadcasts or group errors, does not grow the journal, and preserves both finished snapshots.
- 18OX `codex/stage4d-18ox-mapper-declarebattle-alias`: `ConformanceFixtureShapeTests` now proves `DECLARE_BATTLE` is mapped only from current command payload fields and does not backfill from visible prompt metadata aliases such as source requirements, attacker/defender choices, battlefield choices, target choices or optional-cost choices.
- 18OY `codex/stage4d-18oy-layer-stale-sourceorder-participant`: `LayerEngineTimestampDependencyTests` now proves battlefield static-aura source-order dependency metadata honors `ObjectLocations` when `PlayerZones` still contains a stale battlefield participant; stale participant/target ids are excluded from authoritative effects and P1/P2 snapshots.

Runtime changed: no, server test coverage only.

## Integration

Worker source commits:

- 18OW source `a785d897`
- 18OX source `c8d8185`
- 18OY source `32a15a2`

Main cherry-picks:

- `756c3eea` Add move unit after-finished regression
- `5a29b995` Add declare battle mapper alias regression
- `5e0b7bfe` Add stale source-order participant aura regression

## Validation

Passed on main:

- Focused new tests: `3/3`
- Touched class filter (`GameHubJoinTests|ConformanceFixtureShapeTests|LayerEngineTimestampDependencyTests`): `316/316`
- Broader adjacent server filter (`ConformanceFixtureShapeTests|GameHubJoinTests|LayerEngineTimestampDependencyTests|MatchRecoveryTests|BattleDamageAssignmentLifecycleTests|PaymentEngineUnificationTests|OfficialOpeningTests|ConformanceFixtureRunnerTests|SpellDuelBattleStateMachineTests|OrnnFriendlyEquipmentStaticPowerTests|BattlefieldContestBattleTaskGuardTests`): `5444/5444`
- Backend full via tracked `Riftbound.slnx`: `7354/7354`
- `git diff --check`
- `git diff 3cfe26da..HEAD --check`
- Anchored conflict-marker scan over `docs`, `tests`, and `src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

DOC_MATRIX_CURRENT was clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 09:52 CST.

## Remaining Gates

This narrows GameHub finished-session MOVE_UNIT redaction/no-mutation, DECLARE_BATTLE mapper visible-metadata no-backfill, and LayerEngine source-order stale participant object-location coverage only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.

Project remains **NOT READY**.
