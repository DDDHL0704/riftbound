# Stage 4D-18UK/18UL/18UM/18UN Stale Raw Cache Audit

Date: 2026-06-06

Status: accepted into `main` as a server test-breadth bundle. Project remains **NOT READY**.

## Scope

A_MAIN dispatched four parallel worker worktrees and accepted the resulting test-only commits:

- 18UK `codex/stage4d-18uk-vex-stale-cache`: source `21cc9cc5` cherry-picked and corrected by A_MAIN as `4f5bb661`, touching `tests/Riftbound.ConformanceTests/VexSpellshieldGuardTests.cs`.
- 18UL `codex/stage4d-18ul-zhonyas-stale-cache`: source `07cb5ebe` cherry-picked as `74e9689d`, touching `tests/Riftbound.ConformanceTests/ZhonyasHourglassGuardTests.cs`.
- 18UM `codex/stage4d-18um-sfur-song-stale-cache`: source `821b5acb` cherry-picked as `9222052a`, touching `tests/Riftbound.ConformanceTests/SfurSongGuardTests.cs`.
- 18UN `codex/stage4d-18un-time-gate-stale-cache`: source `c2f6826b` cherry-picked as `a8003c82`, touching `tests/Riftbound.ConformanceTests/TimeGateGuardTests.cs`.

Runtime changed: no. This batch adds conformance coverage only.

## Coverage Added

Each accepted slice proves the same rejected stale prompt-scoped raw `PLAY_CARD` cache contract after the first accepted command enters stack priority:

- The first stale replay with a new `clientIntentId` is rejected with `PROMPT_EXPIRED`, has no events, and records exactly one rejected journal entry.
- An exact duplicate replay with the same rejected `clientIntentId` and identical raw command returns the cached rejection without journal growth.
- A changed raw command for the same rejected `clientIntentId` returns `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, hand/base/equipment or journal drift.
- The persisted accepted/rejected raw commands remain prompt-scoped through `promptId` and `snapshotTick`.

The covered command surfaces are Vex spellshield unit `PLAY_CARD`, Zhonyas Hourglass equipment `PLAY_CARD`, Sfur Song equipment `PLAY_CARD`, and Time Gate equipment `PLAY_CARD`.

## Validation

- Focused changed tests: `28/28`.
- First adjacent server filter: `865/865`.
- Broader adjacent server filter: `5210/5210`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7451/7451`.
- Mechanical checks passed: `git diff --check`, range review from `ca63a87b..HEAD`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-06 23:37 CST.

## Remaining Open

This narrows stale raw rejected-cache semantics for four additional unit/equipment `PLAY_CARD` guard surfaces only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
