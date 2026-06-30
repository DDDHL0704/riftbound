# Plan B Legend Action Source Group Catalog Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data is the only authority for this slice:

- `data/official/card-catalog.zh-CN.json` rows for the accepted Yasuo, Lee Sin, Poppy, Viktor, Miss Fortune, Kha'Zix, Pyke, Jax, Darius, Diana, Kai'Sa, Ornn, Ezreal, Irelia, Teemo, Azir, and Lillia legend-action source groups.

Existing tests already lock exact membership and negative cross-source examples. This slice changes the catalog source of those memberships, not their rule meaning.

## Engine Evidence

Before this slice:

- `LegendActionAbilityCatalog.SourceCardNosForAbility` read full source-card arrays from an engine-owned ability-id dictionary.
- Adding a new official alternate / spiritforged row required editing that card-number array.

After this slice:

- `LegendActionAbilityCatalog` stores only one representative source card number per ability id.
- The full source group is derived from `OfficialCardSourceIdentityGroups`, which reads official catalog rows and groups by normalized rules identity.
- P4 activated abilities and legend actions now share the same official source identity grouping helper.
- Existing command handling, prompt metadata, action validation, recovery diagnostics, and hidden-information boundaries are unchanged.

## Test Evidence

- `LegendActionSourceIdentityGuardTests.LegendActionAbilityCatalogSourceGroupsDoNotHardcodeCardNumberArrays` blocks reintroducing full card-number arrays in `LegendActionAbilityCatalog`.
- Existing `LegendActionSourceIdentityGuardTests` membership tests continue to prove every accepted legend-action source group and negative cross-source example.
- `ActivatedAbilitySourceIdentityGuardTests` also passed after P4 was moved onto the shared helper, proving the helper preserves P4 alternate / promo source groups and distinct Sigil / Gold-token runtime boundaries.
- Focused gate passed `23/23`.
- Adjacent / hidden-info gate passed `3671/3671`.
- Backend full conformance passed `9048/9048`.

## Non-Claims

This evidence does not claim full legend-action data modeling, complete legend-action official breadth, complete payment windows, complete target-stack timing, P1 closure, or READY.
