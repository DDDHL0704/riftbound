# Stage 4D 18PI-18PK GameHub Mapper Official Audit

Date: 2026-06-06 11:02 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18PI added `GameHubJoinTests.RevealCardAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PJ added `ConformanceFixtureShapeTests.GameCommandMapperRevealCardUsesCommandFieldsOverVisibleSourceMetadata`.
- 18PK added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshRevealCardAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `REVEAL_CARD` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `REVEAL_CARD` uses current command fields for `sourceObjectId`, `cardNo`, `targetObjectIds`, `mode`, `optionalCosts` and `destination`; drops unreadable current array entries under the non-strict array contract; and does not backfill malformed, missing or alias-only command fields from visible prompt metadata aliases.
- Official session coverage now proves a fresh `REVEAL_CARD` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18PI worker source `ed326a06ee07c55f0c9bb4bdd8f1c636e4b98d36`, cherry-picked to main as `8b0cb3f6`.
- 18PJ worker source `3c375495209a2cb8d4ffe659cb5f9ad8e6bcc912`, cherry-picked to main as `8e99c807`.
- 18PK worker source `f19f1fb44fb81b641b05bcc843ec5a5994e32574`, cherry-picked to main as `ab9ea658`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `896/896`.
- Broader adjacent server filter: `5456/5456`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7366/7366`.
- `git diff --check`: passed.
- `git diff 5639d3cd..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 11:02 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `REVEAL_CARD` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
