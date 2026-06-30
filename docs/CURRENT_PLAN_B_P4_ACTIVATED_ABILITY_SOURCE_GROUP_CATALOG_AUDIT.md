# Plan B P4 Activated Ability Source Group Catalog Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the ability-id switch inside `P4ActivatedAbilityCatalog.SourceCardNosForAbility`.

P4 activated ability definitions still keep their existing ability ids, effect kinds, costs, prompt surfaces, and stack resolution paths. The changed surface is only source-card group identity for alternate / promo official rows.

## Authority

Official card data:

- `data/official/card-catalog.zh-CN.json` rows `UNL-030/219` / `UNL-030a/219` Vi.
- `data/official/card-catalog.zh-CN.json` rows `SFD·088/221` / `SFD·088a/221` Renata Glasc.
- `data/official/card-catalog.zh-CN.json` rows `SFD·050/221` / `SFD·050a/221` Azir.
- `data/official/card-catalog.zh-CN.json` rows `SFD·082/221` / `SFD·082a/221` / `SFD·082b/221·P` Ezreal.
- `data/official/card-catalog.zh-CN.json` rows `UNL-022/219` / `UNL-022a/219` Jhin.
- `data/official/card-catalog.zh-CN.json` rows `UNL-087/219` / `UNL-087a/219` Blue Sentinel.

These rows are official alternate / promo source rows for already implemented P4 representative abilities. Reminder text and incomplete region metadata are not runtime identity.

## Implementation

- `P4ActivatedAbilityCatalog.SourceCardNosForAbility` now reads a lazy source-card map built through shared `OfficialCardSourceIdentityGroups`.
- The map groups source rows by normalized official rules identity: category, name, subtitle, colors, hero, tags, reminder-stripped rules text, mana cost, return energy, power, and group limit.
- The grouping deliberately ignores print-only / variant metadata such as extend type, rarity, product, image, and region. `UNL-087a/219` lacks the base row region value, so region cannot be part of the runtime source key.
- Legend action source groups now reuse the same helper, keeping P4 and legend-action source identity on one official-catalog path.
- If one official rules-identity group contains multiple P4 runtime definition source cards, the map falls back to one-card groups for those definitions. This keeps distinct Sigil and Gold token ability ids from being merged.
- Existing `IsSourceCardNoForAbility(...)` and `IsSourceCardNoForAbilityId(...)` call sites are unchanged; prompt legality, command revalidation, recovery diagnostics, and stack payloads continue through the shared catalog method.

## Validation

- Red focused guard: `ActivatedAbilitySourceIdentityGuardTests.P4ActivatedAbilitySourceCardGroupsDoNotUseAbilityIdSwitches` failed before implementation because `SourceCardNosForAbility` still contained `string.Equals(definition.AbilityId, ...)` branches.
- Focused gate: `ActivatedAbilitySourceIdentityGuardTests` passed `11/11`.
- Adjacent / hidden-info gate: `ActivatedAbility|ResourceSkill|P4Activate|PaymentEngineCoverageAuditTests|MatchRecovery|CardCatalogBaselineTests` passed `3570/3570`.
- Backend full conformance passed `9048/9048` after the shared-helper follow-up.

## Holdbacks

This does not migrate the full P4 activated ability definition table into BehaviorSpec, does not broaden any activated ability effect, does not change target legality or payment semantics, and does not close full P4, P1, or READY.
