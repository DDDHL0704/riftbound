# Stage 4D-18VE/18VF/18VG/18VH Stale Raw Cache Audit

Date: 2026-06-07

Status: accepted into `main` as a server test-breadth bundle. Project remains **NOT READY**.

## Scope

A_MAIN dispatched four parallel worker worktrees and accepted the resulting test-only commits:

- 18VE `codex/stage4d-18ve-overcharged-stale-cache`: source `a9038807` cherry-picked as `c60f410b`, touching `tests/Riftbound.ConformanceTests/OverchargedEnergyGuardTests.cs`.
- 18VF `codex/stage4d-18vf-edge-play-stale-cache`: source `a57530d3` cherry-picked as `e66ced76`, touching `tests/Riftbound.ConformanceTests/EdgeOfNightAssembleGuardTests.cs`.
- 18VG `codex/stage4d-18vg-ezreal-stale-cache`: source `0359b82d` cherry-picked as `261a6a7c`, touching `tests/Riftbound.ConformanceTests/EzrealCombatDamageTextPlayUnitGuardTests.cs`.
- 18VH `codex/stage4d-18vh-reksai-attack-stale-cache`: source `eb28e791` cherry-picked as `56ca7a3a`, touching `tests/Riftbound.ConformanceTests/ReksaiAttackRevealPlayUnitGuardTests.cs`.

Runtime changed: no. This batch adds conformance coverage only.

## Coverage Added

Each accepted slice proves the same rejected stale prompt-scoped raw `PLAY_CARD` cache contract after the first accepted command enters stack priority:

- The first stale replay with a new `clientIntentId` is rejected with `PROMPT_EXPIRED`, has no events, and records exactly one rejected journal entry.
- An exact duplicate replay with the same rejected `clientIntentId` and identical raw command returns the cached rejection without journal growth.
- A changed raw command for the same rejected `clientIntentId` returns `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, zones, object locations, session projections or journal drift as applicable.
- The persisted accepted/rejected raw commands remain prompt-scoped through `promptId` and `snapshotTick`.

The covered command surfaces are Overcharged Energy `PLAY_CARD`, Edge of Night direct `PLAY_CARD`, Ezreal combat-damage-text unit `PLAY_CARD`, and RekSai attack-reveal unit `PLAY_CARD`.

## Validation

- Focused changed tests: `52/52`.
- First adjacent server filter: `1100/1100`.
- Broader adjacent server filter: `5230/5230`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7471/7471`.
- Mechanical checks passed: `git diff --check`, range review from `21949d6e..HEAD`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-07 01:19 CST.

## Remaining Open

This narrows stale raw rejected-cache semantics for four additional spell/equipment/unit `PLAY_CARD` guard surfaces only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
