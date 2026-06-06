# Stage 4D-18UW/18UX/18UY/18UZ Stale Raw Cache Audit

Date: 2026-06-07

Status: accepted into `main` as a server test-breadth bundle. Project remains **NOT READY**.

## Scope

A_MAIN dispatched four parallel worker worktrees and accepted the resulting test-only commits:

- 18UW `codex/stage4d-18uw-hunttheweak-stale-cache`: source `314fb5b6` cherry-picked as `2cdcf793`, touching `tests/Riftbound.ConformanceTests/HuntTheWeakDestroyGuardTests.cs`.
- 18UX `codex/stage4d-18ux-vengeance-stale-cache`: source `ea094fda` cherry-picked as `110cdcd0`, touching `tests/Riftbound.ConformanceTests/VengeanceDestroyGuardTests.cs`.
- 18UY `codex/stage4d-18uy-spiritfire-stale-cache`: source `6b2908ae` cherry-picked as `d940b9bb`, touching `tests/Riftbound.ConformanceTests/SpiritFireDestroyGuardTests.cs`.
- 18UZ `codex/stage4d-18uz-zenithblade-stale-cache`: source `fc43dd76` cherry-picked as `8a42eea1`, touching `tests/Riftbound.ConformanceTests/ZenithBladeStunGuardTests.cs`.

Runtime changed: no. This batch adds conformance coverage only.

## Coverage Added

Each accepted slice proves the same rejected stale prompt-scoped raw `PLAY_CARD` cache contract after the first accepted command enters stack priority:

- The first stale replay with a new `clientIntentId` is rejected with `PROMPT_EXPIRED`, has no events, and records exactly one rejected journal entry.
- An exact duplicate replay with the same rejected `clientIntentId` and identical raw command returns the cached rejection without journal growth.
- A changed raw command for the same rejected `clientIntentId` returns `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, hand/base/battlefield/graveyard/session projection or journal drift as applicable.
- The persisted accepted/rejected raw commands remain prompt-scoped through `promptId` and `snapshotTick`.

The covered command surfaces are Hunt the Weak `PLAY_CARD`, Vengeance `PLAY_CARD`, Spirit Fire `PLAY_CARD`, and Zenith Blade `PLAY_CARD`.

## Validation

- Focused changed tests: `43/43`.
- First adjacent server filter: `596/596`.
- Broader adjacent server filter: `5222/5222`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7463/7463`.
- Mechanical checks passed: `git diff --check`, range review from `1287435c..HEAD`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-07 00:41 CST.

## Remaining Open

This narrows stale raw rejected-cache semantics for four additional destroy/stun `PLAY_CARD` guard surfaces only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
