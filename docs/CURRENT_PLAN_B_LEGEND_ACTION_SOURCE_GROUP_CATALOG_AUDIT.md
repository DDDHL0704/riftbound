# Plan B Legend Action Source Group Catalog Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes full card-number source arrays from `LegendActionAbilityCatalog`.

Legend action ability ids, costs, targets, prompt surfaces, command validation, and effects remain unchanged. The changed surface is only source-card group identity for official alternate / reprint / spiritforged legend rows.

## Authority

Official card data:

- `data/official/card-catalog.zh-CN.json` legend rows for Yasuo, Lee Sin, Poppy, Viktor, Miss Fortune, Kha'Zix, Pyke, Jax, Darius, Diana, Kai'Sa, Ornn, Ezreal, Irelia, Teemo, Azir, and Lillia.
- Existing `LegendActionSourceIdentityGuardTests` rows prove the accepted source-card groups and negative cross-source examples for those ability ids.

## Implementation

- Added shared `OfficialCardSourceIdentityGroups`.
- The helper builds source-card groups from normalized official catalog rules identity: category, name, subtitle, colors, hero, tags, reminder-stripped rules text, mana cost, return energy, power, and group limit.
- The helper ignores print-only / variant metadata and region metadata, which is incomplete on some official alternate rows.
- `LegendActionAbilityCatalog` now stores only `abilityId -> representative source cardNo`.
- `SourceCardNosForAbility(...)` derives the full official source group through `OfficialCardSourceIdentityGroups`.
- P4 activated ability source groups also reuse the helper, keeping one normalized source identity path for both catalogs.

## Validation

- Red focused guard: `LegendActionAbilityCatalogSourceGroupsDoNotHardcodeCardNumberArrays` failed before implementation because `LegendActionAbilityCatalog` still stored `IReadOnlyDictionary<string, IReadOnlyList<string>> SourceCardNosByAbilityId`.
- Focused gate: `ActivatedAbilitySourceIdentityGuardTests|LegendActionSourceIdentityGuardTests` passed `23/23`.
- Adjacent / hidden-info gate: `LegendAction|LegendAct|ActivatedAbility|ResourceSkill|P4Activate|PaymentEngineCoverageAuditTests|MatchRecovery|CardCatalogBaselineTests` passed `3671/3671`.
- Backend full conformance passed `9048/9048`.

## Holdbacks

This does not migrate full legend action effect definitions into BehaviorSpec, does not broaden any legend action effect, does not change payment or target legality, and does not close full legend-action breadth, P1, or READY.
