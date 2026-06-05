# Stage 4D-18JS/18JT/18JU Hub Recovery Layer Audit

Date: 2026-06-05 23:34 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18JS integrated worker commit `0df4c7d3`: `GameHubJoinTests` now covers seeded `MOVE_UNIT` duplicate `clientIntentId` raw-payload behavior. Exact same raw payload replays stable output without journal growth; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation, snapshot drift or changed journal entries.
- 18JT integrated worker commit `aece1ce2`: `MatchRecoveryTests` now covers snapshot timing battle damage assignment `requiredAssignments[]` legal-target item value drift. Legal-target item entries with surrounding whitespace, empty values and duplicates emit stable diagnostics.
- 18JU integrated worker commit `595d98c2`: `LayerEngineTimestampDependencyTests` now strengthens battlefield static-aura source-order dependency metadata coverage by comparing authoritative dependency signatures against P1/P2 snapshot signatures.

## Validation

- Focused new/extended tests: `3/3`.
- Touched class filter `GameHubJoinTests|MatchRecoveryTests|LayerEngineTimestampDependencyTests`: `1442/1442`.
- Broader adjacent server filter `GameHubJoinTests|MatchRecoveryTests|LayerEngineTimestampDependencyTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests|OrnnFriendlyEquipmentStaticPowerTests`: `5075/5075`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7255/7255`.
- Mechanical checks after docs sync: `git diff --check`, `git diff --cached --check`, anchored conflict-marker scan and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
