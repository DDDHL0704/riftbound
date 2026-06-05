# Stage 4D-18KB/18KC/18KD Order Surrender Recovery Audit

Date: 2026-06-06 00:26 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18KB integrated worker commit `5cc8c9d5`: `ConformanceFixtureShapeTests` now covers `ORDER_TRIGGERS` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same raw payload replays the cached accepted trigger-ordering result without journal growth or state/tick/event/stack/prompt drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18KC integrated worker commit `279a4383`: `ConformanceFixtureRunnerTests` now covers `SURRENDER` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same raw payload replays the cached match-finished result without journal growth or state/tick/event/prompt drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18KD integrated worker commit `b21bc6fd`: `MatchRecoveryTests` now covers spectator replay-frame timing pending-hand-choice `legalObjectIds` payload shape drift. Current validator behavior requires spectator legal-object IDs to remain redacted, so non-redacted/non-list payloads emit the stable redaction diagnostic.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureShapeTests|ConformanceFixtureRunnerTests|MatchRecoveryTests`: `4449/4449`.
- Broader adjacent server filter `ConformanceFixtureShapeTests|ConformanceFixtureRunnerTests|MatchRecoveryTests|OfficialOpeningTests|GameHubJoinTests|PostgresMatchRecoveryStoreSmokeTests|LayerEngineTimestampDependencyTests|BattleDamageAssignmentLifecycleTests|OrnnFriendlyEquipmentStaticPowerTests`: `5247/5247`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7266/7266`.
- Mechanical checks after code integration: `git diff --check` passed before docs sync.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
