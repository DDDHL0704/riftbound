# Plan B Legend Identity Source Group Catalog Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data is the only authority for this slice:

- `data/official/card-catalog.zh-CN.json` rows for the accepted Ahri, Lucian, Master Yi, Draven, Garen, Lux, Annie, Jinx, Rumble, Volibear, Fiora, Sett, Vi, Vex, Renata Glasc, Rek'Sai, Ivern, LeBlanc, Rengar, Leona, Sivir, and Jhin legend identity source groups.

Existing tests already lock exact membership, primary fallback card numbers, and negative cross-source examples. This slice changes the catalog source of those memberships, not their rule meaning.

## Engine Evidence

Before this slice:

- `LegendIdentityCatalog.SourceCardNosForIdentity` read full source-card arrays from an engine-owned identity-id dictionary.
- Adding a new official alternate / spiritforged row required editing that full card-number array.

After this slice:

- `LegendIdentityCatalog` stores one or more representative source card numbers per identity id.
- The full source group is derived from `OfficialCardSourceIdentityGroups`, which reads official catalog rows and groups by normalized rules identity.
- `PowerfulUnitRuneLegendIdentityId` keeps one Volibear representative and one Fiora representative because those are distinct official rules identities intentionally sharing the same runtime identity family.
- `PrimarySourceCardNoForIdentity` remains stable by using the configured first representative instead of the official-catalog group order.
- P4 activated abilities, legend actions, and legend identities now share the same official source identity grouping helper.

## Test Evidence

- `LegendActionSourceIdentityGuardTests.LegendIdentityCatalogSourceGroupsDoNotHardcodeCardNumberArrays` blocks reintroducing full source-card arrays in `LegendIdentityCatalog`.
- `LegendActionSourceIdentityGuardTests.CoreLegendIdentitySourceRowsUseSharedCatalog` continues to prove every accepted legend identity source group, primary fallback, and negative cross-source example.
- Focused gate passed `13/13`.
- Adjacent / hidden-info gate passed `2753/2753`.
- Backend full conformance passed `9049/9049`.

## Non-Claims

This evidence does not claim full legend identity data modeling, complete legend-action official breadth, complete payment windows, complete target-stack timing, P1 closure, or READY.
