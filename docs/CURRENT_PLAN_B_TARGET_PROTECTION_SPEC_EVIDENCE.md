# Current Plan B Target Protection Spec Evidence

更新时间：2026-07-06

This evidence records the Plan B source-unit enemy spell/skill target-protection slice.

## Official Text

- `UNL-147/219` 纳什男爵 includes `我无法被敌方法术和技能选作目标。`
- `SFD·105/221` 沙墟啸匪 includes `敌方法术和技能无法将我选作目标。`
- `UNL-059/219` 易 includes the level-gated form `{{等级16>}} 我无法被敌方法术和技能选作目标。`

The parser maps these to `StaticAbilityKinds.SourceUnitEnemySpellSkillTargetProtection`, with `RequiredPlayerExperience=16` for the Yi level-gated text.

## Engine Evidence

- `TargetProtectionRules` reads `BehaviorSpec.StaticAbilities` through `CardStaticAbilitySpecRules.TryGetStaticAbility(...)` and the `IsSourceUnitEnemySpellSkillTargetProtectionAbility` shape predicate.
- `CoreRuleEngine.TryBuildPlayCardPlan` rejects protected enemy targets before payment, hand movement, stack creation, or event emission.
- `MatchSession` removes those targets from `PLAY_CARD` prompt target choices and `legalTargetSelections`.
- Implemented activated skill target paths for Xerath, Crimson Rose, and Shadow also call `TargetProtectionRules.IsLegalActivatedSkillTarget`.

## Regression Evidence

- `BehaviorSpecCatalogParsesSourceUnitEnemySpellSkillTargetProtectionStaticAbility` verifies Baron Nashor, Desert Plunderer, and level-gated Master Yi parsing.
- `EnemySpellCannotTargetSourceUnitWithEnemySpellSkillProtection` verifies enemy `OGS·003/024` 焚烧 is rejected when targeting protected enemy `UNL-147/219` 纳什男爵, with no mutation.
- `EnemySpellCannotTargetSourceUnitWithAlternativeProtectionWording` verifies the `SFD·105/221` 沙墟啸匪 wording routes through the same generic protection.
- `EnemySpellTargetProtectionHonorsRequiredExperience` verifies `UNL-059/219` 易 is targetable below level 16 and protected at level 16.
- `PlayCardPromptOmitsEnemySourceUnitWithEnemySpellSkillProtection` verifies server-authored prompt choices omit protected 纳什男爵 while retaining another legal battlefield unit.
- `SourceUnitEnemySpellSkillTargetProtectionUsesGenericStaticAbilitySpec` guards against a card-number allow-list in the target-protection implementation.

## Validation

- Focused red/green: 7/7 after implementation.
- Adjacent: 2595/2595.
- Backend full conformance: 9177/9177.

## Remaining Risk

This slice does not close Wildclaw Beastmaster's lower-power same-battlefield target-protection aura, Baron Nest creation / replacement entry, complete official-deck breadth for target protection, or any future target-bearing skill family that has not yet been implemented.
