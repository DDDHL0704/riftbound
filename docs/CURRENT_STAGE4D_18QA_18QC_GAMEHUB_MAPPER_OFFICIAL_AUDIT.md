# Stage 4D 18QA-18QC GameHub Mapper Official Audit

Date: 2026-06-06 12:56 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18QA added `GameHubJoinTests.OrderTriggersAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18QB added `ConformanceFixtureShapeTests.GameCommandMapperAssignCombatDamageUsesCommandFieldsOverVisibleDamageMetadata`.
- 18QC added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshPayCostAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `ORDER_TRIGGERS` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, secret, internal, debug, current and legacy trigger ordering, and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `ASSIGN_COMBAT_DAMAGE` uses only current `battleId`, `battlefieldId` and `assignments` command fields and does not backfill malformed, missing or alias-only command fields from visible battle/damage metadata aliases such as assignment choices, required assignments, legal targets, participants and damage pools.
- Official session coverage now proves a fresh `PAY_COST` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18QA worker source `7c2f1b7484b943c3ea52dab4fd5232647f7aec03`, cherry-picked to main as `8fad0913`.
- 18QB worker source `d1d74c759d8ec249efb93f620916ace918e763d3`, cherry-picked to main as `57374a9c`.
- 18QC worker source `2b972190ddbdc8acf0506214520c425f18876f4d`, cherry-picked to main as `12f93153`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `914/914`.
- Broader adjacent server filter: `5474/5474`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7384/7384`.
- `git diff --check`: passed.
- `git diff e4f259d9..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 12:56 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `ASSIGN_COMBAT_DAMAGE` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
