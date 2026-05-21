# Stage 4D-04U LayerEngine Source Order Audit

Date: 2026-05-21

Conclusion: **IMPLEMENTED AND A-VALIDATED / WRITELOCK CLOSED / PROJECT NOT READY**

## Accepted Behavior

4D-04U is accepted for this bounded backend slice because A verified:

1. `timing.continuousEffects[]` now exposes additive server-authored `sourceOrder` metadata for public-field continuous-effect source objects.
2. Same-target / same-layer static aura representatives use public field source order before falling back to effect id ordering.
3. The source-order fixture deliberately reverses lexical object id order and field order, proving the ordering is not accidental string sorting.
4. Existing sequence metadata remains stable and still starts at 1 for the visible continuous-effect list.
5. Hidden face-down objects are excluded from source-order assignment.

## Files

Changed runtime/test files:

- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/LayerEngineTimestampDependencyTests.cs`

New evidence docs:

- `docs/CURRENT_STAGE4D_04U_LAYERENGINE_SOURCE_ORDER_EVIDENCE.md`
- `docs/CURRENT_STAGE4D_04U_LAYERENGINE_SOURCE_ORDER_AUDIT.md`

## Validation

Validation is recorded in `docs/CURRENT_STAGE4D_04U_LAYERENGINE_SOURCE_ORDER_EVIDENCE.md`:

- 04U focused LayerEngine tests: 6/6 passed.
- LayerEngine / ContinuousEffect / Ornn / BattlefieldStatic adjacent filter: 57/57 passed.
- Backend full test: 5242/5242 passed.

## Boundaries

This slice does not change frontend behavior, card matrix JSON, `PaymentEngineCoverageAuditTests.cs`, official catalog data, Chrome/browser scripts, formal 18-step E2E scripts, protocol core field removals/renames, `fullOfficial`, READY, or `riftbound-dotnet.sln`.

Project remains **NOT READY**.
