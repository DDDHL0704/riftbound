# Stage 4D 18OZ-18PB GameHub Mapper Official Audit

Date: 2026-06-06 10:09 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18OZ added `GameHubJoinTests.ActivateAbilityAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PA added `ConformanceFixtureShapeTests.GameCommandMapperActivateAbilityUsesCommandFieldsOverVisibleAbilityMetadata`.
- 18PB added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshPlayCardAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `ACTIVATE_ABILITY` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `ACTIVATE_ABILITY` uses current command fields for `sourceObjectId`, `abilityId`, `targetObjectIds` and `optionalCosts`; drops unreadable current array entries under the non-strict array contract; and does not backfill malformed, missing or alias-only command fields from visible prompt metadata aliases.
- Official session coverage now proves a fresh `PLAY_CARD` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18OZ worker source `45d9efc031b7b9185295a2109d504de5cb9accfd`, cherry-picked to main as `7a65f876`.
- 18PA worker source `09c486f7af11d6886fe5d4c57ba82293a298464a`, cherry-picked to main as `e4cc26e9`.
- 18PB worker source `8bb0555907b7d8c3eeeee45be34e6749c8ad13a6`, cherry-picked to main as `a14c1e95`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `887/887`.
- Broader adjacent server filter: `5447/5447`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7357/7357`.
- `git diff --check`: passed.
- `git diff 3899e705..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 10:09 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `ACTIVATE_ABILITY` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
