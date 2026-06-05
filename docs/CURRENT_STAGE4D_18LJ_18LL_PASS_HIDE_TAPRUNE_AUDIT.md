# Stage 4D-18LJ/18LK/18LL Pass Hide TapRune Audit

Date: 2026-06-06 02:13 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18LJ integrated worker commit `0ed4a7a5` plus A_MAIN assertion fix: `ConformanceFixtureRunnerTests` now covers `PASS` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same prompt-scoped raw payload replays the cached accepted generic pass result without journal growth or state/tick/event/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation. A_MAIN corrected the initial prompt assertion to match current behavior: ordinary main prompts expose `END_TURN` and do not list `PASS`, while raw `PASS` remains accepted by the engine/session path.
- 18LK integrated worker commit `521b0680`: `GameHubJoinTests` now covers GameHub `HIDE_CARD` duplicate `clientIntentId` raw-payload behavior in the battlefield extra standby seed. Exact same raw payload replays the accepted hide-card event/snapshot/prompt broadcast without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation.
- 18LL integrated worker commit `6bfca700`: `OfficialOpeningTests` now covers official first-turn `TAP_RUNE` duplicate `clientIntentId` raw-payload behavior after final mulligan. Exact same raw payload replays the accepted rune-tap result without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without state/snapshot drift.

## Validation

- Focused new tests: `3/3` after the A_MAIN assertion fix.
- Touched class filter `ConformanceFixtureRunnerTests|GameHubJoinTests|OfficialOpeningTests`: `3779/3779`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|ConformanceFixtureShapeTests|BattleDamageAssignmentLifecycleTests|GameHubJoinTests|OfficialOpeningTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests`: `5237/5237`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7281/7281`.
- Mechanical checks after code integration: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
