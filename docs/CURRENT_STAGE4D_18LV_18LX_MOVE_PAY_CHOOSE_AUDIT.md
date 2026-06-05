# Stage 4D-18LV/18LW/18LX Move Pay Choose Audit

Date: 2026-06-06 03:33 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18LV integrated worker commit `63168d6f`: `ConformanceFixtureRunnerTests` now covers session-level `MOVE_UNIT` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same raw payload replays the cached accepted unit-move result without journal growth or state/event/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18LW integrated worker commit `06274622`: `PaymentEngineUnificationTests` now covers session-level `PAY_COST` duplicate `clientIntentId` raw-payload behavior for an ordinary pending payment window. Exact same raw payload replays the accepted cost-paid/payment-window-closed output without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without payment state, rune pool, prompt, snapshot or journal drift.
- 18LX integrated worker commit `51c34728`: `UndercoverAgentTriggerTests` now covers `CHOOSE_HAND_CARDS` duplicate `clientIntentId` raw-payload behavior for Undercover Agent pending hand choice. Exact same raw payload replays the accepted discard/draw hand-choice result without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without hand/graveyard/deck, prompt, snapshot or journal drift.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureRunnerTests|PaymentEngineUnificationTests|UndercoverAgentTriggerTests`: `3144/3144`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|ConformanceFixtureShapeTests|PaymentEngineUnificationTests|UndercoverAgentTriggerTests|GameHubJoinTests|OfficialOpeningTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests`: `5297/5297`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7293/7293`.
- Mechanical checks after code integration: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
