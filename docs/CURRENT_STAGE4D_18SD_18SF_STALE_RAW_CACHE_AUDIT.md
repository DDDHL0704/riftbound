# Stage 4D-18SD/18SE/18SF Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18SD/18SE/18SF parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18SD: spell-duel second-player `PASS_FOCUS` after the next spell duel starts.
- 18SE: Legend resource bridge `LEGEND_ACT` after resource gain.
- 18SF: `END_TURN` after the next player starts.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `8f9914d` was cherry-picked into main as `9414383a`.
- Worker source `9dd0e2f` was cherry-picked into main as `65e09dee`.
- Worker source `e9dd741` was cherry-picked into main as `7cc934e7`.

## Test Coverage

- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs` extends `PassFocusSecondPlayerClosingSpellDuelStalePromptReplayRecordsRejectedJournalWithoutMutation`.
- `tests/Riftbound.ConformanceTests/LegendResourceBridgeVerifierTests.cs` extends `LegendResourceBridgeStalePromptReplayAfterResourceGainRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` extends `EndTurnStalePromptReplayAfterNextPlayerStartsRejectsWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Validation

- Focused changed tests: `11/11`.
- Touched class filter: `3175/3175`.
- Broader adjacent server filter: `4425/4425`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed: `git diff --check`, `git diff b8514a71..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for PASS_FOCUS, Legend resource bridge LEGEND_ACT and END_TURN only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
