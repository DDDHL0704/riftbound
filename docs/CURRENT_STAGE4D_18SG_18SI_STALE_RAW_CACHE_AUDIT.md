# Stage 4D-18SG/18SH/18SI Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18SG/18SH/18SI parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18SG: official `SUBMIT_DECK` after ready prompts start.
- 18SH: `SURRENDER` after the match has finished.
- 18SI: spell-duel first-contest `PASS_FOCUS` after the next contest starts.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `f8d9e427` was cherry-picked into main as `b0686707`.
- Worker source `22de35a8` was cherry-picked into main as `57c134e2`.
- Worker source `5b48df4c` was cherry-picked into main as `0ac6cced`.

## Test Coverage

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs` extends `SubmitDeckStalePromptReplayAfterReadyPromptStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` extends `SurrenderStalePromptReplayAfterMatchFinishedRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs` extends `SpellDuelFocusStalePromptReplayAfterNextContestStartsRecordsRejectedJournalWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Validation

- Focused changed tests: `3/3`.
- Touched class filter: `3671/3671`.
- Broader adjacent server filter: `4364/4364`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed: `git diff --check`, `git diff 6df74f1d..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for SUBMIT_DECK, SURRENDER and PASS_FOCUS only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
