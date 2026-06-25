# Plan B / B4 Battlefield Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·277/298` 后巷酒吧 has official text `每当一名单位从此处向别处移动时，让其本回合内{{S}}+1。`
- `data/official/card-catalog.zh-CN.json`: `UNL-216/219` 皮城学院 has official text `当你据守此处时，在本回合内，你的下一个法术获得等同于其基础费用的{{回响}}。（你可以选择支付此额外费用，以重复此法术效果。）`
- `data/official/card-catalog.zh-CN.json`: `UNL-219/219` 海力亚秘库 has official text `当你据守此处时，你的非指示物单位在本回合内的打出费用增加{{1}}。`
- `data/official/card-catalog.zh-CN.json`: `OGN·292/298` 幻梦之树 has official text `每回合首次：当你对此处的友方单位使用法术时，抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `UNL-205/219` 废弃大厅 has official text `当一名玩家打出法术时，该玩家可以选择让自己在此处控制的一名单位在本回合内{{S}}+1。`
- `data/official/card-catalog.zh-CN.json`: `UNL-211/219` 失落书库 has official text `若此战场受你控制，当你打出一张法术牌时，如果消耗了不低于{{4}}法力，则进行{{洞察}}。（查看你主牌堆顶部的一张牌。你可以选择将其回收。）`
- `data/official/card-catalog.zh-CN.json`: `UNL-218/219` 偶像谷 has official text `当一名玩家在此处打出一名单位时，该玩家可以选择支付{{1}}，以此给予该单位{{增益}}。（未拥有增益的单位获得一个{{S}}+1增益。）`
- `data/official/card-catalog.zh-CN.json`: `UNL-214/219` 鬼影湾 has official text `当此处的一名单位返回到一名玩家的手牌时，该玩家可以选择支付{{1}}，以此召出一枚休眠的符文。`
- `data/official/card-catalog.zh-CN.json`: `UNL-215/219` 流星疗泉 has official text `每回合首次，当玩家在此处打出一名非指示物单位时，该玩家可以选择将自己在此处控制的另一名单位移动到其基地。`
- `data/official/card-catalog.zh-CN.json`: `OGN·280/298` has official text `当你据守此处时，抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `OGN·288/298` 星尖峰 has official text `当你据守此处时，你可以选择召出一枚休眠的符文。`
- `data/official/card-catalog.zh-CN.json`: `SFD·219/221` 彩纸灵树 has official text `当你据守此处时，每名玩家召出一枚休眠的符文。`
- `data/official/card-catalog.zh-CN.json`: `UNL-207/219` 业余排练厅 has official text `当你据守此处时，你可以选择将战场上的一名单位移动到其基地。`
- `data/official/card-catalog.zh-CN.json`: `OGN·283/298` 纳沃利角斗场 has official text `当你据守此处时，给予此处的一名单位增益。（如果该单位未拥有增益，则获得一个{{S}}+1增益。）`
- `data/official/card-catalog.zh-CN.json`: `OGN·275/298` 团结圣坛 has official text `当你据守此处时，打出一名1{{S}}的“随从”到你的基地。`
- `data/official/card-catalog.zh-CN.json`: `OGN·281/298` 圣化之墓 has official text `当你据守此处时，如果你的英雄区域已无英雄单位牌，则可以选择让该英雄从废牌堆中返回英雄区域。`
- `data/official/card-catalog.zh-CN.json`: `OGN·293/298` / `OGN·293a/298` 宏伟广场 has official text `当你据守此处，且在此拥有至少七名单位时，你赢得游戏胜利。`
- `data/official/card-catalog.zh-CN.json`: `OGN·291/298` 烛光圣殿 has official text `当你征服此处时，查看主牌堆顶部的两张牌。你可以选择从这两张牌中回收任意数量的卡牌，并将其余的卡牌按任意顺序放回原处。`
- `data/official/card-catalog.zh-CN.json`: `SFD·212/221` 雷区 has official text `当你征服此处时，将你主牌堆顶部的两张牌放入废牌堆。`
- `data/official/card-catalog.zh-CN.json`: `OGN·287/298` 雷霆之纹 has official text `当你征服此处时，回收一枚你的符文。`
- `data/official/card-catalog.zh-CN.json`: `OGN·282/298` 希拉娜修道院 has official text `当你征服此处时，你可以选择消耗一个增益，以此抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `OGN·298/298` 祖安地沟 has official text `当你征服此处时，弃置一张手牌，然后抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `SFD·217/221` 权能之座 has official text `当你征服此处时，你和盟友每控制一处其他战场，你便抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `OGN·289/298` 巨神峰之巅 has official text `当你征服此处时，选择两枚符文，并在本回合结束时，让它们变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `SFD·221/221` 月帷祭坛 has official text `当你征服此处时，你可以选择让一件友方装备变为活跃状态。如果它是一件武装，则你可以选择将其卸除。`
- `data/official/card-catalog.zh-CN.json`: `SFD·220/221` 珍宝堆 has official text `当你征服此处时，你可以选择支付{{1}}，以此打出一个休眠的“金币”装备指示物。`
- `data/official/card-catalog.zh-CN.json`: `SFD·218/221` 沉没神庙 has official text `当你征服此处时，如果此战场上留存至少一名{{强力}}单位，则你可以选择支付{{1}}来抽一张牌。（战力达到5或以上时，即为强力单位。）`
- `data/official/card-catalog.zh-CN.json`: `SFD·207/221` 帝王神坛 has official text `当你征服此处时，你可以选择支付{{1}}并让你在此处控制的一名单位返回其所属的手牌，以此在此处打出一名2{{S}}的“黄沙士兵”。`
- `data/official/card-catalog.zh-CN.json`: `SFD·210/221` 传奇殿堂 has official text `当你征服此处时，你可以选择支付{{1}}，以此让你的传奇变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `SFD·215/221` 拉文布鲁姆学院 has official text `当你防守此处时，展示你主牌堆顶部的一张牌。如果是一张法术牌，则将其放入你的手牌，否则将其回收。`
- `data/official/card-catalog.zh-CN.json`: `UNL-217/219` 捕猎场 has official text `当你征服此处时，如果你给敌方单位分配了不低于3点的过量伤害，则打出一名1{{S}}“战鹰”，它拥有{{法盾}}。`
- `data/official/card-catalog.zh-CN.json`: `UNL-212/219` 冰霜要塞 has official text `在每名玩家各自的开始阶段开始时，对此处的所有单位造成1点伤害。`
- `data/official/card-catalog.zh-CN.json`: `UNL-209/219` 暮色玫瑰实验室 has official text `在你的开始阶段开始时，你可以选择摧毁一名此处由你控制的单位，以此抽一张牌。（此行动在得分前进行。）`
- `data/official/card-catalog.zh-CN.json`: `OGN·284/298` 力量方尖碑 has official text `每名玩家在各自的第一个回合开始阶段，额外召出一枚符文。`
- `data/official/card-catalog.zh-CN.json`: `OGN·290/298` 荣耀竞技场 has official text `每名玩家在各自的第一个回合开始阶段，获得1分。`
- `data/official/card-catalog.zh-CN.json`: `SFD·209/221` 遗忘丰碑 has official text `每名玩家在各自的第三回合开始前，无法从此处获得分数。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldMovedUnitGainsTemporaryPower`, `P79BattlefieldMovedUnitPowerSkipsOpponentControlledSource`, and `P79BattlefieldMovePowerSeedMovesUnitAndAppliesBonus` remain the runtime evidence for this narrow behavior.
- Existing representative tests `P79BattlefieldHeldNextSpellEcho...` and GameHub `P79BattlefieldHeldNextSpellEcho...` remain the runtime evidence for the held-next-spell Echo behavior.
- Existing representative tests `P79BattlefieldHeldUnitCostIncrease...` and GameHub `P79BattlefieldHeldUnitCostIncrease...` remain the runtime evidence for the held unit-cost increase behavior.
- Existing representative tests `P79BattlefieldFriendlySpellTarget...` and GameHub `P79BattlefieldFriendlySpellDrawSeed...` remain the runtime evidence for the friendly-spell draw behavior.
- Existing representative tests `P79BattlefieldSpellPowerBonus...` and GameHub `P79BattlefieldSpellPowerBonusSeed...` remain the runtime evidence for the spell-power bonus behavior.
- Existing representative tests `P79BattlefieldHighCostSpellInsight...` and GameHub `P79BattlefieldHighCostSpellInsightSeed...` remain the runtime evidence for the high-cost spell insight behavior.
- Existing representative tests `P79BattlefieldPlayUnitBoon...` and GameHub `P79BattlefieldPlayUnitBoonSeed...` remain the runtime evidence for the unit-play pay-mana boon behavior.
- Existing representative tests `P79BattlefieldReturnedUnit...` and GameHub `P79BattlefieldReturnCallRuneSeed...` remain the runtime evidence for the unit-returned pay-mana call-rune behavior.
- Existing representative tests `P79BattlefieldFirstUnitPlayedMoveOther...` and GameHub `P79BattlefieldFirstUnitMoveOtherSeed...` remain the runtime evidence for the first non-token unit-play move-other-to-base behavior.
- Existing representative tests `P79BattlefieldHeldDraw...` and GameHub `P79BattlefieldHeldDrawSeed...` remain the runtime evidence for the held draw-one behavior.
- Existing representative tests `P79BattlefieldHeldCallsRuneForHolder` and GameHub `P79BattlefieldHeldRuneSeedOffersBattlefieldDestinationAndCallsRuneForHolder` remain the runtime evidence for the held call-rune behavior.
- Existing representative tests `P79BattlefieldHeldCallsRunesForEachPlayer` and GameHub `P79BattlefieldHeldRunesSeedOffersBattlefieldDestinationAndCallsRunes` remain the runtime evidence for the held each-player call-rune behavior.
- Existing representative tests `P79BattlefieldHeldMovesSurvivingDefenderToBase` and GameHub `P79BattlefieldHeldMoveToBaseSeedOffersBattlefieldDestinationAndMovesDefender` remain the runtime evidence for the held move-unit-to-base behavior.
- Existing representative tests `P79BattlefieldHeldGrantsBoonToSurvivingDefender` and GameHub `P79BattlefieldHeldBoonSeedOffersBattlefieldDestinationAndGrantsBoon` remain the runtime evidence for the held grant-boon behavior.
- Existing representative tests `P79BattlefieldHeldCreatesMinionInBase`, `P79BattlefieldHeldMinionSeedOffersBattlefieldDestinationAndCreatesToken`, and `TurnStartHeldBattlefieldScoresAndTriggersOgn275Minion` remain the runtime evidence for the held create-minion behavior.
- Existing representative tests `P79BattlefieldHeldReturnsHeroFromGraveyardToChampionZone`, `P79BattlefieldHeldReturnHeroSkipsOpponentOwnedTomb`, and GameHub `P79BattlefieldHeldReturnHeroSeedOffersBattlefieldDestinationAndReturnsHero` remain the runtime evidence for the held return-hero behavior.
- Representative tests `P79BattlefieldHeldSevenUnitsWinsGame`, `P79BattlefieldHeldSevenUnitsWinCountsOnlyUnitsAtThatBattlefield`, and GameHub `P79BattlefieldHeldSevenUnitsSeedOffersBattlefieldDestinationAndWins` are the runtime evidence for the held seven-units win behavior and its `在此` same-battlefield boundary.
- Existing representative tests `P79BattlefieldConquerRevealRecyclesTopTwo` and GameHub `P79BattlefieldConquerRevealRecycleSeedOffersBattlefieldDestinationAndRecycles` remain the runtime evidence for the conquered-battlefield reveal/recycle representative behavior.
- Existing representative tests `P79BattlefieldConquerMillsTopTwoFromBattlefieldObject` and GameHub `P79BattlefieldConquerMillSeedOffersBattlefieldDestinationAndMills` remain the runtime evidence for the conquered-battlefield mill representative behavior.
- Existing representative tests `P79BattlefieldConquerRecyclesRune`, `P79BattlefieldConquerRecycleRuneSkipsOpponentControlledBaseRune`, and GameHub `P79BattlefieldConquerRecycleRuneSeedOffersBattlefieldDestinationAndRecyclesRune` remain the runtime evidence for the conquered-battlefield recycle-rune representative behavior.
- Existing representative tests `P79BattlefieldConquerConsumesBoonAndDraws`, `P79BattlefieldConquerConsumesControlledBoonWhenDirtyBoonIsOpponentOwned`, and GameHub `P79BattlefieldConquerBoonDrawSeedOffersBattlefieldDestinationAndConsumesBoon` remain the runtime evidence for the conquered-battlefield consume-boon draw representative behavior.
- Existing representative tests `P79BattlefieldConquerDiscardsThenDraws`, `P79BattlefieldConquerDiscardDrawSkipsOpponentControlledHandCard`, and GameHub `P79BattlefieldConquerDiscardDrawSeedOffersBattlefieldDestinationAndCyclesHand` remain the runtime evidence for the conquered-battlefield discard-draw representative behavior.
- Existing representative tests `P79BattlefieldConquerDrawsForOtherControlledBattlefields`, `P79BattlefieldConquerDrawsForOtherBattlefieldsSkipsOpponentOwnedBattlefield`, and GameHub `P79BattlefieldConquerDrawOtherSeedOffersBattlefieldDestinationAndDraws` remain the runtime evidence for the conquered-battlefield draw-for-other-battlefields representative behavior.
- Existing representative tests `P79BattlefieldConquerReadyRunesAtEndSchedulesAndReadiesRunes`, `P79BattlefieldConquerReadyRunesAtEndSkipsOpponentOwnedRune`, and GameHub `P79BattlefieldConquerReadyRunesEndSeedSchedulesAndReadiesRunes` remain the runtime evidence for the conquered-battlefield ready-runes-at-end representative behavior.
- Existing representative tests `P79BattlefieldConquerReadiesAndDetachesEquipment`, `P79BattlefieldConquerReadyEquipmentSkipsOpponentOwnedEquipment`, and GameHub `P79BattlefieldConquerReadyEquipmentSeedOffersBattlefieldDestinationAndDetachesArmament` remain the runtime evidence for the conquered-battlefield ready-equipment representative behavior.
- Existing representative tests `P79BattlefieldConquerGoldOpensPaymentThenPaysOneToCreateDormantGold` and GameHub `P79BattlefieldConquerGoldSeedOffersBattlefieldDestinationAndCreatesGold` remain the runtime evidence for the conquered-battlefield pay-create-gold representative behavior.
- Representative tests `P79BattlefieldConquerPowerfulUnitPaysOneToDraw`, `P79BattlefieldConquerPowerfulDrawCountsAnySurvivingPowerfulAttacker`, and GameHub `P79BattlefieldConquerPowerfulDrawSeedOffersBattlefieldDestinationAndDraws` are the runtime evidence for the conquered-battlefield powerful-unit pay-draw representative behavior.
- Existing representative tests `P79BattlefieldConquerSandSoldierPaysOneReturnsUnitAndCreatesToken`, `P79BattlefieldConquerSandSoldierSkipsWhenManaUnavailable`, and GameHub `P79BattlefieldConquerSandSoldierSeedReturnsUnitAndCreatesToken` remain the runtime evidence for the conquered-battlefield pay-return-unit create-Sand-Soldier representative behavior.
- Existing representative tests `P79BattlefieldConquerReadyLegendPaysOne`, `P79BattlefieldConquerReadyLegendSkipsOpponentOwnedLegend`, and GameHub `P79BattlefieldConquerReadyLegendSeedOffersBattlefieldDestinationAndReadiesLegend` remain the runtime evidence for the conquered-battlefield pay-ready-legend representative behavior.
- Existing representative tests `P79BattlefieldDefendRevealSpellDrawsTopSpell`, `P79BattlefieldDefendRevealSpellRecyclesTopNonSpell`, `P79BattlefieldDefendRevealSpellSkipsOpponentControlledTopCard`, `P79BattlefieldDefendRevealSpellSkipsAttackerControlledBattlefield`, and GameHub `P79BattlefieldDefendRevealSpellSeedOffersBattlefieldDestinationAndDrawsSpell` remain the runtime evidence for the defended-battlefield reveal-spell-or-recycle representative behavior.
- Existing representative tests `P79BattlefieldConquerOverkillCreatesWarhawk` and GameHub `P79BattlefieldConquerWarhawkSeedOffersBattlefieldDestinationAndCreatesWarhawk` remain the runtime evidence for the conquered-battlefield overkill create-Warhawk representative behavior.
- Existing representative tests `P79BattlefieldTurnStartDamageAllBattlefieldUnitsBeforeScoring`, `P79BattlefieldTurnStartDamageSkipsOpponentControlledSource`, and GameHub `P79BattlefieldTurnStartDamageSeedDamagesAndDestroysBeforeRuneCall` remain the runtime evidence for the turn-start damage-units representative behavior.
- Existing representative tests `P79BattlefieldTurnStartDestroyDrawsBeforeScoring`, `P79BattlefieldTurnStartDestroyDrawSkipsOpponentControlledSource`, and GameHub `P79BattlefieldTurnStartDestroyDrawSeedDestroysAndDrawsBeforeRuneCall` remain the runtime evidence for the turn-start destroy-draw representative behavior.
- Existing representative tests `P79BattlefieldStaticFirstTurnRuneCallsOneExtraRune`, `P79BattlefieldStaticFirstTurnRuneIgnoresBattlefieldControlChange`, and GameHub `P79BattlefieldFirstTurnRuneSeedCallsFourthRune` remain the runtime evidence for the first-turn extra-rune representative behavior.
- Existing representative tests `P79BattlefieldStaticFirstTurnScoreGainsOneScore`, `P79BattlefieldStaticFirstTurnScoreIgnoresBattlefieldControlChange`, and GameHub `P79BattlefieldFirstTurnScoreSeedGainsScore` remain the runtime evidence for the first-turn score representative behavior.
- Existing representative tests `P79BattlefieldScoreDelay...`, GameHub `P79BattlefieldScoreDelaySeedPreventsFirstTurnScore`, and the battle-response held-score prevention tests remain the runtime evidence for the score-delay representative behavior.

## Runtime Evidence

The new parser path turns the official text into a structured `TriggerSpec`, including the moved-unit target scope, until-end-of-turn duration and numeric power delta. The runtime no longer checks `OGN·277/298` through `BattlefieldMovedUnitPowerPlusOneCardNo` / `IsBattlefieldMovedUnitPowerPlusOneCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `MOVE_UNIT` path still applies the same server-authoritative mutation after a successful battlefield-origin move. It now emits `BATTLEFIELD_TRIGGER_RESOLVED` and `POWER_MODIFIED_UNTIL_END_OF_TURN` with `BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER` as the trigger / reason, and reads the applied `+1` from the parsed spec.

The 2026-06-25 follow-up parser path turns the Piltover Academy official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO`, `Timing=BATTLEFIELD_HELD`, and `Duration=UNTIL_END_OF_TURN`. The runtime no longer checks `UNL-216/219` through `BattlefieldHeldNextSpellEchoCardNo` / `IsBattlefieldHeldNextSpellEchoCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still stores the same until-end-of-turn marker `BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:{playerId}`. The next spell prompt still offers the existing Echo optional cost, charges extra mana equal to the spell base cost, repeats the stack item, and consumes the marker. Only the source recognition moved from a card-number branch to BehaviorSpec.

The held unit-cost follow-up parser path turns the Vaults of Helia official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE`, `Timing=BATTLEFIELD_HELD`, `Duration=UNTIL_END_OF_TURN`, and `ManaDelta=1`. Runtime no longer checks `UNL-219/219` through `BattlefieldHeldUnitCostIncreaseCardNo` / `IsBattlefieldHeldUnitCostIncreaseCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still stores the existing until-end-of-turn marker shape `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:{playerId}` for the official `+1` case. `CoreRuleEngine` and `MatchSession` now parse the marker for a mana delta, defaulting the compatibility marker to `1` and allowing future larger deltas to be represented in data without adding another card-number branch. `PLAY_CARD` still excludes tokens and writes `battlefieldHeldUnitCostIncreaseMana` into payment metadata and prompt source requirements.

The friendly-spell draw follow-up parser path turns the Dream Tree official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE`, `Timing=BATTLEFIELD_FRIENDLY_SPELL_TARGETED`, `TargetScope=FRIENDLY_UNIT_AT_THIS_BATTLEFIELD`, and `DrawCount=1`. Runtime no longer checks `OGN·292/298` through `BattlefieldFriendlySpellDrawCardNo` / `IsBattlefieldFriendlySpellDrawCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `PLAY_CARD` path still requires a spell targeting a controlled battlefield unit and still records `BATTLEFIELD_FRIENDLY_SPELL_DRAW_USED:{playerId}:{battlefieldObjectId}` until end of turn so the source draws only once. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` payload now carries the parsed trigger kind and `drawCount`, and the draw application reads the count from the parsed spec.

The spell-power bonus follow-up parser path turns the Waste Hall official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_SPELL_POWER_PLUS_1`, `Timing=BATTLEFIELD_SPELL_PLAYED`, `TargetScope=FRIENDLY_UNIT_AT_THIS_BATTLEFIELD`, `PowerDelta=1`, and `Duration=UNTIL_END_OF_TURN`. Runtime no longer checks `UNL-205/219` through `BattlefieldSpellPowerBonusCardNo` / `IsBattlefieldSpellPowerBonusCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `PLAY_CARD` path still requires a spell play, an eligible controlled battlefield source, and a controlled unit at that battlefield. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` payload keeps `BATTLEFIELD_SPELL_POWER_PLUS_1` while sourcing `battlefieldCardNo` and `powerDelta` from the parsed spec-backed source, and the until-end-of-turn power modifier applies that parsed delta.

The high-cost spell insight follow-up parser path turns the Lost Library official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE`, `Timing=BATTLEFIELD_SPELL_PLAYED`, `MinimumPaidMana=4`, and `RecycleCount=1`. Runtime no longer checks `UNL-211/219` through `BattlefieldHighCostSpellInsightCardNo` / `IsBattlefieldHighCostSpellInsightCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `PLAY_CARD` path still requires a spell play, an eligible controlled battlefield source, paid mana at least the parsed threshold, and a controlled main deck card to recycle. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` payload now carries the parsed trigger kind and source `battlefieldCardNo`, and `CARDS_RECYCLED` uses the parsed recycle count while preserving the same hidden deck boundary.

The unit-play boon follow-up parser path turns the Idol Valley official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON`, `Timing=BATTLEFIELD_UNIT_PLAYED`, `TargetScope=PLAYED_UNIT_AT_THIS_BATTLEFIELD`, `ManaCost=1`, and `BoonCount=1`. Runtime no longer checks `UNL-218/219` through `BattlefieldPlayUnitPayOneBoonCardNo` / `IsBattlefieldPlayUnitPayOneBoonCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `PLAY_CARD` path still requires a unit played to the battlefield, an eligible controlled battlefield source, a non-booned controlled source unit, and enough mana to pay the parsed cost. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` and `COST_PAID` payloads now use the parsed trigger kind and mana cost, and the boon grant remains server-authoritative.

The unit-returned call-rune follow-up parser path turns the Ghost Bay official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE`, `Timing=BATTLEFIELD_UNIT_RETURNED`, `TargetScope=RETURNED_UNIT_AT_THIS_BATTLEFIELD`, `ManaCost=1`, and `RuneCallCount=1`. Runtime no longer checks `UNL-214/219` through `BattlefieldUnitReturnedCallRuneCardNo` / `IsBattlefieldUnitReturnedCallRuneCardNo`, and no longer uses `BattlefieldUnitReturnedCallRuneManaCost`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted return-to-hand path still requires a returned unit from that battlefield, an eligible controlled battlefield source, a non-empty rune deck, and enough mana to pay the parsed cost. The emitted `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, and `RUNES_CALLED` payloads now use the parsed trigger kind and parsed values, while the skipped cases for empty rune deck, insufficient mana, and opponent-controlled source remain server-authoritative.

The first-unit-play move-other-to-base follow-up parser path turns the Meteor Spring official text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE`, `Timing=BATTLEFIELD_UNIT_PLAYED`, `TargetScope=OTHER_CONTROLLED_UNIT_AT_THIS_BATTLEFIELD`, `MoveCount=1`, `MoveDestination=OWNER_BASE`, `OncePerTurn=true`, and `ExcludesTokens=true`. Runtime no longer checks `UNL-215/219` through `BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo` / `IsBattlefieldFirstUnitPlayedMoveOtherToBaseCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `PLAY_CARD` stack-resolution path still requires a non-token unit played to a battlefield, an eligible controlled battlefield source, no matching once-per-turn marker for that player/source, and another controlled unit to move. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` and `UNIT_MOVED_TO_BASE` payloads now use the parsed trigger kind, while the used marker remains `BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE_USED:{playerId}:{battlefieldObjectId}` for compatibility.

The held draw-one follow-up parser path turns the official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_DRAW_ONE`, `Timing=BATTLEFIELD_HELD`, and `DrawCount=1`. Runtime no longer checks `OGN·280/298` through `BattlefieldHoldDrawCardNo` / `IsBattlefieldHoldDrawCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still requires an eligible controlled battlefield source and applies the authoritative draw path through `ApplyDrawToPlayer`. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` payload now uses the parsed trigger kind and parsed draw count, preserving hidden main-deck ordering boundaries through the existing draw implementation.

The held call-rune follow-up parser path turns the Star Peak official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_CALL_RUNE`, `Timing=BATTLEFIELD_HELD`, and `RuneCallCount=1`. Runtime no longer checks `OGN·288/298` through `BattlefieldHoldCallRuneCardNo` / `IsBattlefieldHoldCallRuneCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still requires an eligible controlled battlefield source and applies the authoritative rune-call path through `CallRunes`. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` payload now uses the parsed trigger kind and the rune-call count is read from the parsed spec. This preserves the existing representative auto-resolution path; optional trigger choice prompts remain outside this slice.

The held each-player call-rune follow-up parser path turns the Confetti Tree official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_EACH_PLAYER_CALL_RUNE`, `Timing=BATTLEFIELD_HELD`, `TargetScope=EACH_PLAYER`, and `RuneCallCount=1`. Runtime no longer checks `SFD·219/221` through `BattlefieldHoldEachPlayerCallRuneCardNo` / `IsBattlefieldHoldEachPlayerCallRuneCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still requires an eligible controlled battlefield source and applies the authoritative rune-call path through `CallRunes` for the holder and the other player. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` payload now uses the parsed trigger kind, and each `RUNES_CALLED` event uses the parsed rune-call count while preserving per-player hidden rune-deck boundaries.

The held move-unit-to-base follow-up parser path turns the Rehearsal Hall official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE`, `Timing=BATTLEFIELD_HELD`, `TargetScope=UNIT_AT_THIS_BATTLEFIELD`, `MoveCount=1`, and `MoveDestination=OWNER_BASE`. Runtime no longer checks `UNL-207/219` through `BattlefieldHeldMoveUnitToBaseCardNo` / `IsBattlefieldHeldMoveUnitToBaseCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still requires an eligible controlled battlefield source and moves the first legal battlefield unit target to its owner's base through the existing server-authoritative move helper. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` and `UNIT_MOVED_TO_BASE` payloads now use the parsed trigger kind, while target visibility and destination legality remain covered by existing GameHub and move-unit prompt tests.

The held grant-boon follow-up parser path turns the Navori Arena official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_GRANT_BOON`, `Timing=BATTLEFIELD_HELD`, `TargetScope=UNIT_AT_THIS_BATTLEFIELD`, and `BoonCount=1`. Runtime no longer checks `OGN·283/298` through `BattlefieldHoldGrantBoonCardNo` / `IsBattlefieldHoldGrantBoonCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still requires an eligible controlled battlefield source and grants one boon to the existing auto-selected surviving battlefield unit target through the server-authoritative boon helper. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` and `BOON_GRANTED` payloads now use the parsed trigger kind, while the broader optional target-choice prompt remains outside this narrow routing slice.

The held create-minion follow-up parser path turns the Unity Sanctum official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_CREATE_MINION`, `Timing=BATTLEFIELD_HELD`, `CreatedTokenCount=1`, `CreatedTokenName=随从`, `CreatedTokenPower=1`, and `CreatedTokenDestination=OWNER_BASE`. Runtime no longer checks `OGN·275/298` through `BattlefieldHoldCreateMinionCardNo` / `IsBattlefieldHoldCreateMinionCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still requires an eligible controlled battlefield source and creates the token in the holder's base through the server-authoritative token factory path. The concrete token card is resolved from `P6TokenFactoryCatalog` by token family, power and unit tag, preserving the current `OGN·271/298` output while removing the trigger source's card-number branch. Optional trigger choice prompts and broader token-family disambiguation remain outside this narrow routing slice.

The held return-hero follow-up parser path turns the Hallowed Tomb official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD`, `Timing=BATTLEFIELD_HELD`, `TargetScope=OWNED_HERO_UNIT_IN_GRAVEYARD`, `ReturnCount=1`, `RequiredEmptyZone=CHAMPION`, `ReturnOriginZone=GRAVEYARD`, `ReturnDestinationZone=CHAMPION`, and `ReturnCardFilter=TAG:CARD_CATEGORY:英雄单位`. Runtime no longer checks `OGN·281/298` through `BattlefieldHeldReturnHeroCardNo` / `IsBattlefieldHeldReturnHeroCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still requires an eligible controlled battlefield source, an empty holder champion zone, and an owned hero unit card in that player's graveyard. The returned object is moved from graveyard to champion zone, damage/exhaustion/combat flags are cleared, and the emitted `BATTLEFIELD_TRIGGER_RESOLVED` / `UNIT_RETURNED_TO_CHAMPION_ZONE` payloads use the parsed trigger kind and parsed origin/destination zones. Optional trigger choice prompts remain outside this narrow routing slice.

The held seven-units win follow-up parser path turns the Grand Plaza official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_HELD_SEVEN_UNITS_WIN`, `Timing=BATTLEFIELD_HELD`, `TargetScope=CONTROLLED_UNITS_AT_THIS_BATTLEFIELD`, `RequiredUnitCount=7`, and `WinsGame=true`. Runtime no longer checks `OGN·293/298` / `OGN·293a/298` through `BattlefieldHeldSevenUnitsWinCardNo` / `BattlefieldHeldSevenUnitsWinAltCardNo` / `IsBattlefieldHeldSevenUnitsWinCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` held-battlefield path still requires an eligible controlled battlefield source and now counts only controlled, face-up, non-standby units whose `ObjectLocations.BattlefieldObjectId` equals the held battlefield object. The emitted `BATTLEFIELD_TRIGGER_RESOLVED` / `MATCH_WON` payloads use the parsed trigger kind and parsed required count. `P79BattlefieldHeldSevenUnitsWinCountsOnlyUnitsAtThatBattlefield` proves that seven controlled battlefield-zone units split across different battlefields do not satisfy the official `在此` text.

The conquer reveal/recycle follow-up parser path turns the Candlelit Sanctum official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE`, `Timing=BATTLEFIELD_CONQUERED`, `RevealCount=2`, `RevealSourceZone=MAIN_DECK`, `RecycleCount=2`, and `RecycleDestinationZone=MAIN_DECK`. Runtime no longer checks `OGN·291/298` through `BattlefieldConquerRevealRecycleCardNo` / `IsBattlefieldConquerRevealRecycleCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path still keeps the existing deterministic representative behavior: it reveals the top two controlled main-deck cards, recycles the parsed count, emits `BATTLEFIELD_TRIGGER_RESOLVED`, `CARDS_REVEALED`, and `CARDS_RECYCLED` with `BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE`, and preserves hidden main-deck ordering boundaries. The official optional choice of any number and arbitrary order for non-recycled cards remains outside this narrow routing slice.

The conquer mill follow-up parser path turns the Minefield official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_MILL_TOP_TWO`, `Timing=BATTLEFIELD_CONQUERED`, `MillCount=2`, `MillSourceZone=MAIN_DECK`, and `MillDestinationZone=GRAVEYARD`. Runtime no longer checks `SFD·212/221` through `BattlefieldConquerMillTwoCardNo` / `IsBattlefieldConquerMillTwoCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path still moves the top controlled main-deck cards into the conquering player's graveyard and emits `BATTLEFIELD_TRIGGER_RESOLVED` plus `CARDS_MILLED` with `BATTLEFIELD_CONQUERED_MILL_TOP_TWO`. The mill count and source/destination zones now come from the parsed spec while preserving existing hidden main-deck ordering boundaries.

The conquer recycle-rune follow-up parser path turns the Sigil of Thunder official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_RECYCLE_RUNE`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=OWNED_RUNE_IN_BASE`, `RecycleCount=1`, `RecycleSourceZone=BASE`, and `RecycleDestinationZone=MAIN_DECK`. Runtime no longer checks `OGN·287/298` through `BattlefieldConquerRecycleRuneCardNo` / `IsBattlefieldConquerRecycleRuneCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path still selects a controlled rune in the conquering player's base, moves it to the bottom of that player's main deck, and emits `BATTLEFIELD_TRIGGER_RESOLVED` plus `CARDS_RECYCLED` with `BATTLEFIELD_CONQUERED_RECYCLE_RUNE`. The recycle count and source/destination zones now come from the parsed spec, and the existing opponent-controlled base rune guard remains covered.

The conquer consume-boon draw follow-up parser path turns the Shirana Monastery official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=CONTROLLED_BOON_UNIT_ON_FIELD`, `ConsumedBoonCount=1`, and `DrawCount=1`. Runtime no longer checks `OGN·282/298` through `BattlefieldConquerConsumeBoonDrawCardNo` / `IsBattlefieldConquerConsumeBoonDrawCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path keeps the existing representative auto-resolution behavior for this optional trigger: when a controlled boon unit is available, it consumes one boon, reduces that unit's power by one, draws the parsed count, and emits `BATTLEFIELD_TRIGGER_RESOLVED`, `BOON_CONSUMED`, and `CARD_DRAWN` with `BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW`. The opponent-controlled dirty boon guard remains covered; the broader optional yes/no prompt remains outside this narrow routing slice.

The conquer discard-draw follow-up parser path turns the Zaun Sump official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_DISCARD_DRAW`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=CONTROLLED_HAND_CARD`, `DiscardCount=1`, `DiscardSourceZone=HAND`, `DiscardDestinationZone=GRAVEYARD`, and `DrawCount=1`. Runtime no longer checks `OGN·298/298` through `BattlefieldConquerDiscardDrawCardNo` / `IsBattlefieldConquerDiscardDrawCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path keeps the existing representative auto-resolution behavior: it discards the first controlled hand card when one is available, triggers the existing Jinx discard hook, draws the parsed count, and emits `BATTLEFIELD_TRIGGER_RESOLVED`, `CARD_DISCARDED`, and `CARD_DRAWN` with `BATTLEFIELD_CONQUERED_DISCARD_DRAW`. The opponent-controlled dirty hand-card guard remains covered; broader discard choice prompting remains outside this narrow routing slice.

The conquer draw-for-other-battlefields follow-up parser path turns the Seat of Power official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=OTHER_CONTROLLED_BATTLEFIELDS`, and `DrawCountPerParticipant=1`. Runtime no longer checks `SFD·217/221` through `BattlefieldConquerDrawForOtherBattlefieldsCardNo` / `IsBattlefieldConquerDrawForOtherBattlefieldsCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path still counts other controlled battlefield card objects, excludes the conquered source battlefield object, skips opponent-owned dirty battlefield objects, and draws `otherBattlefieldObjectIds.Length * DrawCountPerParticipant`. It emits `BATTLEFIELD_TRIGGER_RESOLVED` and `CARD_DRAWN` with `BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS` while preserving hidden main-deck ordering boundaries through the existing draw implementation.

The conquer ready-runes-at-end follow-up parser path turns the Mount Targon official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_READY_RUNES_AT_END`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=OWNED_RUNE_IN_BASE`, `RuneReadyCount=2`, and `ReadyTiming=END_OF_TURN`. Runtime no longer checks `OGN·289/298` through `BattlefieldConquerReadyTwoRunesAtEndCardNo` / `IsBattlefieldConquerReadyTwoRunesAtEndCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path still schedules the existing delayed end-of-turn ready effects for owned base runes, skips opponent-owned dirty rune objects in the base zone, and then `END_TURN` resolves those markers through the existing `BATTLEFIELD_END_TURN_READY_RUNES` path. It emits `BATTLEFIELD_TRIGGER_RESOLVED` and `RUNE_READY_SCHEDULED` with `BATTLEFIELD_CONQUERED_READY_RUNES_AT_END`, while preserving the existing end-turn marker shape and snapshot hidden-information boundaries.

The conquer ready-equipment follow-up parser path turns the Moonveil Altar official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_READY_EQUIPMENT`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=FRIENDLY_EQUIPMENT`, `EquipmentReadyCount=1`, and `DetachesArmament=true`. Runtime no longer checks `SFD·221/221` through `BattlefieldConquerReadyEquipmentCardNo` / `IsBattlefieldConquerReadyEquipmentCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path still readies the first controlled exhausted equipment in the conquering player's base / battlefield zones, skips opponent-owned dirty equipment objects, and detaches the target when the parsed trigger allows armament detach and the target is attached armament. It emits `BATTLEFIELD_TRIGGER_RESOLVED`, `EQUIPMENT_READIED`, and, when applicable, `EQUIPMENT_DETACHED` with `BATTLEFIELD_CONQUERED_READY_EQUIPMENT`, while preserving existing equipment ownership / controller guards.

The conquer pay-create-gold follow-up parser path turns the Treasure Pile official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD`, `Timing=BATTLEFIELD_CONQUERED`, `ManaCost=1`, `CreatedTokenCount=1`, `CreatedTokenName=金币`, `CreatedTokenDestination=OWNER_BASE`, and `CreatedTokenExhausted=true`. Runtime no longer checks `SFD·220/221` through `BattlefieldConquerPayOneCreateGoldCardNo` / `IsBattlefieldConquerPayOneCreateGoldCardNo`, and no longer uses `BattlefieldGoldManaCost`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path still opens a `TRIGGER_PAYMENT` prompt, accepts `SPEND_MANA:1` or `DECLINE`, and creates an exhausted Gold equipment token after successful payment. The prompt cost, trigger id, token name, token destination and exhausted state now come from the parsed spec while preserving the existing `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, and `EQUIPMENT_TOKEN_CREATED` event contract and hidden information boundaries.

The conquer powerful pay-draw follow-up parser path turns the Sunken Temple official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=SURVIVING_POWERFUL_UNIT_AT_THIS_BATTLEFIELD`, `RequiredPowerThreshold=5`, `ManaCost=1`, and `DrawCount=1`. Runtime no longer checks `SFD·218/221` through `BattlefieldConquerPowerfulPayOneDrawCardNo` / `IsBattlefieldConquerPowerfulPayOneDrawCardNo`, and no longer uses `BattlefieldPowerfulDrawManaCost`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path still opens a `TRIGGER_PAYMENT` prompt, accepts `SPEND_MANA:1` or `DECLINE`, and draws after successful payment. The prompt cost, trigger id, draw count, and required power threshold now come from the parsed spec. The new multi-attacker regression proves the official `此战场上留存至少一名{{强力}}单位` condition is evaluated over all surviving conquest attackers instead of only the first attacker object.

The conquer pay-return-unit create-Sand-Soldier follow-up parser path turns the Imperial Shrine official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=CONTROLLED_UNIT_AT_THIS_BATTLEFIELD`, `ManaCost=1`, `ReturnCount=1`, `ReturnOriginZone=BATTLEFIELD`, `ReturnDestinationZone=HAND`, `CreatedTokenCount=1`, `CreatedTokenName=黄沙士兵`, `CreatedTokenPower=2`, `CreatedTokenDestination=BATTLEFIELD`, and `CreatedTokenExhausted=false`. Runtime no longer checks `SFD·207/221` through `BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo` / `IsBattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo`, and no longer uses `BattlefieldSandSoldierManaCost`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path keeps the existing representative auto-resolution behavior: when mana and a controlled unit are available, it pays the parsed cost, returns the selected unit to its owner's hand, resolves existing unit-returned battlefield hooks, and creates the parsed 2-power Sand Soldier token at that battlefield. The concrete token card now resolves from `P6TokenFactoryCatalog` by parsed token family and power instead of hardcoding the trigger source card number or payment cost.

The conquer pay-ready-legend follow-up parser path turns the Hall of Legends official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND`, `Timing=BATTLEFIELD_CONQUERED`, `TargetScope=CONTROLLED_LEGEND`, `ManaCost=1`, and `LegendReadyCount=1`. Runtime no longer checks `SFD·210/221` through `BattlefieldConquerPayOneReadyLegendCardNo` / `IsBattlefieldConquerPayOneReadyLegendCardNo`, and no longer uses `BattlefieldReadyLegendManaCost`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path keeps the existing representative auto-resolution behavior: when mana and an exhausted controlled legend are available, it pays the parsed cost and readies one controlled legend. The trigger id, mana cost, controlled-legend target scope and ready count now come from the parsed spec while the existing opponent-owned legend guard remains covered.

The defend reveal-spell-or-recycle follow-up parser path turns the Ravenbloom Conservatory official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE`, `Timing=BATTLEFIELD_DEFENDED`, `RevealCount=1`, `RevealSourceZone=MAIN_DECK`, `RevealMatchCardFilter=TAG:CARD_TYPE:SPELL`, `RevealMatchDestinationZone=HAND`, and `RevealMissDestinationZone=MAIN_DECK`. Runtime no longer checks `SFD·215/221` through `BattlefieldDefendRevealSpellCardNo` / `IsBattlefieldDefendRevealSpellCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` defended-battlefield path keeps the existing representative auto-resolution behavior: the defending player reveals the top controlled main-deck card, moves it to hand when it matches the parsed spell-card filter, otherwise recycles it to the parsed miss destination. The opponent-controlled dirty top-card guard and attacker-controlled battlefield source guard remain covered.

The conquer overkill create-Warhawk follow-up parser path turns the Hunting Grounds official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_CONQUERED_OVERKILL_CREATE_WARHAWK`, `Timing=BATTLEFIELD_CONQUERED`, `RequiredOverkillDamage=3`, `CreatedTokenCount=1`, `CreatedTokenName=战鹰`, `CreatedTokenPower=1`, `CreatedTokenDestination=BATTLEFIELD`, and `CreatedTokenKeywords=[法盾]`. Runtime no longer checks `UNL-217/219` through `BattlefieldConquerOverkillCreateWarhawkCardNo` / `IsBattlefieldConquerOverkillCreateWarhawkCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `DECLARE_BATTLE` conquered-battlefield path keeps the existing representative auto-resolution behavior: when assigned overkill damage to enemy units reaches the parsed threshold, it creates the parsed Warhawk token on the battlefield. The concrete token resolves by parsed token family and power through `P6TokenFactoryCatalog`, and the parsed `法盾` keyword is checked against the token definition tags before creation.

The turn-start damage-units follow-up parser path turns the Frost Hold official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS`, `Timing=TURN_START`, `TargetScope=UNIT_AT_THIS_BATTLEFIELD`, and `DamageAmount=1`. Runtime no longer checks `UNL-212/219` through `BattlefieldTurnStartDamageAllUnitsCardNo` / `IsBattlefieldTurnStartDamageAllUnitsCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `START_TURN` path keeps the existing start-phase timing before scoring and rune-call progression. It now derives affected units from the source battlefield object's location scope, so the official `此处` wording damages only units at that battlefield object rather than every battlefield-zone unit. The same source-controlled guard remains in place for dirty-state protection.

The turn-start destroy-draw follow-up parser path turns the Duskpetal Lab official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW`, `Timing=TURN_START`, `TargetScope=CONTROLLED_UNIT_AT_THIS_BATTLEFIELD`, `DestroyCount=1`, `DrawCount=1`, and `Optional=true`. Runtime no longer checks `UNL-209/219` through `BattlefieldTurnStartDestroyUnitDrawCardNo` / `IsBattlefieldTurnStartDestroyUnitDrawCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `START_TURN` path keeps the existing auto-representative optional-trigger behavior and still resolves before scoring, rune-call progression and normal turn draw. It now chooses the destroyed unit from the source battlefield object's location scope, so a controlled unit at another battlefield object is preserved under the official `此处` boundary.

The first-turn extra-rune follow-up parser path turns the Power Obelisk official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_FIRST_TURN_EXTRA_RUNE`, `Timing=TURN_START`, `TargetScope=EACH_PLAYER`, `RuneCallCount=1`, and `FirstTurnOnly=true`. Runtime no longer checks `OGN·284/298` through `BattlefieldFirstTurnExtraRuneCardNo` / `IsBattlefieldFirstTurnExtraRuneCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `START_TURN` path keeps the existing representative behavior: while the battlefield object is present, each player's own first turn-start rune call adds the parsed rune count to the normal turn-start rune call count. Existing dirty-control evidence remains unchanged because the official text is a global battlefield rule and not a controller-gated source.

The first-turn score follow-up parser path turns the Glory Arena official battlefield text into a structured `TriggerSpec` with `Kind=BATTLEFIELD_FIRST_TURN_GAIN_SCORE`, `Timing=TURN_START`, `TargetScope=EACH_PLAYER`, `FirstTurnOnly=true`, and `ScoreAmount=1`. Runtime no longer checks `OGN·290/298` through `BattlefieldFirstTurnScoreCardNo` / `IsBattlefieldFirstTurnScoreCardNo`; it queries `BehaviorSpec.Triggers` via `BattlefieldTriggerSpecRules`.

The accepted `START_TURN` path keeps the existing representative behavior: while the battlefield object is present, each player's own first turn-start score step grants the parsed score amount and emits the existing `BATTLEFIELD_TRIGGER_RESOLVED` / `SCORE_GAINED` payloads with `BATTLEFIELD_FIRST_TURN_GAIN_SCORE`. Existing dirty-control evidence remains unchanged because the official text is a global battlefield rule and not a controller-gated source.

The score-delay follow-up parser path turns the Forgotten Monument official battlefield text into a structured `StaticAbilitySpec` with `Kind=BATTLEFIELD_SCORE_DELAY_UNTIL_TURN` and `Amount=3`. Runtime no longer checks `SFD·209/221` through `BattlefieldScoreDelayCardNo` / `IsBattlefieldScoreDelayCardNo` or a fixed `BattlefieldScoreDelayReleasedTurnOrdinal`; it queries `BehaviorSpec.StaticAbilities` via `BattlefieldStaticAbilitySpecRules`.

The accepted score-prevention path keeps the existing representative behavior: while an eligible score-delay battlefield object is present and the scoring player's turn ordinal is below the parsed release turn, battlefield-origin score is prevented and the existing `BATTLEFIELD_SCORE_PREVENTED` payload shape is preserved, including `trigger=BATTLEFIELD_SCORE_DELAY_UNTIL_THIRD_TURN`. This slice does not claim complete physical `此处` scoping; the current flat battlefield representative remains documented as non-closure.

## Hidden Information Evidence

No snapshot hidden-zone logic was changed. The representative GameHub and MatchRecovery validation still cover prompt/snapshot boundaries; MatchRecovery passed `1989/1989`.

## Validation

- moved-unit focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- moved-unit adjacent BattlefieldMoved / BattlefieldMovePower / MoveUnit / BoardTaskQueue / FullGame / GameHub: `326/326`;
- held-next-spell Echo focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- held-next-spell Echo adjacent BattlefieldHeld / BattlefieldTriggerSpec / BattlefieldMovedUnitPower / BattlefieldMovePower / GameHub battlefield representatives: `102/102`;
- held unit-cost increase focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- held unit-cost increase adjacent BattlefieldHeld / BattlefieldTriggerSpec / PaymentEngine / PlayCard / GameHub battlefield representatives / BoardTaskQueue / FullGame: `1156/1156`;
- friendly-spell draw focused behavior-spec/source guard/runtime representative: `5/5`;
- friendly-spell draw adjacent BattlefieldFriendlySpellDraw / BattlefieldFriendlySpellTarget / BattlefieldTriggerSpec / held-trigger representatives: `12/12`;
- spell-power bonus focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- spell-power bonus adjacent BattlefieldSpellPowerBonus / BattlefieldTriggerSpec / recently migrated battlefield trigger representatives: `21/21`;
- high-cost spell insight focused behavior-spec/source guard/runtime/GameHub representative: `6/6`;
- high-cost spell insight adjacent BattlefieldHighCostSpellInsight / BattlefieldTriggerSpec / BattlefieldSpellPowerBonus / BattlefieldFriendlySpellDraw / P6 battlefield surface representatives: `15/15`;
- unit-play boon focused behavior-spec/source guard/runtime representative: `6/6`;
- unit-play boon adjacent BattlefieldPlayUnitBoon / BattlefieldTriggerSpec / PlayCard / Boon / GameHub / P6 battlefield surface representatives: `331/331`;
- unit-returned call-rune focused behavior-spec/source guard/runtime/GameHub representative: `6/6`;
- unit-returned call-rune adjacent BattlefieldReturnCallRune / BattlefieldReturnedUnit / BattlefieldTriggerSpec / recent battlefield trigger representatives / call-rune representatives: `23/23`;
- first-unit-play move-other-to-base focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- first-unit-play move-other-to-base adjacent BattlefieldFirstUnit / BattlefieldPlayUnitBoon / BattlefieldTriggerSpec / MoveUnit / PlayCard / GameHub representatives: `342/342`;
- held draw-one focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- held draw-one adjacent BattlefieldHeld / BattlefieldTriggerSpec / Dunehorn / held-trigger GameHub representatives: `63/63`;
- held call-rune focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- held call-rune adjacent BattlefieldHeld / BattlefieldTriggerSpec / BattlefieldReturnCallRune representatives: `64/64`;
- held each-player call-rune focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- held each-player call-rune adjacent BattlefieldHeld / BattlefieldTriggerSpec / CallRune / Runes representatives: `103/103`;
- held move-unit-to-base focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- held move-unit-to-base adjacent BattlefieldHeld / BattlefieldTriggerSpec / MoveUnit / FullGame / GameHub representatives: `360/360`;
- held grant-boon focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- held grant-boon adjacent BattlefieldHeld / BattlefieldTriggerSpec / Boon / FullGame / GameHub representatives: `360/360`;
- held create-minion focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- held create-minion adjacent BattlefieldHeld / BattlefieldTriggerSpec / Token / FullGame / GameHub representatives: `349/349`;
- held return-hero focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- held return-hero adjacent BattlefieldHeld / BattlefieldTriggerSpec / ChampionZone / FullGame / GameHub representatives: `290/290`;
- held seven-units win focused behavior-spec/source guard/runtime/GameHub representative: `7/7`;
- held seven-units win adjacent BattlefieldHeld / BattlefieldTriggerSpec / FullGame / GameHub representatives: `293/293`;
- conquer reveal/recycle focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- conquer reveal/recycle adjacent BattlefieldConquer / BattlefieldTriggerSpec / FullGame / GameHub representatives: `272/272`;
- conquer mill focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- conquer mill adjacent BattlefieldConquer / BattlefieldTriggerSpec / FullGame / GameHub representatives: `274/274`;
- conquer recycle-rune focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- conquer recycle-rune adjacent BattlefieldConquer / BattlefieldTriggerSpec / FullGame / GameHub representatives: `276/276`;
- conquer consume-boon draw focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- conquer consume-boon draw adjacent BattlefieldConquer / BattlefieldTriggerSpec / Boon / FullGame / GameHub representatives: `357/357`;
- conquer discard-draw focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- conquer discard-draw adjacent BattlefieldConquer / BattlefieldTriggerSpec / Discard / Jinx / FullGame / GameHub representatives: `330/330`;
- conquer draw-for-other-battlefields focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- conquer draw-for-other-battlefields adjacent BattlefieldConquer / BattlefieldTriggerSpec / FullGame / GameHub representatives: `282/282`;
- conquer ready-runes-at-end focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- conquer ready-runes-at-end adjacent BattlefieldConquer / BattlefieldTriggerSpec / Rune / FullGame / GameHub representatives: `458/458`;
- conquer ready-equipment focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- conquer ready-equipment adjacent BattlefieldConquer / BattlefieldTriggerSpec / Equipment / FullGame / GameHub representatives: `750/750`;
- conquer pay-create-gold focused behavior-spec/source guard/runtime/GameHub representative: `4/4`;
- conquer pay-create-gold adjacent BattlefieldConquer / TriggerPayment representatives: `130/130`;
- conquer powerful pay-draw focused behavior-spec/source guard/runtime representatives: `4/4`;
- conquer powerful pay-draw GameHub seed representative: `1/1`;
- conquer powerful pay-draw adjacent BattlefieldConquer / TriggerPayment / BattlefieldTriggerSpec representatives: `91/91`;
- conquer pay-return-unit create-Sand-Soldier focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- conquer pay-return-unit create-Sand-Soldier adjacent BattlefieldConquer / TriggerPayment / BattlefieldTriggerSpec / SandSoldier representatives: `141/141`;
- conquer pay-ready-legend focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- conquer pay-ready-legend adjacent BattlefieldConquer / BattlefieldTriggerSpec / ReadyLegend / LegendReadied representatives: `81/81`;
- defend reveal-spell-or-recycle focused behavior-spec/source guard/runtime/GameHub representative: `7/7`;
- defend reveal-spell-or-recycle adjacent BattlefieldDefend / BattlefieldDefender / BattlefieldTriggerSpec / RevealCard / DeclareBattle representatives: `188/188`;
- conquer overkill create-Warhawk focused behavior-spec/source guard/runtime/GameHub representative: `3/3`;
- conquer overkill create-Warhawk adjacent BattlefieldConquer / BattlefieldTriggerSpec / Overkill / Warhawk / DeclareBattle representatives: `221/221`;
- turn-start damage-units focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- turn-start damage-units CardCatalog baseline: `147/147`;
- turn-start damage-units adjacent BattlefieldTurnStartDamage / BattlefieldTriggerSpec / GameHub representatives: `226/226`;
- turn-start damage-units FullGame representatives: `7/7`;
- turn-start destroy-draw focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- turn-start destroy-draw CardCatalog baseline: `149/149`;
- turn-start destroy-draw adjacent BattlefieldTurnStart / BattlefieldTriggerSpec / GameHub representatives: `230/230`;
- first-turn extra-rune focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- first-turn extra-rune CardCatalog baseline: `151/151`;
- first-turn extra-rune adjacent FirstTurnRune / BattlefieldFirstTurn / BattlefieldTriggerSpec / GameHub representatives: `226/226`;
- first-turn score focused behavior-spec/source guard/runtime/GameHub representative: `5/5`;
- first-turn score CardCatalog baseline: `153/153`;
- first-turn score adjacent FirstTurnScore / ScoreDelay / BattlefieldTriggerSpec / GameHub representatives: `229/229`;
- score-delay focused behavior-spec/source guard/runtime representatives: `6/6`;
- score-delay CardCatalog baseline: `155/155`;
- score-delay adjacent ScoreDelay / ScorePrevented / BattlefieldHeldScore / BattleResponse / BattlefieldTriggerSpec / GameHub representatives: `290/290`;
- MatchRecovery: `1989/1989`;
- backend full conformance after the conquer overkill create-Warhawk follow-up: `8436/8436`;
- backend full conformance after the turn-start damage-units follow-up: `8438/8438`;
- backend full conformance after the turn-start destroy-draw follow-up: `8440/8440`;
- backend full conformance after the first-turn extra-rune follow-up: `8442/8442`;
- backend full conformance after the first-turn score follow-up: `8444/8444`;
- backend full conformance after the score-delay follow-up: `8446/8446`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.
- score-delay DevUi build: not run; this follow-up did not change DevUi source or catalog TypeScript shape.

## Non-Closure

This evidence proves thirty-five battlefield trigger representatives plus one battlefield static score-delay representative have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, all trigger timing windows, all movement / control-zone edge cases, optional trigger choice prompts, complete `此处` physical battlefield scoping for score prevention, B0 full real-deck end-to-end game completion, all card-effect families, frontend smoke or READY.
