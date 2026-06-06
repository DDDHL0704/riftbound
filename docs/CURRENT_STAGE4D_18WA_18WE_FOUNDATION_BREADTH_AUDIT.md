# Stage 4D-18WA/18WB/18WC/18WD/18WE Foundation Breadth Audit

Date: 2026-06-07

Status: accepted into A_MAIN. Project remains **NOT READY**.

## Scope

A_MAIN accepted five parallel foundation-test shards covering canonical JSON serialization, development UI CORS origin boundaries, behavior-spec catalog referential integrity, LayerEngine battlefield static-aura participant ordering, and prompt/snapshot shape redaction for waiting players.

Runtime changed: no. Test coverage only.

## Accepted Commits

- 18WB API CORS: worker commit `56cc8115dbe583c323d734a751b0286d38234aa1` accepted into main as `d7eac361`, adding loopback fallback boundary, IPv6 loopback, non-http, high-port, non-loopback and configured-origin case-insensitive coverage in `tests/Riftbound.ConformanceTests/ApiDevUiCorsPolicyTests.cs`.
- 18WA CanonicalJson: worker commit `956198146b9f2e98194b952304405c02d98e87e7` accepted into main as `5fb11654`, adding `tests/Riftbound.ConformanceTests/CanonicalJsonTests.cs` to prove camelCase names, compact output and relaxed escaping for non-ASCII plus HTML-sensitive characters.
- 18WC CardCatalog baseline: worker commit `957d8ac9067484b8f01ad55b993bfa199b4e75f9` accepted into main as `36edb0ef`, adding `ImplementedBehaviorSpecsReferenceOfficialCardsAndStayWithinFunctionalUnits` in `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs`.
- 18WE ConformanceFixture shape: worker commit `e03b94a9a93f46a17cd17beabe8a2b1fbe53b750` accepted into main as `7d438ac6`, adding `BattleDeclarationPromptDoesNotLeakSelectionShapeToWaitingPlayer` in `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`.
- 18WD LayerEngine: worker commit `bf3213307f0ce6f35c534cc4b640091dd16f9ce0` accepted into main as `aec98a42`, adding `LayerEngineBattlefieldStaticAuraParticipantOrderIsCanonicalWhenPublicBattlefieldZoneOrderDiffers` in `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`.

## Coordination Note

This batch used five simultaneous worker worktrees from `eedce03e`, with disjoint write scopes. A_MAIN accepted each worker result serially on main, ran each focused test after cherry-pick, then ran the combined and adjacent filters. No worker wrote to the default main worktree during this batch.

## Validation

- 18WB focused API CORS filter on main: `10/10`.
- 18WA focused CanonicalJson filter on main: `4/4`.
- 18WC focused CardCatalog baseline filter on main: `76/76`.
- 18WE focused ConformanceFixtureShape filter on main: `142/142`.
- 18WD focused LayerEngineTimestampDependency filter on main: `26/26`.
- Combined changed-test filter: `258/258` for `CanonicalJson|ApiDevUiCorsPolicyTests|CardCatalogBaselineTests|LayerEngineTimestampDependencyTests|ConformanceFixtureShapeTests`.
- Adjacent/broader server filter: `6089/6089` for canonical JSON, API CORS, catalog baseline, LayerEngine, conformance fixture shape/runner, GameHub, official opening, recovery and payment audit coverage.
- Backend full via tracked `Riftbound.slnx` under the current no-DB environment: `7505/7505`.
- `git diff --check eedce03e HEAD` passed before docs sync; conflict-marker scan over `docs src tests` had no matches; matrix JSON parse passed.

## Remaining Risk

This narrows serialization, dev CORS, catalog referential integrity, LayerEngine battlefield static-aura ordering, and prompt WAIT-shape coverage only. It does not close P0/P1, broader command/recovery/random determinism, remaining recovered/spectator/authoritative nested payload breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, DOC_MATRIX future scope or final readiness.
