# Plan B Legend Identity Source Group Catalog Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes full card-number source arrays from `LegendIdentityCatalog`.

Legend identity ids, event fallback card numbers, trigger windows, targets, and effects remain unchanged. The changed surface is only source-card group identity for official alternate / reprint / spiritforged legend rows.

## Authority

Official card data:

- `data/official/card-catalog.zh-CN.json` legend rows for Ahri, Lucian, Master Yi, Draven, Garen, Lux, Annie, Jinx, Rumble, Volibear, Fiora, Sett, Vi, Vex, Renata Glasc, Rek'Sai, Ivern, LeBlanc, Rengar, Leona, Sivir, and Jhin.
- Existing `LegendActionSourceIdentityGuardTests.CoreLegendIdentitySourceRowsUseSharedCatalog` locks exact membership and negative cross-source examples for the accepted identity groups.

## Implementation

- `LegendIdentityCatalog` now stores identity ids as representative source card numbers instead of full source-card arrays.
- `SourceCardNosForIdentity(...)` expands representatives through shared `OfficialCardSourceIdentityGroups`.
- `PowerfulUnitRuneLegendIdentityId` keeps two representatives because it intentionally covers two official rules identities: Volibear powerful-unit play and Fiora unit-becomes-powerful.
- `PrimarySourceCardNoForIdentity(...)` returns the first configured representative source card number, preserving existing event fallback card numbers.
- P4 activated abilities, legend actions, and legend identities now share the same official source identity grouping helper.

## Validation

- Red focused guard: `LegendIdentityCatalogSourceGroupsDoNotHardcodeCardNumberArrays` failed before implementation because `LegendIdentityCatalog` still stored `IReadOnlyDictionary<string, IReadOnlyList<string>> SourceCardNosByIdentityId`.
- Focused gate: `LegendActionSourceIdentityGuardTests` passed `13/13`.
- Adjacent / hidden-info gate: `LegendActionSourceIdentity|LegendIdentity|LegendAction|LegendAct|Rengar|Leona|Sivir|Jhin|Ahri|Lucian|MasterYi|Draven|Garen|Lux|Sett|ViLegend|Vex|Renata|Reksai|Ivern|Leblanc|Rumble|Jinx|Volibear|Fiora|Annie|PowerfulUnit|CardCatalogBaselineTests|MatchRecovery` passed `2753/2753`.
- Backend full conformance passed `9049/9049`.

## Holdbacks

This does not migrate legend identity effects into BehaviorSpec, does not broaden any legend identity behavior, does not change payment or target legality, and does not close full legend identity / legend action breadth, P1, or READY.
