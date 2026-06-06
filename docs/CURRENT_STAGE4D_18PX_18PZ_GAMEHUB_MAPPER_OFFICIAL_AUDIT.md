# Stage 4D 18PX-18PZ GameHub Mapper Official Audit

Date: 2026-06-06 12:38 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18PX added `GameHubJoinTests.PayCostAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PY added `ConformanceFixtureShapeTests.GameCommandMapperPlayCardUsesCommandFieldsOverVisibleCardMetadata`.
- 18PZ added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshDeclareBattleAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `PAY_COST` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, secret, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `PLAY_CARD` uses only current `sourceObjectId`, `cardNo`, `targetObjectIds`, `mode`, `optionalCosts` and `destination` command fields and does not backfill malformed, missing or alias-only command fields from visible card/source/target/cost/destination metadata aliases.
- Official session coverage now proves a fresh `DECLARE_BATTLE` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18PX worker source `f9277dda4a69a53607207886b4474ea8e944824b`, cherry-picked to main as `c7bf67df`.
- 18PY worker source `afad87c6c4d76086e99fb0226665fdd2c625513f`, cherry-picked to main as `955d55c8`.
- 18PZ worker source `666856476a469266e118fe08d45d683197270e32`, cherry-picked to main as `af1f00a9`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `911/911`.
- Broader adjacent server filter: `5471/5471`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7381/7381`.
- `git diff --check`: passed.
- `git diff 73be66b6..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 12:38 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `PLAY_CARD` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
