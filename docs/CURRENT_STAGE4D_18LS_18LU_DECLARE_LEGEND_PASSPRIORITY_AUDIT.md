# Stage 4D-18LS/18LT/18LU Declare Legend PassPriority Audit

Date: 2026-06-06 03:13 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18LS integrated worker commit `63e6530b`: `ConformanceFixtureRunnerTests` now covers session-level `DECLARE_BATTLE` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same raw payload replays the cached accepted battle declaration result without journal growth or state/event/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18LT integrated worker commit `933c3ecb`: `GameHubJoinTests` now covers GameHub `LEGEND_ACT` duplicate `clientIntentId` raw-payload behavior in the development legend-act seed. Exact same raw payload replays the accepted legend activation, experience spend, exhaustion and card draw output without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation.
- 18LU integrated worker commit `6a489c40`: `SpellDuelBattleStateMachineTests` now covers spell-duel stack `PASS_PRIORITY` duplicate `clientIntentId` raw-payload behavior. Exact same raw payload replays the cached accepted stack-resolution result without journal growth or state/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureRunnerTests|GameHubJoinTests|SpellDuelBattleStateMachineTests`: `3209/3209`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|ConformanceFixtureShapeTests|BattleDamageAssignmentLifecycleTests|GameHubJoinTests|OfficialOpeningTests|SpellDuelBattleStateMachineTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests`: `5254/5254`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7290/7290`.
- Mechanical checks after code integration: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
