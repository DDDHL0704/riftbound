# Stage 4D-18VM/18VN/18VO/18VP Trigger Layer Stale Raw Cache Audit

Date: 2026-06-07

Owner: A_MAIN

Status: accepted into main. Project remains **NOT READY**.

## Scope

This batch integrated four parallel worktree slices:

- 18VM: Lux paid-cost `PLAY_CARD` prompt-scoped raw stale replay/cache conflict guard in `tests/Riftbound.ConformanceTests/LuxHighCostPaidCostTriggerTests.cs`.
- 18VN: Treasure Hunter `MOVE_UNIT` prompt-scoped raw stale replay/cache conflict guard in `tests/Riftbound.ConformanceTests/TreasureHunterMoveTriggerTests.cs`.
- 18VO: real trigger queue `ORDER_TRIGGERS` prompt-scoped raw stale replay/cache conflict guard in `tests/Riftbound.ConformanceTests/RealTriggerQueueTests.cs`.
- 18VP: mixed object static-aura plus battlefield static-aura snapshot/dependency determinism in `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`.

Runtime changed: no. This is server test coverage only.

## Source Commits

- 18VM worker source `7353ed48`, cherry-picked to main as `7fb83eb6`.
- 18VN worker source `f65cccfce08ba2f03c5a54cf77544e107999ed74`, cherry-picked to main as `c9d688f4`.
- 18VP worker source `a9c9c391ab12af0df0690f7b9b5845c8e4be9858`, cherry-picked to main as `9f97a976`.
- A_MAIN integration fix `159ab18c`.
- 18VO worker source `a9929a2485b2ab031596b5697ef7f70a8e80c6d6`, cherry-picked to main as `7add019b`.

## Integration Notes

A_MAIN narrowed two worker assertions during validation:

- Treasure Hunter rejected stale raw replay now asserts authoritative state, zones, object locations, gold token ids, session prompts/snapshots and journal behavior without requiring the rejected result prompt dictionary to match the accepted result byte-for-byte.
- LayerEngine mixed public-field source order now asserts the actual mixed-state public field order: the battlefield sources follow Ornn and public equipment, so their source orders are `3` and `5`.

## Validation

Passed on main:

- Focused Lux test filter: `4/4`.
- Focused Treasure Hunter test filter: `9/9`.
- Focused LayerEngine test filter after A_MAIN assertion fix: `24/24`.
- Focused RealTriggerQueue test filter: `59/59`.
- Focused changed-test bundle: `96/96`.
- Adjacent/broader server filter `Lux|TreasureHunter|RealTriggerQueue|LayerEngine|OrnnFriendlyEquipmentStaticPowerTests|ConformanceFixtureRunnerTests|GameHubJoinTests|MatchRecoveryTests|OfficialOpeningTests`: `5262/5262`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7479/7479`.
- `git diff --check db884c60 HEAD`.
- `git diff --check`.
- Anchored conflict-marker scan over `docs`, `src` and `tests` returned no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`.

## Remaining Open Work

This narrows trigger, move, order-trigger and LayerEngine dependency test breadth only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness status remain open.
