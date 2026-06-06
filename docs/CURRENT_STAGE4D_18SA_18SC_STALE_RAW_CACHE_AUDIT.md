# Stage 4D-18SA/18SB/18SC Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18SA/18SB/18SC parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18SA: task queue `PASS_PRIORITY` after the next stack item starts.
- 18SB: Dragon Soul Sage reaction resource `ACTIVATE_ABILITY` after mana gain.
- 18SC: trigger payment `PAY_COST` after payment closes and the next contest starts.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `0058587c` was cherry-picked into main as `b347e780`.
- Worker source `156bd649` was cherry-picked into main as `9852a60b`.
- Worker source `0ffc1604` was cherry-picked into main as `788e7183`.

## Test Coverage

- `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs` extends `StackPriorityStalePromptReplayAfterNextStackItemStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ReactionResourceSkillTests.cs` extends `DragonSoulSageReactionResourceStalePromptReplayAfterManaGainRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs` extends `BattlefieldConquerGoldTriggerPaymentStalePromptReplayAfterNextContestStartsRejectsWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Validation

- Focused changed tests: `3/3`.
- Touched class filter: `104/104`.
- Broader adjacent server filter: `4265/4265`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed: `git diff --check`, `git diff f6f0e86f..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for PASS_PRIORITY, Dragon Soul Sage ACTIVATE_ABILITY and trigger-payment PAY_COST only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
