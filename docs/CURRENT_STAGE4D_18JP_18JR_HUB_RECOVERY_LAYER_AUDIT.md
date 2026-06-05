# Stage 4D-18JP/18JQ/18JR Hub Recovery Layer Audit

Date: 2026-06-05 23:17 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18JP integrated worker commit `635aa38a`: `GameHubJoinTests` now covers `END_TURN` duplicate `clientIntentId` raw-payload behavior. Exact same raw payload replays stable output without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation, snapshot drift or changed journal entries.
- 18JQ integrated worker commit `efe6cf79`: `MatchRecoveryTests` now covers snapshot timing battle damage assignment `requiredAssignments[]` item value drift. Empty and whitespace-only `sourceObjectId` values emit the stable source-object required diagnostic, and `damage = -1` emits the stable negative-damage diagnostic.
- 18JR integrated worker commit `8fa68644`: `LayerEngineTimestampDependencyTests` now broadens battlefield static-aura other-battlefield exclusion coverage across P1/P2 snapshots, proving the other-battlefield unit does not leak through target, dependency or participant metadata and player-view signatures stay aligned.

## Validation

- Focused new/extended tests: `3/3`.
- Touched class filter `GameHubJoinTests|MatchRecoveryTests|LayerEngineTimestampDependencyTests`: `1440/1440`.
- Broader adjacent server filter `GameHubJoinTests|MatchRecoveryTests|LayerEngineTimestampDependencyTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests|OrnnFriendlyEquipmentStaticPowerTests`: `5073/5073`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7253/7253`.
- Mechanical checks after docs sync: `git diff --check`, `git diff --cached --check`, anchored conflict-marker scan and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
