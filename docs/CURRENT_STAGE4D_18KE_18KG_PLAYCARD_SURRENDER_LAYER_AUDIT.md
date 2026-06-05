# Stage 4D-18KE/18KF/18KG PlayCard Surrender Layer Audit

Date: 2026-06-06 00:54 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18KE integrated worker commit `f5ef000f`: `ConformanceFixtureRunnerTests` now covers `PLAY_CARD` duplicate `clientIntentId` raw-payload behavior through `MatchSession.SubmitAsync`. Exact same raw payload replays the cached accepted stack-play result without journal growth or state/tick/event/stack/prompt/snapshot drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without mutation.
- 18KF integrated worker commit `31abf524`: `GameHubJoinTests` now covers official GameHub `SURRENDER` duplicate `clientIntentId` raw-payload behavior. Exact same raw payload replays the accepted match-finished group output without journal growth or event/snapshot/prompt drift; changed raw payload with the same intent returns `CLIENT_INTENT_CONFLICT` without group/caller mutation.
- 18KG integrated amended worker commit `0e5cba05`: `LayerEngineTimestampDependencyTests` now covers battlefield static-aura source-order and other-battlefield exclusion pressure together. Authoritative continuous effects and both P1/P2 snapshots retain public-field source order while excluding other-battlefield object/unit ids from target, participant and dependency metadata.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureRunnerTests|GameHubJoinTests|LayerEngineTimestampDependencyTests`: `3198/3198`.
- Broader adjacent server filter `ConformanceFixtureShapeTests|ConformanceFixtureRunnerTests|MatchRecoveryTests|OfficialOpeningTests|GameHubJoinTests|PostgresMatchRecoveryStoreSmokeTests|LayerEngineTimestampDependencyTests|BattleDamageAssignmentLifecycleTests|OrnnFriendlyEquipmentStaticPowerTests`: `5250/5250`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7269/7269`.
- Mechanical checks after code integration: `git diff --cached --check`, `git diff --check`, anchored conflict-marker scan and matrix JSON parse passed.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
