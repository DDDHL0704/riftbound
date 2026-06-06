# Stage 4D-18SW/18SX/18SY Stale Raw Cache Audit

Date: 2026-06-06

Owner: A_MAIN

Status: accepted into main after validation. Project remains **NOT READY**.

## Scope

This audit records the Stage 4D-18SW/18SX/18SY parallel server test bundle. The batch extends stale prompt-scoped raw rejected-intent cache coverage across three additional server surfaces:

- 18SW: session `REVEAL_CARD` base and reaction paths after the stale prompt window has expired.
- 18SX: shape-level `ASSIGN_COMBAT_DAMAGE` stale prompt envelope rejection.
- 18SY: official second-player final `MULLIGAN` after the first turn has started.

Runtime changed: no. This is server test coverage only.

## Integrated Commits

- Worker source `27aeb968` was cherry-picked into main as `f0ef3ff9`.
- Worker source `3f9a00ca` was cherry-picked into main as `5b3a38e1`.
- Worker source `55a4675c` was cherry-picked into main as `bf7a342e`.
- A_MAIN added integration fix `5b997b8c` to align the assign-shape stale-cache journal count assertions with xUnit analyzer requirements.

## Test Coverage

- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` extends `RevealCardBaseStalePromptReplayAfterCardFlipsFaceUpRejectsWithoutMutation` and `RevealCardReactionStalePromptReplayAfterCardMovesToStackRejectsWithoutMutation`.
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs` extends `AssignCombatDamagePromptStampRejectsStaleEnvelopeWithoutChangingState`.
- `tests/Riftbound.ConformanceTests/OfficialOpeningTests.cs` extends `OfficialFinalMulliganReplaysAfterFirstTurnStartsRejectWithoutMutation`.

Each surface now proves exact duplicate rejected raw submissions replay from the rejected-intent cache without additional journal growth, while changed raw payloads for the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, session projection, journal or RNG drift as applicable.

## Validation

- Focused changed tests: `4/4`.
- Touched class filter: `3796/3796`.
- Broader adjacent server filter: `5438/5438`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7409/7409`.
- Mechanical checks passed before docs sync: `git diff --check`, `git diff c760f439..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Work

This narrows rejected stale raw cache semantics for session `REVEAL_CARD`, `ASSIGN_COMBAT_DAMAGE` shape stale envelopes and official final `MULLIGAN` paths only. Broader P0/P1 closure, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
