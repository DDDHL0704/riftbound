# Stage 4D 18PR-18PT GameHub Mapper Official Audit

Date: 2026-06-06 12:02 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18PR added `GameHubJoinTests.RecycleRuneAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PS added `ConformanceFixtureShapeTests.GameCommandMapperRecycleRuneUsesCommandFieldsOverVisibleSourceMetadata`.
- 18PT added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshActivateAbilityAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `RECYCLE_RUNE` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, secret, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `RECYCLE_RUNE` uses the current `sourceObjectId` command field and does not backfill malformed, missing or alias-only command fields from visible prompt metadata aliases.
- Official session coverage now proves a fresh `ACTIVATE_ABILITY` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18PR worker source `158ac6bf083198bea0210babdb11c7428f3bd2a3`, cherry-picked to main as `a254e4f3`.
- 18PS worker source `c1b4fcb6cfd3710d053b2eb49cf93f5d22d5e8d8`, cherry-picked to main as `c9803999`.
- 18PT worker source `f4bcbb9324bbdd215e539179b0d2f04cb2908df0`, cherry-picked to main as `5476e548`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `905/905`.
- Broader adjacent server filter: `5465/5465`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7375/7375`.
- `git diff --check`: passed.
- `git diff 2ee95102..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 12:02 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `RECYCLE_RUNE` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
