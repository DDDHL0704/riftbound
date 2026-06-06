# Stage 4D-18VA/18VB/18VC/18VD Stale Raw Cache Audit

Date: 2026-06-07

Status: accepted into `main` as a server test-breadth bundle. Project remains **NOT READY**.

## Scope

A_MAIN dispatched four parallel worker worktrees and accepted the resulting test-only commits:

- 18VA `codex/stage4d-18va-ridethewind-stale-cache`: source `c7001cd2` cherry-picked as `c0348016`, touching `tests/Riftbound.ConformanceTests/RideTheWindMoveGuardTests.cs`.
- 18VB `codex/stage4d-18vb-reflections-stale-cache`: source `5aa0f6cf` cherry-picked as `a56c2326`, touching `tests/Riftbound.ConformanceTests/ReflectionsSwapGuardTests.cs`.
- 18VC `codex/stage4d-18vc-switcheroo-stale-cache`: source `7183fd1a` cherry-picked as `10a3d795`, touching `tests/Riftbound.ConformanceTests/SwitcherooGuardTests.cs`; A_MAIN added integration fix `6755d118` for the local `RecordingMatchJournal` test stub.
- 18VD `codex/stage4d-18vd-secretart-mercy-stale-cache`: source `4a057a1b` cherry-picked as `9b26c26e`, touching `tests/Riftbound.ConformanceTests/SecretArtMercyBoonGuardTests.cs`.

Runtime changed: no. This batch adds conformance coverage only.

## Coverage Added

Each accepted slice proves the same rejected stale prompt-scoped raw `PLAY_CARD` cache contract after the first accepted command enters stack priority:

- The first stale replay with a new `clientIntentId` is rejected with `PROMPT_EXPIRED`, has no events, and records exactly one rejected journal entry.
- An exact duplicate replay with the same rejected `clientIntentId` and identical raw command returns the cached rejection without journal growth.
- A changed raw command for the same rejected `clientIntentId` returns `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, stack, hand/base/battlefield/graveyard/session projection or journal drift as applicable.
- The persisted accepted/rejected raw commands remain prompt-scoped through `promptId` and `snapshotTick`.

The covered command surfaces are Ride the Wind `PLAY_CARD`, Reflections `PLAY_CARD`, Switcheroo `PLAY_CARD`, and Secret Art Mercy `PLAY_CARD`.

## Validation

- Focused changed tests: `47/47`.
- First adjacent server filter: `1100/1100`.
- Broader adjacent server filter: `5226/5226`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7467/7467`.
- Mechanical checks passed: `git diff --check`, range review from `0099fe0e..HEAD`, anchored conflict-marker scan, and `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN on 2026-06-07 01:01 CST.

## Remaining Open

This narrows stale raw rejected-cache semantics for four additional move/swap/boon `PLAY_CARD` guard surfaces only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
