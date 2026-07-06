# Current Plan B Target Protection Spec Audit

更新时间：2026-07-06

## Scope

This slice implements source-unit text that prevents enemy spells and skills from choosing that unit as a target. It covers official source-unit text parsed into `BehaviorSpec.StaticAbilities`:

- `UNL-147/219` / `UNL-147a/219` / `UNL-238/219` 纳什男爵: `我无法被敌方法术和技能选作目标。`
- `SFD·105/221` 沙墟啸匪: `敌方法术和技能无法将我选作目标。`
- `UNL-059/219` / `UNL-059a/219` 易: `{{等级16>}} 我无法被敌方法术和技能选作目标。`

## Implementation

- Added `StaticAbilityKinds.SourceUnitEnemySpellSkillTargetProtection` to the shared BehaviorSpec contract.
- `RuleTextParsers.StaticAbilityParser` now parses both Chinese source-unit target-protection wordings and preserves optional `RequiredPlayerExperience`.
- `TargetProtectionRules` evaluates protection generically from `CardStaticAbilitySpecRules.TryGetStaticAbility(...)`; it has no card-number allow-list.
- `CoreRuleEngine` rejects illegal protected targets for `PLAY_CARD` spell/skill behavior and implemented activated skill target paths.
- `MatchSession` filters those same illegal targets out of server-authored play-card and activated-skill prompts.

## Validation

- Red focused before implementation:
  - Catalog parser returned no `SOURCE_UNIT_ENEMY_SPELL_SKILL_TARGET_PROTECTION`.
  - `OGS·003/024` 焚烧 was accepted targeting enemy `UNL-147/219` 纳什男爵.
  - `PLAY_CARD` prompt listed protected 纳什男爵 as a target.
- Green focused after implementation: `BehaviorSpecCatalogParsesSourceUnitEnemySpellSkillTargetProtectionStaticAbility|SourceUnitEnemySpellSkillTargetProtectionUsesGenericStaticAbilitySpec|EnemySpellSkillTargetProtectionTests` passed 7/7.
- Adjacent: `EnemySpellSkillTargetProtection|CardCatalogBaseline|TargetScopeGuard|Spellshield|Xerath|CrimsonRose|Shadow|MatchRecovery` passed 2595/2595.
- Backend full conformance passed 9177/9177.

## Rule Authority

- Official card text from `data/official/card-catalog.zh-CN.json`.
- `CORE-260330` p4-p8 rules 107-129, p14-p15 rules 142-143, and p39-p42 rules 355-356 remain the general play / stack / target legality authority for applying card text to submitted commands and server prompts.

## Not Closed

- Wildclaw Beastmaster's same-battlefield low-power friendly-unit target-protection aura is a different scoped aura family and remains open.
- Baron Nest creation and replacement entry destination remain open.
- Complete official-deck replay breadth for every target-protection card remains open; this slice uses focused official-card states plus adjacent hidden-info and target-scope regressions.
- Future skill families must continue routing through `TargetProtectionRules` when they add target-bearing skill implementations.
- Project remains NOT READY.
