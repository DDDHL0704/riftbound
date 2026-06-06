# Stage 4D 18PU-18PW GameHub Mapper Official Audit

Date: 2026-06-06 12:21 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18PU added `GameHubJoinTests.DeclareBattleAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PV added `ConformanceFixtureShapeTests.GameCommandMapperMoveUnitUsesCommandFieldsOverVisibleMovementMetadata`.
- 18PW added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshLegendActAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `DECLARE_BATTLE` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, secret, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `MOVE_UNIT` uses current `sourceObjectId`, `origin`, `destination` and `optionalCosts` command fields and does not backfill malformed, missing or alias-only command fields from visible movement metadata aliases.
- Official session coverage now proves a fresh `LEGEND_ACT` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18PU worker source `61d47e8e31621be2946c220651e8cc36481bb3fc`, cherry-picked to main as `cadcbe64`.
- 18PV worker source `00cabc24987e8335c64d407f319647ba29b27119`, cherry-picked to main as `bdc1ec07`.
- 18PW worker source `e484035fc89dae6dce7d3a0e6d722bd749bcfde1`, cherry-picked to main as `ba36e121`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `908/908`.
- Broader adjacent server filter: `5468/5468`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7378/7378`.
- `git diff --check`: passed.
- `git diff 6f57efa1..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 12:21 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `MOVE_UNIT` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
