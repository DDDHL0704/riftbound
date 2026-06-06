# Stage 4D-18UO/18UP/18UQ/18UR Stale Raw Cache Audit

Date: 2026-06-06

Status: accepted into `main` as a server test-breadth bundle. Project remains **NOT READY**.

## Scope

A_MAIN dispatched four parallel worker worktrees and accepted the resulting test-only commits:

- 18UO `codex/stage4d-18uo-vex-alt-stale-cache`: source `21da6db2` cherry-picked as `1dde0e19`, touching `tests/Riftbound.ConformanceTests/VexAltSpellshieldGuardTests.cs`.
- 18UP `codex/stage4d-18up-draven-vanilla-stale-cache`: source `2d2d1ff6` cherry-picked as `99b99fbd`, touching `tests/Riftbound.ConformanceTests/DravenVanillaGuardTests.cs`.
- 18UQ `codex/stage4d-18uq-draven-keyword-stale-cache`: source `e54740f5` cherry-picked as `75e96be1`, touching `tests/Riftbound.ConformanceTests/DravenKeywordUnitGuardTests.cs`.
- 18UR `codex/stage4d-18ur-giant-arm-kato-stale-cache`: replacement source `424745b7` cherry-picked as `a0f895d4`, touching `tests/Riftbound.ConformanceTests/GiantArmKatoGuardTests.cs`.

A_MAIN also added integration fix `d9e1189a` after focused validation first hit xUnit2031 analyzer failures in two accepted slices. The fix changes only assertion shape from `Where(...).Single()` to `Assert.Single(..., predicate)`.

Runtime changed: no. This batch adds conformance coverage only. One worker cwd incident was contained by migrating the accidental patch to the intended 18UR worktree, closing the mistaken worker, and restoring main clean before accepting replacement output.

## Coverage Added

Each accepted slice proves the same rejected stale prompt-scoped raw `PLAY_CARD` cache contract after the first accepted command enters stack priority:

- The first stale replay with a new `clientIntentId` is rejected with `PROMPT_EXPIRED`, has no events, and records exactly one rejected journal entry.
- An exact duplicate replay with the same rejected `clientIntentId` and identical raw command returns the cached rejection without journal growth.
- A changed raw command for the same rejected `clientIntentId` returns `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, hand/base, target/session projection or journal drift as applicable.
- The persisted accepted/rejected raw commands remain prompt-scoped through `promptId` and `snapshotTick`.

The covered command surfaces are Vex alt spellshield `PLAY_CARD`, Draven vanilla `PLAY_CARD`, Draven keyword-unit `PLAY_CARD`, and Giant Arm Kato `PLAY_CARD`.

## Validation

- Focused changed tests: `31/31`.
- First adjacent server filter: `406/406`.
- Broader adjacent server filter: `5214/5214`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7455/7455`.
- Mechanical checks passed: `git diff --check`, range review from `79fc97af..HEAD`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 23:58 CST.

## Remaining Open

This narrows stale raw rejected-cache semantics for four additional unit `PLAY_CARD` guard surfaces only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
