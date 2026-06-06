# Stage 4D-18SQ/18SR/18SS Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18SQ/18SR/18SS parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18SQ: official `MULLIGAN` after the second player's mulligan window starts.
- 18SR: session `RECYCLE_RUNE` after the rune moves to the rune deck.
- 18SS: ordinary `PAY_COST` after the payment window closes, across mana, generic-power and typed-power cost shapes.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `dfc3072b` was cherry-picked into main as `66706bda`.
- Worker source `c559ee30` was cherry-picked into main as `cc96ab1b`.
- Worker source `dc53d67f` was cherry-picked into main as `4264aec7`.

## Test Coverage

- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs` extends `OfficialMulliganStalePromptReplayAfterSecondPlayerWindowStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` extends `RecycleRuneStalePromptReplayAfterRuneMovesToRuneDeckRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs` extends `PendingPayCostPromptScopedOrdinaryReplayAfterWindowClosesRejectsWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, session projection or journal drift.

## Validation

- Focused changed tests: `5/5`.
- Touched class filter: `3747/3747`.
- Broader adjacent server filter: `4435/4435`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed before docs sync: `git diff --check`, `git diff 9d729b31..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for `MULLIGAN`, `RECYCLE_RUNE` and ordinary `PAY_COST` prompt paths only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
