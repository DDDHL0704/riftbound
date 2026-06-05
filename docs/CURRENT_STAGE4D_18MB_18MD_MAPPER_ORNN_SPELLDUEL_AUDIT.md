# Stage 4D-18MB/18MC/18MD Mapper Ornn SpellDuel Audit

Date: 2026-06-06 04:16 CST

Owner: A_MAIN

Status: accepted into main after A_MAIN review and validation. Project remains **NOT READY**.

## Scope

- 18MB integrated worker commit `2adb6e13`: `ConformanceFixtureShapeTests` now covers `GameCommandJsonMapper` non-strict `TextArray` behavior. `PLAY_CARD` `targetObjectIds` / `optionalCosts` and `DECLARE_BATTLE` `battlefieldTargetObjectIds` trim non-empty strings while dropping blank and unreadable non-string entries instead of converting the command payload to null.
- 18MC integrated worker commit `9f3e3aa7`: `OrnnFriendlyEquipmentStaticPowerTests` now covers dynamic Ornn friendly-equipment static-aura recompute after an equipment card resolves onto the public field while Ornn is already in play. Authoritative state and both P1/P2 snapshots retain matching source, target, participant dependency metadata, residual metadata and static-aura signatures.
- 18MD integrated worker commit `99e3b9b9`: `SpellDuelBattleStateMachineTests` now covers the closing `PASS_FOCUS` path for a second pass that closes the current spell duel, performs cleanup and advances to the next contested battlefield. Exact duplicate raw payloads replay the cached accepted result; changed raw payloads with the same `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, event, prompt, snapshot or journal drift.

## Validation

- Focused new tests: `3/3`.
- Touched class filter `ConformanceFixtureShapeTests|OrnnFriendlyEquipmentStaticPowerTests|SpellDuelBattleStateMachineTests`: `145/145`.
- Broader adjacent server filter `ConformanceFixtureRunnerTests|ConformanceFixtureShapeTests|PaymentEngineUnificationTests|UndercoverAgentTriggerTests|GameHubJoinTests|OfficialOpeningTests|MatchRecoveryTests|PostgresMatchRecoveryStoreSmokeTests|LayerEngineTimestampDependencyTests|OrnnFriendlyEquipmentStaticPowerTests|SpellDuelBattleStateMachineTests|BattleDamageAssignmentLifecycleTests`: `5382/5382`.
- Backend full via tracked `Riftbound.slnx` with `ConnectionStrings__Riftbound` unset: `7299/7299`.
- Mechanical checks after code integration: `git diff --cached --check` and `git diff --check` passed before docs sync. Post-doc mechanical checks are recorded in the checkpoint commit.

## Residual Risk

- This is test coverage only; it does not close broader P0/P1 readiness.
- Real DB-backed Postgres smoke remains open because no `ConnectionStrings__Riftbound` was configured.
- Frontend build, Chrome smoke, formal E2E, `fullOfficial`, remaining recovery payload breadth, full LayerEngine breadth and final readiness remain open.
