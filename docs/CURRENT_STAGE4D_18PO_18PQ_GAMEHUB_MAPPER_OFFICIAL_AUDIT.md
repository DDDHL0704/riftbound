# Stage 4D 18PO-18PQ GameHub Mapper Official Audit

Date: 2026-06-06 11:44 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18PO added `GameHubJoinTests.TapRuneAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PP added `ConformanceFixtureShapeTests.GameCommandMapperTapRuneUsesCommandFieldsOverVisibleSourceMetadata`.
- 18PQ added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshMoveUnitAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `TAP_RUNE` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, secret, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `TAP_RUNE` uses the current `sourceObjectId` command field and does not backfill malformed, missing or alias-only command fields from visible prompt metadata aliases.
- Official session coverage now proves a fresh `MOVE_UNIT` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18PO worker source `58b75d5677669f72b9735fb13836766e98a7f6ef`, cherry-picked to main as `11dd1e19`.
- 18PP worker source `b0aa3b08db8c76c0d6f2bb57101d3756fb3d28c0`, cherry-picked to main as `0b491921`.
- 18PQ worker source `7cb614ce82a90c08ee9bd1fc8803c456e0ae07be`, cherry-picked to main as `f41e9f23`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `902/902`.
- Broader adjacent server filter: `5462/5462`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7372/7372`.
- `git diff --check`: passed.
- `git diff 6afd48e1..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 11:44 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `TAP_RUNE` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
