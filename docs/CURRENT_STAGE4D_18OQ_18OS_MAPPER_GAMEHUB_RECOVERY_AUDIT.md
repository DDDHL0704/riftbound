# Stage 4D-18OQ/18OR/18OS Mapper/GameHub/Recovery Audit

Date: 2026-06-06 09:11 CST

Owner: A_MAIN

Project status: **NOT READY**

## Scope

A_MAIN integrated three parallel Goal-mode worker slices from independent worktrees:

- 18OQ `codex/stage4d-18oq-mapper-p0-list-shape`: `ConformanceFixtureShapeTests` now proves `PAY_COST.paymentChoiceIds` is authoritative over the visible prompt metadata alias `paymentChoices`; malformed current `paymentChoiceIds` and alias-only `paymentChoices` both leave `PaymentChoiceIds` null instead of falling back.
- 18OR `codex/stage4d-18or-gamehub-surrender-after-finished`: `GameHubJoinTests` now proves raw `SURRENDER` after a finished GameHub match returns stable `MatchFinished`, redacts client-intent/raw/sentinel/internal/debug strings, emits no caller/group broadcasts, does not grow the journal, and preserves both finished snapshots.
- 18OS `codex/stage4d-18os-recovery-battle-damage-map`: `MatchRecoveryTests` now proves spectator replay timing `battle.damageAssignment.requiredAssignments[]` reports stable missing-object-registry diagnostics for missing required-assignment source and legal-target object ids while also flagging authoritative battle mismatch.

Runtime changed: no, server test coverage only.

## Integration

Worker source commits:

- 18OQ source `1d691355`
- 18OR source `0c2a7ed8`
- 18OS source `55ab75cc`

Main cherry-picks:

- `ac8e03c6` Add PAY_COST payment choice mapper boundary test
- `09878dd3` Add GameHub surrender after-finished guard test
- `d446bc9c` Add damage assignment required assignment registry drift test

A_MAIN reviewed the worker diffs before integration. During 18OR, an accidental main-worktree draft of the same GameHub test was removed before cherry-picking the official worker commit; main was restored clean before integration.

## Validation

Passed on main:

- Focused new tests: `3/3`
- Touched class filter (`ConformanceFixtureShapeTests|GameHubJoinTests|MatchRecoveryTests`): `1588/1588`
- Broader adjacent server filter (`ConformanceFixtureShapeTests|GameHubJoinTests|MatchRecoveryTests|BattleDamageAssignmentLifecycleTests|PostgresMatchRecoveryStoreSmokeTests|OfficialOpeningTests|PaymentEngineUnificationTests|ConformanceFixtureRunnerTests|SpellDuelBattleStateMachineTests`): `5385/5385`
- Backend full via tracked `Riftbound.slnx`: `7348/7348`
- `git diff --check`
- `git diff 29ae0c6b..HEAD --check`
- Anchored conflict-marker scan over `docs`, `tests`, and `src`
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`

DOC_MATRIX_CURRENT was clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 09:11 CST.

## Remaining Gates

This narrows mapper command boundary, GameHub finished-session redaction, and recovery spectator battle-damage assignment object-registry coverage only. Broader P0/P1, command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final readiness remain open.

Project remains **NOT READY**.
