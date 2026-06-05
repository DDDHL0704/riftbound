# Stage 4D-18JM/18JN/18JO Hub Recovery Layer Audit

Date: 2026-06-05 22:57 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18JM integrated worker commit `a6cadd78`: `GameHubJoinTests` now covers `PAY_COST` duplicate `clientIntentId` raw-payload behavior. Exact same raw payload replays stable output without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation, state drift or changed journal entries.
- 18JN integrated worker commit `f358f595`: `MatchRecoveryTests` now covers snapshot timing battle damage assignment `requiredAssignments[]` item missing/null `sourceObjectId`, `damage` and `legalTargetObjectIds`. The actual stable diagnostics locked include `damage is required` and `legal target object id list is required`.
- 18JO integrated worker commit `c8e78f28`: `LayerEngineTimestampDependencyTests` now covers battlefield static-aura participant-leaves-field metadata parity across P1/P2 snapshots, proving the removed defender does not leak through target/participant dependency metadata.

## Validation

- Focused new tests: `4/4`.
- Touched class filter `GameHubJoinTests|MatchRecoveryTests|LayerEngineTimestampDependencyTests`: `1438/1438`.
- Broader adjacent server filter `GameHubJoinTests|MatchRecoveryTests|LayerEngineTimestampDependencyTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests|OrnnFriendlyEquipmentStaticPowerTests`: `5071/5071`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7251/7251`.
- Mechanical checks after docs sync: `git diff --check`, `git diff --cached --check`, anchored conflict-marker scan and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
