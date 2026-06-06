# Stage 4D-18SM/18SN/18SO/18SP Ready Stale Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18SM/18SN/18SO/18SP parallel server bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across four server surfaces:

- 18SM: official `READY` after mulligan prompts start.
- 18SN: session `TAP_RUNE` after the rune is exhausted.
- 18SO: session `ORDER_TRIGGERS` after stack priority starts.
- 18SP: Undercover Agent `CHOOSE_HAND_CARDS` after the hand-choice window closes.

Runtime changed: yes. 18SM exposed and fixed a `MatchSession.ReadyAsync` gap where stale prompt `READY` rejections were cached but not written to the match journal on the first uncached rejection. The fix keeps duplicate rejected replay from cache without journal growth and keeps changed raw payloads on the same `clientIntentId` as `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot or journal drift.

## Integrated Commits

- Worker source `a82e4123` was cherry-picked into main as `4e3f5547`.
- Worker source `bbf20e3b` was cherry-picked into main as `b85e0d8d`.
- Worker source `7ca8b1f3` was cherry-picked into main as `0c5bf926`.
- Worker source `9e5ed204` was cherry-picked into main as `710d20f4`.
- Worker source `bb8b5ab5` was cherry-picked into main as `99367ce5`.
- A_MAIN added `405505d3` to align the Undercover Agent assertion with the xUnit analyzer.

## Test Coverage

- `src/Riftbound.Engine/MatchSession.cs` now records the first uncached stale prompt `READY` rejection in the match journal.
- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs` extends `OfficialReadyStalePromptReplayAfterMulliganStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` extends `TapRuneStalePromptReplayAfterRuneExhaustsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs` extends `OrderTriggersStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/UndercoverAgentTriggerTests.cs` extends `UndercoverAgentHandChoiceStalePromptReplayAfterWindowClosesRejectsWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, session projection or journal drift.

## Validation

- Focused changed tests: `4/4`.
- Touched class filter: `3806/3806`.
- Broader adjacent server filter: `4435/4435`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed before checkpoint: `git diff --check`, `git diff f451eb9d..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for `READY`, `TAP_RUNE`, `ORDER_TRIGGERS` and `CHOOSE_HAND_CARDS`, and fixes the `READY` stale prompt rejected-journal gap only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
