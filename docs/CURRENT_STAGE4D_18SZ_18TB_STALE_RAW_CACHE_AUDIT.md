# Stage 4D-18SZ/18TA/18TB Stale Raw Cache Audit

Date: 2026-06-06 19:23 CST

Owner: `A_MAIN`

Project status: **NOT READY**

## Scope

This checkpoint integrates three parallel server-test slices:

- `18SZ`: `ConformanceFixtureShapeTests.OrderTriggersPromptStampRejectsStaleEnvelopeWithoutChangingState`
- `18TA`: `GameHubJoinTests.SubmitIntentPayCostWindowUsesPromptStampAndClosesRuntimeSlice`
- `18TB`: `OfficialOpeningTests.OfficialFirstReadyBothDecksPromptReplayAfterFinalReadyRejectsWithoutMutation`

Runtime changed: no. This is test coverage only.

## Integrated Commits

- `18SZ` source `4ae55e7e` cherry-picked as `90434644`: covers `ORDER_TRIGGERS` stale prompt envelope rejected journal/cache behavior in shape tests.
- `18TA` source `af31adb0` cherry-picked as `2f25ca4a`: covers GameHub `PAY_COST` stale prompt rejected journal/cache/conflict behavior at the protocol boundary.
- `18TB` source `05eee1b9` cherry-picked as `73824d5a`: covers official first `READY` prompt replay after final ready starts mulligan.
- A_MAIN follow-up `df7ee495`: aligns the shape `ORDER_TRIGGERS` conflict assertion with the `SubmitAsync` contract, which returns a rejected `ResolutionResult` for `CLIENT_INTENT_CONFLICT`.

## Coverage Added

- First uncached stale prompt-scoped raw rejection records one rejected journal entry with raw command, state, prompt and snapshot hashes.
- Exact duplicate stale raw submissions with the same rejected `clientIntentId` replay from rejected cache without journal growth.
- Changed raw payloads with the same rejected `clientIntentId` return `CLIENT_INTENT_CONFLICT` without state, prompt, snapshot, broadcast or journal drift as applicable.
- The existing valid-path assertions remain in place for matching `ORDER_TRIGGERS`, valid GameHub `PAY_COST`, and official opening mulligan prompt progression.

## Validation

- Focused changed tests: `3/3`
- Touched class filter: `922/922`
- Broader adjacent server filter: `5438/5438`
- Backend full via tracked `Riftbound.slnx`: `7409/7409`
- Mechanical checks: `git diff --check`, `git diff ba4c2205..HEAD --check`, anchored conflict-marker scan over `docs`/`tests`/`src`, and matrix JSON parse all passed before docs sync.
- DOC_MATRIX_CURRENT was clean at `17bde0c3` when checked from A_MAIN.

## Remaining Open

This narrows rejected stale raw cache semantics for `ORDER_TRIGGERS`, GameHub `PAY_COST`, and official first `READY` replay paths only. Broader P0/P1, command/recovery/random determinism, remaining recovery payload breadth, full LayerEngine breadth, frontend build, Chrome smoke, formal E2E, real DB-backed Postgres smoke, `fullOfficial`, and final status remain open.

Project remains **NOT READY**.
