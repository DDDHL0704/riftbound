# Stage 4D-18US/18UT/18UU/18UV Stale Raw Cache Audit

Date: 2026-06-07

Status: accepted into `main` as a server test-breadth bundle. Project remains **NOT READY**.

## Scope

A_MAIN dispatched four parallel worker worktrees and accepted the resulting test-only commits:

- 18US `codex/stage4d-18us-akshan-stale-cache`: source `e2b82e88` cherry-picked as `89d729f5`, touching `tests/Riftbound.ConformanceTests/AkshanGuardTests.cs`.
- 18UT `codex/stage4d-18ut-berserk-impulse-stale-cache`: source `5100ad7d` cherry-picked as `bf34a3c5`, touching `tests/Riftbound.ConformanceTests/BerserkImpulseGuardTests.cs`.
- 18UU `codex/stage4d-18uu-hunt-stale-cache`: source `c2c4d10e` cherry-picked as `b2b7787b`, touching `tests/Riftbound.ConformanceTests/HuntReadyGuardTests.cs`.
- 18UV `codex/stage4d-18uv-reksai-no-optional-stale-cache`: source `f2254371` cherry-picked as `01ca8222`, touching `tests/Riftbound.ConformanceTests/ReksaiNoOptionalHasteOverwhelmGuardTests.cs`.

A_MAIN also added integration fix `ee2eecac` after focused validation showed Berserk Impulse's target-required prompt exposes `PLAY_CARD` as a disabled top-level candidate while still accepting the command path. The fix changes only the prompt-shape assertion in that test.

Runtime changed: no. This batch adds conformance coverage only.

## Coverage Added

Each accepted slice proves the same rejected stale prompt-scoped raw `PLAY_CARD` cache contract after the first accepted command enters stack priority:

- The first stale replay with a new `clientIntentId` is rejected with `PROMPT_EXPIRED`, has no events, and records exactly one rejected journal entry.
- An exact duplicate replay with the same rejected `clientIntentId` and identical raw command returns the cached rejection without journal growth.
- A changed raw command for the same rejected `clientIntentId` returns `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, hand/base/deck/battlefield/exhaustion/session projection or journal drift as applicable.
- The persisted accepted/rejected raw commands remain prompt-scoped through `promptId` and `snapshotTick`.

The covered command surfaces are Akshan `PLAY_CARD`, Berserk Impulse `PLAY_CARD`, Hunt `PLAY_CARD`, and RekSai no-optional haste/overwhelm `PLAY_CARD`.

## Validation

- Focused changed tests: `62/62`.
- First adjacent server filter: `430/430`.
- Broader adjacent server filter: `5218/5218`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7459/7459`.
- Mechanical checks passed: `git diff --check`, range review from `f9bdd86b..HEAD`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-07 00:20 CST.

## Remaining Open

This narrows stale raw rejected-cache semantics for four additional `PLAY_CARD` guard surfaces only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
