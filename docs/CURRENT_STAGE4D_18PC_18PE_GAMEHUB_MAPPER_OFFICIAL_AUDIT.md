# Stage 4D 18PC-18PE GameHub Mapper Official Audit

Date: 2026-06-06 10:26 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18PC added `GameHubJoinTests.LegendActAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PD added `ConformanceFixtureShapeTests.GameCommandMapperLegendActUsesCommandFieldsOverVisibleAbilityMetadata`.
- 18PE added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshRecycleRuneAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `LEGEND_ACT` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `LEGEND_ACT` uses current command fields for `sourceObjectId`, `abilityId`, `targetObjectIds` and `optionalCosts`; drops unreadable current array entries under the non-strict array contract; and does not backfill malformed, missing or alias-only command fields from visible prompt metadata aliases.
- Official session coverage now proves a fresh `RECYCLE_RUNE` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18PC worker source `db4dd7ac609625486cbc1c2be882100e8bad4c67`, cherry-picked to main as `74fadc7d`.
- 18PD worker source `c74319eaa4e0231c47bb3e3cc736375e64a323ba`, cherry-picked to main as `b2c035ac`.
- 18PE worker source `2e2ae68e412f3d8727e43965502114ee4a35e8b4`, cherry-picked to main as `4eb857a2`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `890/890`.
- Broader adjacent server filter: `5450/5450`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7360/7360`.
- `git diff --check`: passed.
- `git diff 931e2591..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 10:26 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `LEGEND_ACT` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
