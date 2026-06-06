# Stage 4D 18PL-18PN GameHub Mapper Official Audit

Date: 2026-06-06 11:23 CST

Owner: A_MAIN

Status: accepted on main. Project remains **NOT READY**.

## Scope

- 18PL added `GameHubJoinTests.AssembleEquipmentAfterFinishedRedactsSentinelPayloadAndDoesNotBroadcastOrMutate`.
- 18PM added `ConformanceFixtureShapeTests.GameCommandMapperAssembleEquipmentUsesCommandFieldsOverVisibleEquipmentMetadata`.
- 18PN added `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshAssembleEquipmentAfterMatchFinishedThrowsStableErrorWithoutMutation`.

Runtime changed: no. This bundle is server test coverage only.

## Coverage Locked

- GameHub now proves raw `ASSEMBLE_EQUIPMENT` after a finished match returns stable `MatchFinished`, redacts client intent, sentinel, raw, internal, debug and command strings from the user-visible message, emits no caller/group events, snapshots, prompts or group errors, does not grow the journal, and preserves finished snapshots.
- Mapper coverage now proves `ASSEMBLE_EQUIPMENT` uses current command fields for `sourceObjectId`, `targetObjectId` and `optionalCosts`; drops unreadable current optional-cost entries under the non-strict array contract; and does not backfill malformed, missing or alias-only command fields from visible prompt metadata aliases.
- Official session coverage now proves a fresh `ASSEMBLE_EQUIPMENT` submitted after first-turn surrender has finished the match throws stable `MatchFinished`, records no new journal entry, preserves prompts and snapshots, and still satisfies the finished-match prompt queue audit.

## Source Commits

- 18PL worker source `e7d7f8518dcfccea52993a14d96d934f37f4d684`, cherry-picked to main as `41d22434`.
- 18PM worker source `680e2925cc72189c60a3fdce097f112d2243f6c8`, cherry-picked to main as `317d3739`.
- 18PN worker source `f96d6b4da66d9f84e41be4b9f52acb09e4cc71e3`, cherry-picked to main as `5ac1694e`.

## Validation

All validation was run on main after cherry-pick:

- Focused new tests: `3/3`.
- Touched class filter: `899/899`.
- Broader adjacent server filter: `5459/5459`.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7369/7369`.
- `git diff --check`: passed.
- `git diff 423bc2f4..HEAD --check`: passed before docs sync.
- Anchored conflict-marker scan over `docs`, `tests` and `src`: no matches.
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed.

DOC_MATRIX_CURRENT remained clean at `17bde0c3` when observed from A_MAIN at 2026-06-06 11:23 CST.

## Remaining Open

This narrows GameHub finished-session redaction, `ASSEMBLE_EQUIPMENT` mapper boundary, and official finished-session fresh-command coverage only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
