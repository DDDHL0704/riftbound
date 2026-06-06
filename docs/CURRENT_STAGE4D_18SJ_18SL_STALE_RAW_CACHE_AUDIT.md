# Stage 4D-18SJ/18SK/18SL Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18SJ/18SK/18SL parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18SJ: official `SUBMIT_DECK` after the opponent is already ready.
- 18SK: session `PLAY_CARD` after stack priority starts.
- 18SL: natural `ASSIGN_COMBAT_DAMAGE` stale prompt replay after the next contest starts.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `5b6018d7` was cherry-picked into main as `120e23bc`.
- Worker source `63c6fcb5` was cherry-picked into main as `d8c8fb69`.
- Worker source `ae9de30b` was cherry-picked into main as `7abab274`.

## Test Coverage

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs` extends `OfficialSubmitDeckAfterOpponentReadyStalePromptReplayRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` extends `PlayCardStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs` extends `NaturalAssignCombatDamageStalePromptReplayAfterNextContestStartsRejectsWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, session projection or journal drift.

## Validation

- Focused changed tests: `3/3`.
- Touched class filter: `3700/3700`.
- Broader adjacent server filter: `4283/4283`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed before docs sync: `git diff --check`, `git diff 9a05ecf7..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for official opponent-ready `SUBMIT_DECK`, session `PLAY_CARD` and natural battle-damage `ASSIGN_COMBAT_DAMAGE` stale prompt paths only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
