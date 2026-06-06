# Stage 4D-18RE/18RF/18RG Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18RE/18RF/18RG parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18RE: `MOVE_UNIT` after the move starts spell duel priority.
- 18RF: `DECLARE_BATTLE` after the next spell duel starts.
- 18RG: Honeyfruit level-six `ACTIVATE_ABILITY` after the temporary payment ledger exists.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `eda8f1ac` was cherry-picked into main as `4f3836ea`.
- Worker source `0b5c6881` was cherry-picked into main as `29b668d4`.
- Worker source `981ec6fc` was cherry-picked into main as `33b77e91`.

## Test Coverage

- `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` extends `MoveUnitStalePromptReplayAfterSpellDuelStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs` extends `DeclareBattleStalePromptReplayAfterNextSpellDuelStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/HoneyfruitResourceSkillTests.cs` extends `HoneyfruitLevelSixResourceStalePromptReplayAfterTemporaryLedgerRejectsWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Validation

- Focused changed tests: `3/3`.
- Touched class filter: `51/51`.
- Broader adjacent server filter: `4265/4265`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed: `git diff --check`, `git diff 38c8fc18..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for MOVE_UNIT, DECLARE_BATTLE and Honeyfruit ACTIVATE_ABILITY only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
