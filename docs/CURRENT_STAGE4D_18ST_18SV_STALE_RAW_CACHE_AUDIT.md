# Stage 4D-18ST/18SU/18SV Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18ST/18SU/18SV parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18ST: session `HIDE_CARD` after the card has moved to base.
- 18SU: official active-player first `MULLIGAN` after the first turn has started.
- 18SV: natural `ASSIGN_COMBAT_DAMAGE` stale prompt envelope rejection.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `8c5f433d` was cherry-picked into main as `ee4c1fad`.
- Worker source `f6b6b0e3` was cherry-picked into main as `f9bc382f`.
- Worker source `0554a2de` was cherry-picked into main as `5698554c`.
- A_MAIN added integration fix `81c8e4f3` to align the official first-mulligan stale rejection prompt/snapshot baseline with the authoritative post-accepted projection.

## Test Coverage

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` extends `HideCardStalePromptReplayAfterCardMovesToBaseRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs` extends `OfficialFirstMulliganStalePromptReplayAfterFirstTurnStartsRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs` extends `NaturalAssignCombatDamageRejectsWrongOrStaleCommandsWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, session projection, journal or RNG drift as applicable.

## Validation

- Focused changed tests: `3/3`.
- Touched class filter: `3700/3700`.
- Broader adjacent server filter: `4435/4435`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed before docs sync: `git diff --check`, `git diff 50bcc898..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for `HIDE_CARD`, official active-player first `MULLIGAN` and natural `ASSIGN_COMBAT_DAMAGE` stale prompt envelope paths only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
