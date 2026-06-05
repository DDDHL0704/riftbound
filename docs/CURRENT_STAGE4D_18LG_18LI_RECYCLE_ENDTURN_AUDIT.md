# Stage 4D-18LG/18LH/18LI Recycle EndTurn Audit

Date: 2026-06-06 01:54 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18LG integrated worker commit `684d4935`: `ConformanceFixtureRunnerTests` now covers `RECYCLE_RUNE` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same prompt-scoped raw payload replays the cached accepted recycle result without journal growth or state/tick/event/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18LH integrated worker commit `cdb56fc4`: `GameHubJoinTests` now covers GameHub official main-phase `RECYCLE_RUNE` duplicate `clientIntentId` raw-payload behavior after tapping a rune. Exact same raw payload replays the accepted recycle event/snapshot/prompt broadcast without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation.
- 18LI integrated worker commit `6e58aa25`: `OfficialOpeningTests` now covers official first-turn `END_TURN` duplicate `clientIntentId` raw-payload behavior after final mulligan. Exact same raw payload replays the accepted next-player turn-start result without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without state/snapshot drift.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureRunnerTests|GameHubJoinTests|OfficialOpeningTests`: `3776/3776`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|ConformanceFixtureShapeTests|BattleDamageAssignmentLifecycleTests|GameHubJoinTests|OfficialOpeningTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests`: `5234/5234`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7278/7278`.
- Mechanical checks after code integration: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
