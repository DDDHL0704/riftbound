# Stage 4D 18PF-18PH GameHub Mapper Official Audit

Date: 2026-06-06 10:45 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18PF added `GameHubJoinTests.HideCardAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PG added `ConformanceFixtureShapeTests.GameCommandMapperHideCardUsesCommandFieldsOverVisibleSourceMetadata`.
- 18PH added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshHideCardAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `HIDE_CARD` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `HIDE_CARD` uses current command fields for `sourceObjectId`, `cardNo`, `destination` and `optionalCosts`; drops unreadable current array entries under the non-strict array contract; and does not backfill malformed, missing or alias-only command fields from visible prompt metadata aliases.
- Official session coverage now proves a fresh `HIDE_CARD` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18PF worker source `fdc65ca2b957a38146059f94b849bc2987dc6e79`, cherry-picked to main as `11ca32f3`.
- 18PG worker source `b6eacd12644ffceaade8636e9104b95cec5a0faf`, cherry-picked to main as `4a02bc46`.
- 18PH worker source `5ff66a005a0eeb2f3a570dfb6ddebe270f9fe2df`, cherry-picked to main as `9113b118`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `893/893`.
- Broader adjacent server filter: `5453/5453`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7363/7363`.
- `git diff --check`: passed.
- `git diff c8e5261c..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 10:45 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `HIDE_CARD` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
