# Stage 4D-18OT/18OU/18OV Mapper/GameHub/Layer Audit

Date: 2026-06-06 09:30 CST

Owner: A_MAIN

Project status: **NOT READY**

## Scope

A_MAIN integrated three parallel Goal-mode worker slices from independent worktrees:

- 18OT `codex/stage4d-18ot-mapper-hand-choice-boundary`: `ConformanceFixtureShapeTests` now proves `CHOOSE_HAND_CARDS.chosenObjectIds` is authoritative over visible prompt metadata alias `handChoices`; malformed current `chosenObjectIds` and alias-only `handChoices` both leave `ChosenObjectIds` null instead of falling back.
- 18OU `codex/stage4d-18ou-gamehub-playcard-after-finished`: `GameHubJoinTests` now proves raw `PLAY_CARD` after a finished GameHub match returns stable `MatchFinished`, redacts client-intent/raw/sentinel/internal/debug command strings, emits no caller/group broadcasts or group errors, does not grow the journal, and preserves both finished snapshots.
- 18OV `codex/stage4d-18ov-layer-stale-participant-location`: `LayerEngineTimestampDependencyTests` now proves battlefield static-aura participant metadata honors `ObjectLocations` when `PlayerZones` still contains a stale battlefield participant; the stale defender is excluded from targets, target dependencies, participants and participant dependencies across authoritative state and P1/P2 snapshots.

Runtime changed: no, server test coverage only.

## Integration

Worker source commits:

- 18OT source `d28ae8c6`
- 18OU source `ed24ead2`
- 18OV source `6dbfc7c4`

Main cherry-picks:

- `fcad409b` Add choose hand cards mapper alias regression
- `b9746890` Add PLAY_CARD after-finished regression
- `679db7f3` Add stale battlefield participant aura regression

## Validation

Passed on main:

- Focused new tests: `3/3`
- Touched class filter (`ConformanceFixtureShapeTests|GameHubJoinTests|LayerEngineTimestampDependencyTests`): `313/313`
- Broader adjacent server filter (`ConformanceFixtureShapeTests|GameHubJoinTests|LayerEngineTimestampDependencyTests|MatchRecoveryTests|BattleDamageAssignmentLifecycleTests|PaymentEngineUnificationTests|OfficialOpeningTests|ConformanceFixtureRunnerTests|SpellDuelBattleStateMachineTests|OrnnFriendlyEquipmentStaticPowerTests`): `5420/5420`
- Backend full via tracked `Riftbound.slnx`: `7351/7351`
- `git diff --check`
- `git diff 69a2bbe0..HEAD --check`
- Anchored conflict-marker scan over `docs`, `tests`, and `src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

DOC_MATRIX_CURRENT was clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 09:30 CST.

## Remaining Gates

This narrows hand-choice mapper alias, GameHub finished-session PLAY_CARD redaction/no-mutation, and LayerEngine battlefield static-aura stale participant object-location coverage only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.

Project remains **NOT READY**.
