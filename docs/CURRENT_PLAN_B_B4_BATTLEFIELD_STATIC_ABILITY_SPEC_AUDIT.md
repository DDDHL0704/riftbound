# Plan B / B4 Battlefield Static Ability Spec Audit

Date: 2026-06-28

Status: focused B4 battlefield static ability kind lookup generic-predicate route accepted; project remains **NOT READY**.

## Scope

These slices move implemented battlefield static abilities away from engine card-number branching:

- `OGN·276/298` / `OGN·276a/298` official text: `赢得游戏所需的分数+1。`
- `SFD·209/221` official text: `每名玩家在各自的第三回合开始前，无法从此处获得分数。`
- `OGN·295/298` official text: `单位无法从此处移动到基地。`
- `SFD·216/221` official text: `单位无法被打出到此处。`
- `SFD·211/221` official text: `如果此战场受你控制，则友方{{回响}}的费用减少{{1}}。`
- `SFD·213/221` official text: `如果此战场受你控制，则每回合打出的第一件友方装备的费用减少{{1}}，不包括指示物。`
- `UNL-213/219` official text: `此处的单位获得“{{横置}}：获得1经验。”`
- `OGN·296/298` official text: `以此处的单位作为目标的法术或技能，造成的伤害+1（每段伤害都+1）。`
- `OGN·278/298` / `OGN·278a/298` official text: `你可以选择在此处额外布置一张{{待命}}卡牌。`
- `UNL-206/219` official text: `如果此处的一名单位在战斗中被摧毁，其控制者可以选择支付{{A}}{{A}}{{A}}，以此改为移除其所受伤害、将其变为休眠状态、并将其召回。`
- `SFD·208/221` official text: `如果此战场受你控制，则所有友方传奇获得“{{横置}}：将你控制的一件武装贴附到你控制的一名单位上。”`
- `RuleTextParser` now parses those texts as `StaticAbilitySpec` with:
  - `Kind = BATTLEFIELD_WINNING_SCORE_INCREASE`, `Amount = 1`
  - `Kind = BATTLEFIELD_SCORE_DELAY_UNTIL_TURN`, `Amount = 3`
  - `Kind = BATTLEFIELD_PREVENT_MOVE_TO_BASE`
  - `Kind = BATTLEFIELD_PREVENT_UNIT_PLAY`
  - `Kind = BATTLEFIELD_ECHO_COST_REDUCTION`, `Amount = 1`
  - `Kind = BATTLEFIELD_EQUIPMENT_COST_REDUCTION`, `Amount = 1`
  - `Kind = BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE`, `Amount = 1`
  - `Kind = BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS`, `Amount = 1`
  - `Kind = BATTLEFIELD_EXTRA_STANDBY_DESTINATION`
  - `Kind = BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL`, `Amount = 3`
  - `Kind = BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT`
- `CoreRuleEngine` move and play rejection paths now find eligible battlefield sources through `BattlefieldStaticAbilitySpecRules`.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_PREVENT_MOVE_TO_BASE` path through legal official Vex deck submission/opening, P1 `OGN·295/298` selection, server-authored `MOVE_UNIT` prompt filtering, rejected battlefield-to-base `MOVE_UNIT`, no-mutation state hash, score victory, and final-state action-log replay that includes the rejected command.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_PREVENT_UNIT_PLAY` path through legal official Vex deck submission/opening, P1 `SFD·216/221` selection, server-authored `PLAY_CARD` prompt destination filtering, rejected battlefield-destination `PLAY_CARD`, no-mutation state hash, score victory, and final-state action-log replay that includes the rejected command.
- `CoreRuleEngine` Echo optional-cost planning now reads `BATTLEFIELD_ECHO_COST_REDUCTION` from `BehaviorSpec.StaticAbilities` instead of the old `BattlefieldEchoCostReductionCardNo` branch.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_ECHO_COST_REDUCTION` path through legal official Jhin deck submission/opening, server-authored `ECHO` optional-cost prompt metadata, reduced cost payment, stack repeat count, two-card draw resolution, score victory, and final-state action-log replay.
- `CoreRuleEngine` equipment play cost planning now reads `BATTLEFIELD_EQUIPMENT_COST_REDUCTION` from `BehaviorSpec.StaticAbilities` instead of the old `BattlefieldEquipmentCostReductionCardNo` branch.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_EQUIPMENT_COST_REDUCTION` path through legal official Rumble deck submission/opening, server-authored Long Sword `PLAY_CARD` prompt metadata, reduced first-equipment payment, equipment stack resolution, score victory, and final-state action-log replay.
- `CoreRuleEngine` battlefield granted unit-experience activation now reads `BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE` from `BehaviorSpec.StaticAbilities` instead of the old `BattlefieldGrantUnitExperienceCardNo` branch, and uses `Amount` for the experience event.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE` path through legal official Vex deck submission/opening, server-authored `ACTIVATE_ABILITY`, source exhaustion, `EXPERIENCE_GAINED.totalExperience=1`, score victory, and final-state action-log replay.
- `CoreRuleEngine` spell/skill damage resolution now reads `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` from `BehaviorSpec.StaticAbilities` instead of the old `BattlefieldTargetSpellSkillDamageBonusCardNo` branch, and uses `Amount` for the damage modifier.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` path through legal official Jhin vs Vex deck submission/opening, official `UNL-007/219` Punishment stack resolution, `DAMAGE_APPLIED.damage=4`, score victory, and final-state action-log replay.
- `CoreRuleEngine` `HIDE_CARD` battlefield destination validation now reads `BATTLEFIELD_EXTRA_STANDBY_DESTINATION` from `BehaviorSpec.StaticAbilities` instead of the old `BattlefieldExtraStandbyCardNo` / `BattlefieldExtraStandbyAltCardNo` branch.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_EXTRA_STANDBY_DESTINATION` guard through legal official Poppy deck submission/opening without Bandle Tree, server-authored `HIDE_CARD` prompt destination filtering, rejected direct `HIDE_CARD` to `BATTLEFIELD:<non-Bandle battlefield>`, no-mutation state hash, score victory, and final-state action-log replay that includes the rejected command.
- `CoreRuleEngine` battle-destroyed recall replacement now reads `BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL` from `BehaviorSpec.StaticAbilities` instead of the old `BattlefieldDestroyedInBattleRecallCardNo` branch, and uses `Amount` for the mana payment.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL` path through legal official Vex vs Rumble deck submission/opening, P2 `UNL-206/219` Blood Altar selection, real `DECLARE_BATTLE` / `ASSIGN_COMBAT_DAMAGE`, 3-mana payment, replacement recall to base, score victory, and final-state action-log replay.
- `CoreRuleEngine` battlefield-granted legend armament attach action now requires the player to control a battlefield whose `BehaviorSpec.StaticAbilities` includes `BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT`, instead of requiring `SFD·208/221` through `BattlefieldGrantLegendAttachArmamentCardNo` / `RequiredControlledBattlefieldCardNo`.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT` path through legal official Rumble deck submission/opening, server-authored `LEGEND_ACT` prompt metadata, legend exhaustion, armament attachment, score victory, and final-state action-log replay.
- `FullGameEndToEndTests` now carries the same `BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT` guard through legal official Rumble deck submission/opening without Poro Forge, server-authored `LEGEND_ACT` prompt filtering, rejected direct `LEGEND_ACT`, no-mutation state hash, score victory, and final-state action-log replay that includes the rejected command.
- `MatchSession` prompt filtering, Echo/equipment cost metadata, and battlefield-object recognition use the same static ability spec queries instead of the old `BattlefieldPreventMoveToBaseCardNo` / `BattlefieldPreventUnitPlayCardNo` / `BattlefieldEchoCostReductionCardNo` / `BattlefieldEquipmentCostReductionCardNo` constants.
- `MatchSession` granted unit-experience prompt filtering and battlefield-object recognition use the same static ability spec query instead of the old `BattlefieldGrantUnitExperienceCardNo` constant.
- `MatchSession` battlefield-object recognition uses the same static ability spec query instead of the old `BattlefieldTargetSpellSkillDamageBonusCardNo` constant.
- `MatchSession` extra-standby prompt destination filtering and battlefield-object recognition use the same static ability spec query instead of the old Bandle Tree card-number allow-list.
- `MatchSession` battlefield-object recognition uses the same static ability spec query instead of the old Blood Altar card-number allow-list.
- `MatchSession` legend-action prompt source filtering and battlefield-object recognition use the same static ability spec query instead of the old Poro Forge card-number allow-list; development seed data keeps `SFD·208/221` only as a seed card identity.

## 2026-07-02 Supplement: Battlefield Winning-Score Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldWinningScoreIncreaseAbility(...)` and routes Mind and Balance-style winning-score threshold modifiers through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, predicate, out ability)` now delegates to a generic `CardStaticAbilitySpecRules.TryGetStaticAbility(...)` predicate overload. `BattlefieldStaticAbilitySpecRules.IsBattlefieldWinningScoreIncreaseAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_WINNING_SCORE_INCREASE` plus positive `Amount`.

`CoreRuleEngine`, `MatchSession`, and `MatchRecovery` now compute effective winning score and battlefield rule-card recognition by enumerating `BehaviorSpec.StaticAbilities` through that predicate. Existing `OGN·276/298` / `OGN·276a/298` official behavior, `MATCH_WON.winningScore` payloads, prompt threshold checks, recovery validation, and official-deck score-victory replay remain compatible.

Validation: red/green focused guard `BattlefieldWinningScoreStaticAbilityUsesGenericSpecPredicate` 1/1; focused winning-score parser / P79 runtime / GameHub seed / official-deck route 8/8; adjacent `BattlefieldStatic|WinningScore|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2550/2550; backend full conformance 9120/9120.

Non-closure: this slice only closes the winning-score static ability execution helper. The remaining battlefield static ability per-effect helper surface, complete battlefield lifecycle, complete score-delay physical `此处` scoping, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Score-Delay Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldScoreDelayUntilTurnAbility(...)` and routes Forgotten Monument-style score-prevention through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldScoreDelayUntilTurnAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_SCORE_DELAY_UNTIL_TURN` plus positive `Amount`. `CoreRuleEngine.HasDedicatedBattlefieldScoreRuleSpec`, `CoreRuleEngine.TryBuildBattlefieldScorePreventedEvent`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldScoreDelayUntilTurnAbility, out ability)`.

Existing `SFD·209/221` official behavior, `BATTLEFIELD_SCORE_PREVENTED` payloads, `releasedTurnOrdinal = 3`, score-victory replay, and hidden-info recovery coverage remain compatible.

Validation: red/green focused guard `BattlefieldScoreDelayStaticAbilityUsesGenericSpecPredicate` 1/1; focused score-delay parser / P79 runtime / GameHub seed / official-deck route 8/8; adjacent `ScoreDelay|ScorePrevented|BattlefieldStatic|FirstTurnScore|BattlefieldHeldScore|BattleResponse|BattlefieldTriggerSpec|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2617/2617; backend full conformance 9121/9121.

Non-closure: this slice only closes the score-delay static ability execution helper. The remaining battlefield static ability per-effect helper surface, complete physical `此处` score-prevention scoping, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Prevent Move-To-Base Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldPreventMoveToBaseAbility(...)` and routes Vilemaw's Lair-style movement prevention through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldPreventMoveToBaseAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_PREVENT_MOVE_TO_BASE`. `CoreRuleEngine.HasBattlefieldStaticPreventMoveToBase`, `CoreRuleEngine.HasImplementedBattlefieldRuleSpec`, `MatchSession.HasMoveUnitPromptPreventMoveToBase`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldPreventMoveToBaseAbility, out _)`.

Existing `OGN·295/298` official behavior, server-authored `MOVE_UNIT` prompt filtering, rejected `BATTLEFIELD_TO_BASE` command behavior, no-mutation state hash, hidden-info checks, and official-deck score-victory replay remain compatible.

Validation: red/green focused guard `BattlefieldPreventMoveToBaseStaticAbilityUsesGenericSpecPredicate` 1/1; focused static-restriction parser / P79 runtime / prompt / official-deck route 7/7; adjacent `BattlefieldStatic|PreventMoveToBase|MoveUnit|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2631/2631; backend full conformance 9122/9122.

Non-closure: this slice only closes the prevent-move-to-base static ability execution helper. The remaining battlefield static ability per-effect helper surface, complete movement / control-zone edge cases, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Prevent Unit-Play Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldPreventUnitPlayAbility(...)` and routes Falling Rocks-style unit-play prevention through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldPreventUnitPlayAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_PREVENT_UNIT_PLAY`. `CoreRuleEngine.HasBattlefieldStaticPreventUnitPlayToBattlefield`, `CoreRuleEngine.HasImplementedBattlefieldRuleSpec`, `MatchSession.PromptBattlefieldStaticPreventsUnitPlayToBattlefield`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldPreventUnitPlayAbility, out _)`.

Existing `SFD·216/221` official behavior, server-authored `PLAY_CARD` destination filtering, rejected battlefield-destination command behavior, no-mutation state hash, hidden-info checks, and official-deck score-victory replay remain compatible.

Validation: red/green focused guard `BattlefieldPreventUnitPlayStaticAbilityUsesGenericSpecPredicate` 1/1; focused static-restriction parser / P79 runtime / prompt / official-deck route 6/6; adjacent `BattlefieldStatic|PreventUnitPlay|PlayCard|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2804/2804; backend full conformance 9123/9123.

Non-closure: this slice only closes the prevent-unit-play static ability execution helper. The remaining battlefield static ability per-effect helper surface, complete play destination policy / timing-window breadth, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Echo Cost-Reduction Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldEchoCostReductionAbility(...)` and routes Marai Spire-style Echo optional-cost reduction through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldEchoCostReductionAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_ECHO_COST_REDUCTION` plus positive `Amount`. `CoreRuleEngine.ResolveBattlefieldEchoCostReductionMana`, `CoreRuleEngine.HasImplementedBattlefieldRuleSpec`, `MatchSession.PromptBattlefieldEchoCostReductionMana`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldEchoCostReductionAbility, out ability)`.

Existing `SFD·211/221` official behavior, controlled battlefield-source filtering, server-authored Echo optional-cost prompt metadata, `COST_PAID.battlefieldEchoCostReductionMana`, stack `effectRepeatCount = 2`, repeated draw resolution, hidden-info checks, and official-deck score-victory replay remain compatible.

Validation: red/green focused guard `BattlefieldEchoCostReductionStaticAbilityUsesGenericSpecPredicate` 1/1; focused parser / P79 runtime / prompt / GameHub seed / official-deck route 7/7; adjacent `BattlefieldStatic|EchoCostReduction|CenterStage|Echo|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2617/2617; backend full conformance 9124/9124.

Non-closure: this slice only closes the Echo cost-reduction static ability execution helper. The remaining battlefield static ability per-effect helper surface, complex Echo costs, complete PaymentEngine optional-cost matrix, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Equipment Cost-Reduction Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldEquipmentCostReductionAbility(...)` and routes Ornn's Forge-style first-equipment cost reduction through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldEquipmentCostReductionAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_EQUIPMENT_COST_REDUCTION` plus positive `Amount`. `CoreRuleEngine.ResolveBattlefieldEquipmentCostReductionMana`, `CoreRuleEngine.HasImplementedBattlefieldRuleSpec`, `MatchSession.PromptBattlefieldEquipmentCostReductionMana`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldEquipmentCostReductionAbility, out ability)`.

Existing `SFD·213/221` official behavior, controlled battlefield-source filtering, server-authored equipment `PLAY_CARD` prompt metadata, once-per-turn `PLAYED_EQUIPMENT_THIS_TURN` tracking, `COST_PAID.battlefieldEquipmentCostReductionMana`, equipment stack resolution, hidden-info checks, and official-deck score-victory replay remain compatible.

Validation: red/green focused guard `BattlefieldEquipmentCostReductionStaticAbilityUsesGenericSpecPredicate` 1/1; focused parser / P79 runtime / prompt / GameHub seed / official-deck route 7/7; adjacent `BattlefieldStatic|EquipmentCostReduction|LongSword|Equipment|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 3025/3025; backend full conformance 9125/9125.

Non-closure: this slice only closes the equipment cost-reduction static ability execution helper. The remaining battlefield static ability per-effect helper surface, complex equipment costs, complete PaymentEngine equipment-cost matrix, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Granted Unit-Experience Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldGrantUnitExperienceAbility(...)` and routes Mutation Garden-style granted unit experience activation through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantUnitExperienceAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE` plus positive `Amount`. `CoreRuleEngine` battlefield granted unit-experience activation resolution, `CoreRuleEngine.HasImplementedBattlefieldRuleSpec`, `MatchSession.BattlefieldGrantUnitExperienceObjectId`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantUnitExperienceAbility, out ability)`.

Existing `UNL-213/219` official behavior, controlled battlefield-source filtering, server-authored `ACTIVATE_ABILITY` prompt metadata, source exhaustion, `BATTLEFIELD_TRIGGER_RESOLVED.amount = 1`, `EXPERIENCE_GAINED.totalExperience = 1`, hidden-info checks, and official-deck score-victory replay remain compatible.

Validation: red/green focused guard `BattlefieldGrantUnitExperienceStaticAbilityUsesGenericSpecPredicate` 1/1; focused parser / P79 runtime / rejection / official-deck route 12/12; adjacent `BattlefieldStatic|UnitExperience|Experience|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2607/2607; backend full conformance 9126/9126.

Non-closure: this slice only closes the granted unit-experience static ability execution helper. The remaining battlefield static ability per-effect helper surface, complete activated ability modeling for granted abilities, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Target Spell/Skill Damage-Bonus Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldTargetSpellSkillDamageBonusAbility(...)` and routes Void Gate-style target spell/skill damage modifiers through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldTargetSpellSkillDamageBonusAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS` plus positive `Amount`. `CoreRuleEngine.ApplyBattlefieldTargetSpellSkillDamageBonus`, `CoreRuleEngine.HasImplementedBattlefieldRuleSpec`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldTargetSpellSkillDamageBonusAbility, out ability)`.

Existing `OGN·296/298` official behavior, same-battlefield target checks, controlled battlefield-source filtering, official `UNL-007/219` Punishment stack resolution, `DAMAGE_APPLIED.damage = 4`, hidden-info checks, and official-deck score-victory replay remain compatible.

Validation: red/green focused guard `BattlefieldTargetSpellSkillDamageBonusStaticAbilityUsesGenericSpecPredicate` 1/1; focused parser / P79 runtime / GameHub seed / official-deck route 7/7; adjacent `BattlefieldStatic|TargetDamageBonus|TargetSpellSkillDamageBonus|Punishment|Damage|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2774/2774; backend full conformance 9127/9127.

Non-closure: this slice only closes the target spell/skill damage-bonus static ability execution helper. The remaining battlefield static ability per-effect helper surface, all spell/skill damage modifier timing edges, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Granted Legend Attach-Armament Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldGrantLegendAttachArmamentAbility(...)` and routes Poro Forge-style granted legend attach-armament battlefield recognition through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantLegendAttachArmamentAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT`. `CoreRuleEngine.HasImplementedBattlefieldRuleSpec` and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantLegendAttachArmamentAbility, out _)`. Existing `RequiredControlledBattlefieldStaticAbilityKind` execution gates already use the generic static ability kind path and remain unchanged.

Existing `SFD·208/221` official behavior, controlled battlefield-source filtering, server-authored `LEGEND_ACT` prompt metadata, legend exhaustion, armament attachment / reattachment, rejected-action no-mutation guard, hidden-info checks, and official-deck score-victory replays remain compatible.

Validation: red/green focused guard `BattlefieldGrantLegendAttachArmamentStaticAbilityUsesGenericSpecPredicate` 1/1; focused parser / P79 runtime / GameHub seed / official-deck accepted+rejected route 10/10; adjacent `PoroForge|LegendAttachArmament|LegendAct|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2586/2586; backend full conformance 9128/9128.

Non-closure: this slice only closes the granted legend attach-armament static ability execution helper. The remaining battlefield static ability per-effect helper surface, complete granted-ability modeling, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Extra-Standby Destination Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldExtraStandbyDestinationAbility(...)` and routes Bandle Tree-style extra standby destination permission through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldExtraStandbyDestinationAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_EXTRA_STANDBY_DESTINATION`. `CoreRuleEngine.TryGetBattlefieldExtraStandbyObject`, `CoreRuleEngine.HasImplementedBattlefieldRuleSpec`, `MatchSession.ControlledBattlefieldExtraStandbyObjects`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldExtraStandbyDestinationAbility, out _)`.

Existing `OGN·278/298` / `OGN·278a/298` official behavior, controlled battlefield-source filtering, server-authored `HIDE_CARD` destination metadata, `CARD_HIDDEN.destinationZone = BATTLEFIELD`, cleanup after control loss, rejected destination no-mutation guard, hidden-info checks, and official-deck score-victory replays remain compatible.

Validation: red/green focused guard `BattlefieldExtraStandbyStaticAbilityUsesGenericSpecPredicate` 1/1; focused parser / P79 runtime / GameHub seed / official-deck accepted+cleanup+rejected route 11/11; adjacent `BandleTree|BattlefieldExtraStandby|ExtraStandby|HideCard|Standby|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2629/2629; backend full conformance 9129/9129.

Non-closure: this slice only closes the extra-standby destination static ability execution helper. The remaining battlefield static ability per-effect helper surface, complete extra-standby hidden-info breadth, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Destroyed-In-Battle Recall Static Ability Generic Predicate

This follow-up removes `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldDestroyedInBattlePayRecallReplacementAbility(...)` and routes Blood Altar-style battle-destroyed recall replacement through the generic battlefield static ability predicate path.

`BattlefieldStaticAbilitySpecRules.IsBattlefieldDestroyedInBattlePayRecallReplacementAbility` validates the parsed `StaticAbilitySpec` shape by `Kind = BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL` plus positive `Amount`. `CoreRuleEngine.TryApplyBattlefieldDestroyedInBattleRecallReplacement`, `CoreRuleEngine.HasImplementedBattlefieldRuleSpec`, and `MatchSession` battlefield rule-card recognition now use `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldDestroyedInBattlePayRecallReplacementAbility, out ability)`.

Existing `UNL-206/219` official behavior, `DECLARE_BATTLE_COMBAT_DAMAGE` timing gate, controlled battlefield-source filtering, `Amount = 3` mana payment, damage removal, exhaustion, recall-to-base replacement, fallback destruction when unable to pay, hidden-info checks, and official-deck score-victory replay remain compatible.

Validation: red/green focused guard `BattlefieldDestroyedInBattleRecallStaticAbilityUsesGenericSpecPredicate` 1/1; focused parser / P79 runtime / GameHub seed / official-deck route 7/7; adjacent `BloodAltar|BattleDestroyedRecall|DeclareBattle|BattleDamageAssignment|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2643/2643; backend full conformance 9130/9130.

Non-closure: this slice only closes the destroyed-in-battle recall static ability execution helper. The remaining battlefield static ability per-effect helper surface, complete battle-destroyed replacement / payment prompt breadth, complete battlefield lifecycle, P0, and READY remain open.

## 2026-07-02 Supplement: Battlefield Static Ability Kind Lookup Generic Predicate

This follow-up removes the residual `BattlefieldStaticAbilitySpecRules.TryGetBattlefieldStaticAbility(cardNo, kind, out ability)` helper. The remaining callers were the Core/MatchSession checks for `RequiredControlledBattlefieldStaticAbilityKind`, used by battlefield-granted legend actions such as Poro Forge. Those callers now use the generic `BattlefieldStaticAbilitySpecRules.TryGetAbility(cardNo, ability => ability.Kind == staticAbilityKind, out _)` predicate route.

Existing `SFD·208/221` Poro Forge behavior, controlled battlefield-source filtering, server-authored `LEGEND_ACT` prompt metadata, accepted attach-armament resolution, rejected no-controlled-forge guard, hidden-info checks, and official-deck score-victory replays remain compatible.

Validation: red/green focused guard `BattlefieldStaticAbilityKindLookupUsesGenericSpecPredicate|BattlefieldSpecDomainHelpersDoNotUseCardNumberHelperNames` 2/2; adjacent `BattlefieldStaticAbilityKindLookup|PoroForge|LegendAttachArmament|LegendAct|BattlefieldStatic|FullGameEndToEnd|MatchRecovery|CardCatalogBaseline` 2670/2670; backend full conformance 9139/9139.

Non-closure: this slice only closes the residual kind lookup helper surface. Complete battlefield static ability breadth, complete granted-ability modeling, complete battlefield lifecycle, P0, and READY remain open.

## Non-Closure

This is a narrow B4 cleanup slice. It does not close all battlefield static abilities, complete battlefield lifecycle, complete movement / control-zone edge cases, full activated ability modeling for granted abilities, all spell/skill damage modifier timing edges, complete extra-standby destination / hidden-info breadth, complete battle-destroyed replacement / payment prompt breadth, frontend/browser smoke, full official coverage or READY.

## Validation

- latest battlefield static ability kind lookup generic-predicate guard red/green: passed `2/2`;
- latest B4 kind lookup / PoroForge / LegendAttachArmament / LegendAct / BattlefieldStatic / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2670/2670`;
- backend full conformance after the kind lookup generic-predicate increment: passed `9139/9139`;
- latest destroyed-in-battle recall static ability generic-predicate guard red/green: passed `1/1`;
- latest destroyed-in-battle recall parser / P79 runtime / GameHub seed / official-deck route focused validation: passed `7/7`;
- latest BloodAltar / BattleDestroyedRecall / DeclareBattle / BattleDamageAssignment / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2643/2643`;
- backend full conformance after the destroyed-in-battle recall generic-predicate increment: passed `9130/9130`;
- latest extra-standby destination static ability generic-predicate guard red/green: passed `1/1`;
- latest extra-standby parser / P79 runtime / GameHub seed / official-deck accepted+cleanup+rejected route focused validation: passed `11/11`;
- latest BandleTree / BattlefieldExtraStandby / ExtraStandby / HideCard / Standby / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2629/2629`;
- backend full conformance after the extra-standby generic-predicate increment: passed `9129/9129`;
- latest granted legend attach-armament static ability generic-predicate guard red/green: passed `1/1`;
- latest granted legend attach-armament parser / P79 runtime / GameHub seed / official-deck accepted+rejected route focused validation: passed `10/10`;
- latest PoroForge / LegendAttachArmament / LegendAct / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2586/2586`;
- backend full conformance after the granted legend attach-armament generic-predicate increment: passed `9128/9128`;
- latest target spell/skill damage-bonus static ability generic-predicate guard red/green: passed `1/1`;
- latest target spell/skill damage-bonus parser / P79 runtime / GameHub seed / official-deck route focused validation: passed `7/7`;
- latest BattlefieldStatic / TargetDamageBonus / TargetSpellSkillDamageBonus / Punishment / Damage / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2774/2774`;
- backend full conformance after the target spell/skill damage-bonus generic-predicate increment: passed `9127/9127`;
- latest granted unit-experience static ability generic-predicate guard red/green: passed `1/1`;
- latest granted unit-experience parser / P79 runtime / rejection / official-deck route focused validation: passed `12/12`;
- latest BattlefieldStatic / UnitExperience / Experience / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2607/2607`;
- backend full conformance after the granted unit-experience generic-predicate increment: passed `9126/9126`;
- latest equipment cost-reduction static ability generic-predicate guard red/green: passed `1/1`;
- latest equipment parser / P79 runtime / prompt / GameHub seed / official-deck route focused validation: passed `7/7`;
- latest BattlefieldStatic / EquipmentCostReduction / LongSword / Equipment / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `3025/3025`;
- backend full conformance after the equipment cost-reduction generic-predicate increment: passed `9125/9125`;
- latest Echo cost-reduction static ability generic-predicate guard red/green: passed `1/1`;
- latest Echo parser / P79 runtime / prompt / GameHub seed / official-deck route focused validation: passed `7/7`;
- latest BattlefieldStatic / EchoCostReduction / CenterStage / Echo / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2617/2617`;
- backend full conformance after the Echo cost-reduction generic-predicate increment: passed `9124/9124`;
- latest Poro Forge official-deck rejected-command replay focused validation: passed `1/1`;
- latest PoroForge / LegendAttachArmament / LegendAct / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2486/2486`;
- backend full conformance after the Poro Forge rejected-command replay increment: passed `8865/8865`;
- latest extra-standby official-deck rejected-command replay focused validation: passed `1/1`;
- latest BandleTree / BattlefieldExtraStandby / HideCard / Standby / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2506/2506`;
- backend full conformance after the extra-standby replay increment: passed `8864/8864`;
- latest prevent unit-play official-deck rejected-command replay focused validation: passed `1/1`;
- latest prevent unit-play / PlayCard / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2689/2689`;
- backend full conformance after the prevent unit-play replay increment: passed `8863/8863`;
- latest prevent move-to-base official-deck rejected-command replay focused validation: passed `1/1`;
- latest prevent move-to-base / MoveUnit / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2517/2517`;
- backend full conformance after the prevent move-to-base replay increment: passed `8862/8862`;
- latest Blood Altar official-deck replay focused validation: passed `1/1`;
- latest Blood Altar battle-destroyed recall / DeclareBattle / BattleDamageAssignment / FullGameEndToEnd / MatchRecovery / CardCatalogBaseline adjacent validation: passed `2597/2597`;
- backend full conformance after the Blood Altar replay increment: passed `8861/8861`;
- latest Poro Forge official-deck replay focused validation: passed `1/1`;
- latest Poro Forge legend attach-armament / LegendAct / FullGameEndToEnd / MatchRecovery adjacent validation: passed `2249/2249`;
- backend full conformance after the Poro Forge replay increment: passed `8860/8860`;
- latest Ornn's Forge official-deck replay focused validation: passed `1/1`;
- latest Ornn's Forge equipment cost-reduction / LongSword / FullGameEndToEnd / MatchRecovery adjacent validation: passed `2179/2179`;
- backend full conformance after the Ornn's Forge replay increment: passed `8859/8859`;
- latest Marai Spire official-deck replay focused validation: passed `1/1`;
- latest Marai Spire Echo / FullGameEndToEnd / MatchRecovery adjacent validation: passed `2208/2208`;
- backend full conformance after the Marai Spire replay increment: passed `8858/8858`;
- latest Mutation Garden official-deck replay focused validation: passed `1/1`;
- latest Mutation Garden granted unit-experience / FullGameEndToEnd / MatchRecovery adjacent validation: passed `2183/2183`;
- backend full conformance after the Mutation Garden replay increment: passed `8857/8857`;
- latest Void Gate official-deck replay focused validation: passed `1/1`;
- latest Void Gate target-damage / FullGameEndToEnd / MatchRecovery adjacent validation: passed `2086/2086`;
- backend full conformance after the Void Gate replay increment: passed `8856/8856`;
- latest focused behavior-spec/source guard/Poro Forge legend attach runtime representatives: passed `8/8`;
- CardCatalog baseline: passed `172/172`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8471/8471`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.
