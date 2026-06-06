# Stage 4D 18ON-18OP Mapper / LayerEngine / Official Audit

Date: 2026-06-06

Owner: A_MAIN

Project status: **NOT READY**

## Accepted Slice

A_MAIN integrated three parallel worktree slices on `main`:

- 18ON: `ConformanceFixtureShapeTests.GameCommandMapperOrderTriggersPrefersCurrentOrderedTriggerIdsOverLegacyTriggerIds`
- 18OO: `LayerEngineTimestampDependencyTests.LayerEngineBattlefieldStaticAuraSourceOrderDependencyMetadataUsesObjectLocationToIgnoreStaleBattlefieldZoneSourceAcrossPlayerViews`
- 18OP: `OfficialOpeningTests.OfficialFirstTurnSurrenderFreshTapRuneAfterMatchFinishedThrowsStableErrorWithoutMutation`

Worker source commits:

- 18ON source `2bd868ce`, cherry-picked on main as `621d8460`
- 18OO source `d4ae122f` plus runtime fix `1e94eb0c`, cherry-picked on main as `3dae8659` and `b5d950d1`
- 18OP source `ff9af845`, cherry-picked on main as `57e05f5a`

Runtime changed: yes, `src/Riftbound.Engine/MatchSession.cs` now respects explicit object locations when deriving battlefield static-aura source candidates, participant fallback, public-field dependency ids and public-field source ordering. Missing `ObjectLocations` entries still fall back to existing zone membership behavior.

## Locked Behavior

Mapper coverage now proves `ORDER_TRIGGERS` raw payloads prefer current `orderedTriggerIds` over legacy `triggerIds` when both are present, including trimming current ids into both typed lists, and that malformed current `orderedTriggerIds` remain authoritative instead of silently falling back to valid legacy data.

LayerEngine coverage now proves battlefield static-aura source/dependency metadata obeys explicit `ObjectLocations`: if a stale source id remains in `PlayerZones.Battlefields` but its object location says `GRAVEYARD`, the stale source is ignored in authoritative effects and P1/P2 snapshot dependency/participant metadata while the valid later battlefield aura remains.

Official opening coverage now proves after first-turn `SURRENDER` finishes a match, a fresh `TapRuneCommand` with a new client intent throws stable `MatchFinished`, does not create a journal entry, does not grow the journal, preserves both player snapshots/prompts, and still matches the official match-finished prompt queue audit with no events.

## Validation

Validation passed on main:

- Focused new tests: `3/3`
- Touched class filter: `738/738`
- Broader adjacent server filter: `5419/5419`
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7345/7345`
- `git diff --check`: passed
- `git diff 09694b41..HEAD --check`: passed
- Anchored conflict-marker scan over `docs`, `tests`, and `src`: no matches
- `jq empty docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`: passed

## Remaining Open

This narrows mapper precedence, LayerEngine object-location/static-aura metadata, and official finished-session guards only. P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial` and final readiness remain open.
