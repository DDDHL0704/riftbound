# Stage 4D-18JY/18JZ/18KA Battle Recovery Ornn Audit

Date: 2026-06-06 00:09 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18JY integrated worker commit `453f5608`: `BattleDamageAssignmentLifecycleTests` now covers `ASSIGN_COMBAT_DAMAGE` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same raw payload replays the cached accepted result without journal growth or state/tick/event/zone/prompt drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18JZ integrated worker commit `048391b6`: `MatchRecoveryTests` now covers spectator replay-frame timing battle damage assignment `requiredAssignments[]` item null fields. Null `sourceObjectId`, `damage` and `legalTargetObjectIds` emit stable item-field diagnostics.
- 18KA integrated cumulative worker commits `99dc8db3` and `87a16376`: `OrnnFriendlyEquipmentStaticPowerTests` now covers static-aura exclusion metadata across player views. Ignored hand, face-down, dirty-controller, non-equipment, enemy equipment and rune objects do not leak through authoritative participant/dependency metadata or P1/P2 snapshot timing views, both when no friendly public equipment qualifies and when one valid friendly public equipment remains.

## Validation

- Focused new tests: `4/4`.
- Touched class filter `BattleDamageAssignmentLifecycleTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests`: `1346/1346`.
- Broader adjacent server filter `BattleDamageAssignmentLifecycleTests|MatchRecoveryTests|OrnnFriendlyEquipmentStaticPowerTests|GameHubJoinTests|OfficialOpeningTests|PostgresMatchRecoveryStoreSmokeTests|ConformanceFixtureRunnerTests|LayerEngineTimestampDependencyTests`: `5126/5126`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7263/7263`.
- Mechanical checks after code integration: `git diff --check` passed before docs sync.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
