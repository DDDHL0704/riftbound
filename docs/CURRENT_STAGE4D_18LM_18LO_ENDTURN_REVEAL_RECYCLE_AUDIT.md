# Stage 4D-18LM/18LN/18LO EndTurn Reveal Recycle Audit

Date: 2026-06-06 02:32 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18LM integrated worker commit `48953ee3`: `ConformanceFixtureRunnerTests` now covers session-level `END_TURN` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same prompt-scoped raw payload replays the cached accepted end-turn result without journal growth or state/tick/event/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18LN integrated worker commit `d26c910d`: `GameHubJoinTests` now covers GameHub `REVEAL_CARD` duplicate `clientIntentId` raw-payload behavior in the standby reaction seed. Exact same raw payload replays the accepted reveal-card event/snapshot/prompt broadcast without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation.
- 18LO integrated worker commit `7633bd23`: `OfficialOpeningTests` now covers official first-turn `RECYCLE_RUNE` duplicate `clientIntentId` raw-payload behavior after final mulligan. Exact same raw payload replays the accepted rune recycle result without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without state/snapshot drift.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureRunnerTests|GameHubJoinTests|OfficialOpeningTests`: `3782/3782`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|ConformanceFixtureShapeTests|BattleDamageAssignmentLifecycleTests|GameHubJoinTests|OfficialOpeningTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests`: `5240/5240`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7284/7284`.
- Mechanical checks after code integration: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
