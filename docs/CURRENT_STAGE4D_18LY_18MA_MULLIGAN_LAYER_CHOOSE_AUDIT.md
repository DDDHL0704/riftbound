# Stage 4D-18LY/18LZ/18MA Mulligan Layer Choose Audit

Date: 2026-06-06 03:55 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18LY integrated worker commit `3808518c`: `OfficialOpeningTests` now covers official `MULLIGAN` duplicate `clientIntentId` raw-payload behavior during the opening mulligan window. Exact same raw payload replays the cached accepted mulligan output without journal growth or state/event/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation or intent-id leakage.
- 18LZ integrated worker commit `9be880f6`: `LayerEngineTimestampDependencyTests` now covers object static-aura participant/dependency metadata for Ornn friendly equipment when public-field order differs from canonical object-id order. Authoritative state and P1/P2 snapshots keep canonical participant/dependency ordering and exclude face-down equipment.
- 18MA integrated worker commit `0a183a0e`: `ConformanceFixtureShapeTests` now covers `CHOOSE_HAND_CARDS` command-mapper payload shape. Normal `choiceId`/`choiceWindow`/`chosenObjectIds` JSON maps to `ChooseHandCardsCommand`; malformed scalar `chosenObjectIds` is preserved as a stable null list for runtime validation.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `OfficialOpeningTests|LayerEngineTimestampDependencyTests|ConformanceFixtureShapeTests`: `722/722`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|ConformanceFixtureShapeTests|PaymentEngineUnificationTests|UndercoverAgentTriggerTests|GameHubJoinTests|OfficialOpeningTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests|LayerEngineTimestampDependencyTests|OrnnFriendlyEquipmentStaticPowerTests`: `5325/5325`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7296/7296`.
- Mechanical checks after code integration: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` passed.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
