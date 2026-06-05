# Stage 4D-18LP/18LQ/18LR Assemble PassFocus Audit

Date: 2026-06-06 02:55 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18LP integrated worker commit `a3472fc2`: `ConformanceFixtureRunnerTests` now covers session-level `ASSEMBLE_EQUIPMENT` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same prompt-scoped raw payload replays the cached accepted assemble result without journal growth or state/event/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18LQ integrated worker commit `639d001a`: `GameHubJoinTests` now covers GameHub `ASSEMBLE_EQUIPMENT` duplicate `clientIntentId` raw-payload behavior in the assemble-payment-recycle seed. Exact same raw payload replays the accepted rune recycle, cost paid and equipment attached output without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation.
- 18LR integrated worker commit `a3016f4d`: `SpellDuelBattleStateMachineTests` now covers `PASS_FOCUS` duplicate `clientIntentId` raw-payload behavior during spell-duel focus. Exact same raw payload replays the cached accepted focus-pass result without journal growth or state/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureRunnerTests|GameHubJoinTests|SpellDuelBattleStateMachineTests`: `3206/3206`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|ConformanceFixtureShapeTests|BattleDamageAssignmentLifecycleTests|GameHubJoinTests|OfficialOpeningTests|SpellDuelBattleStateMachineTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests`: `5251/5251`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7287/7287`.
- Mechanical checks after code integration: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
