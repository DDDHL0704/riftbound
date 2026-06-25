# Plan B / B5 Armament Equipment Source Tag Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: implemented equipment entries listed in `docs/CURRENT_PLAN_B_B5_ARMAMENT_EQUIPMENT_SOURCE_TAG_AUDIT.md` carry official `Tag = 武装`.
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog text and tags remain the local rule authority inputs for this representative slice.

## Conformance Evidence

- `OfficialArmamentEquipmentRegistryDefinitionsCarryWeaponSourceTag` verifies every implemented official equipment card with catalog `Tag = 武装` and `PlaysSourceToBaseAsEquipment = true` has `SourceEquipmentTags` containing `武装`.
- `ArmamentPlayTrackingDoesNotUseCoreCardNumberAllowList` verifies `CoreRuleEngine` no longer contains `IsOfficialArmamentEquipmentCardNo`.
- `P4EquipmentKeywordProfilesMapOfficialTextToRegistryTags` now verifies `SFD·033/221` 多兰之盾 carries `HasWeapon = true` and remains recognized as deferred equipment breadth after the source tag is exposed.
- `P4EquipmentKeywordProfilesKeepExistingNoAttachFixturesGreen` keeps the existing no-attach equipment fixture route green after the public equipment objects include official `武装` tags.
- `EdgeOfNightPlayCardWithNoTargetsUsesStackAndResolvesToBase` verifies `SFD·139/221` 夜之锋刃 resolves to base with `CARD_TYPE:EQUIPMENT` and `武装`.

## Validation

- Focused registry/source guard representatives: `2/2` passing.
- Adjacent `EquipmentKeyword|Armament|Assemble|EquipmentState` representatives: `181/181` passing.
- `FullGameEndToEnd`: `15/15` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8525/8525` passing.

## Residual Risk

- This slice only moves armament source recognition from Core card-number branching to registry source tags. It does not prove full official equipment attach lifecycle, optional assemble prompt breadth, all weapon static modifiers, or complete equipment control / zone movement.
- Remaining `Is*CardNo(...)` helper count is `35` total / `32` in `CoreRuleEngine`; legend and other non-armament helpers remain follow-up work.
