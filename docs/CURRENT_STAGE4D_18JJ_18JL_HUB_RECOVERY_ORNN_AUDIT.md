# Stage 4D-18JJ/18JK/18JL Hub Recovery Ornn Audit

Date: 2026-06-05 22:44 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18JJ integrated worker commit `a5b9d925`: `GameHubJoinTests` now covers official main-phase `TAP_RUNE` duplicate `clientIntentId` raw-payload behavior. Exact same raw payload replays stable output without journal growth; changed `sourceObjectId` plus added `clientNote` returns `CLIENT_INTENT_CONFLICT` without group/caller mutation, state drift or changed journal entries.
- 18JK integrated worker commit `1e097c0c`: `MatchRecoveryTests` now covers snapshot timing battle damage assignment with `requiredAssignments = null`, locking the stable `required assignment list is required` diagnostic.
- 18JL integrated worker commit `3bb0fe30`: `OrnnFriendlyEquipmentStaticPowerTests` now covers source-leaves-field static-aura metadata removal across P1/P2 snapshots, including dependency metadata leak checks for the Ornn source and friendly equipment object ids. The worker could not run dotnet because its PATH lacked dotnet; A_MAIN validated this slice on main.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `GameHubJoinTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests`: `1432/1432`.
- Broader adjacent server filter `GameHubJoinTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests|LayerEngineTimestampDependencyTests`: `5068/5068`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7248/7248`.
- Mechanical checks after docs sync: `git diff --check`, `git diff --cached --check`, anchored conflict-marker scan and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
