# Plan B / B4 Battlefield Static Ability Spec Audit

Date: 2026-06-28

Status: focused B4 battlefield score-delay static ability generic-predicate route accepted; project remains **NOT READY**.

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

## Non-Closure

This is a narrow B4 cleanup slice. It does not close all battlefield static abilities, complete battlefield lifecycle, complete movement / control-zone edge cases, full activated ability modeling for granted abilities, all spell/skill damage modifier timing edges, complete extra-standby destination / hidden-info breadth, complete battle-destroyed replacement / payment prompt breadth, frontend/browser smoke, full official coverage or READY.

## Validation

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
