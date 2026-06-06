# Stage 4D-18UG/18UH/18UI/18UJ Stale Raw Cache Audit

Date: 2026-06-06

Status: accepted into `main` as a server test-breadth bundle. Project remains **NOT READY**.

## Scope

A_MAIN dispatched four parallel worker worktrees and accepted the resulting test-only commits:

- 18UG `codex/stage4d-18ug-enemy-battlefield-unit-stale-cache`: source `3354dc90` cherry-picked as `a2dc85b5`, touching `tests/Riftbound.ConformanceTests/EnemyBattlefieldUnitTargetScopeGuardTests.cs`.
- 18UH `codex/stage4d-18uh-firestorm-stale-cache`: source `17836adc` cherry-picked as `f5dc6d5b`, touching `tests/Riftbound.ConformanceTests/FirestormEnemyBattlefieldDamageGuardTests.cs`.
- 18UI `codex/stage4d-18ui-hostile-takeover-stale-cache`: source `21f1f6f8` cherry-picked as `afed0a73`, touching `tests/Riftbound.ConformanceTests/HostileTakeoverGuardTests.cs`.
- 18UJ `codex/stage4d-18uj-sea-monster-hook-stale-cache`: source `d5468ea4` cherry-picked as `3f878e3f`, touching `tests/Riftbound.ConformanceTests/SeaMonsterHookGuardTests.cs`.

Runtime changed: no. This batch adds conformance coverage only.

## Coverage Added

Each accepted slice proves the same rejected stale prompt-scoped raw `PLAY_CARD` cache contract after the first accepted command enters stack priority:

- The first stale replay with a new `clientIntentId` is rejected with `PROMPT_EXPIRED`, has no events, and records exactly one rejected journal entry.
- An exact duplicate replay with the same rejected `clientIntentId` and identical raw command returns the cached rejection without journal growth.
- A changed raw command for the same rejected `clientIntentId` returns `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, zone, target/equipment/object or journal drift.
- The persisted accepted/rejected raw commands remain prompt-scoped through `promptId` and `snapshotTick`.

The covered command surfaces are Megashark Cannon enemy battlefield-unit target-scope `PLAY_CARD`, Firestorm no-target enemy battlefield-unit AOE `PLAY_CARD`, Hostile Takeover enemy battlefield-unit control `PLAY_CARD`, and Sea Monster Hook no-target equipment `PLAY_CARD`.

## Validation

- Focused changed tests: `43/43`.
- First adjacent server filter: `1119/1119`.
- Broader adjacent server filter: `5206/5206`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7447/7447`.
- Mechanical checks passed: `git diff --check`, range review from `755eb306..HEAD`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 23:16 CST.

## Remaining Open

This narrows stale raw rejected-cache semantics for four additional `PLAY_CARD` guard/equipment/damage/control surfaces only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
