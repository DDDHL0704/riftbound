# Plan B / B3 Unit Conquest Trigger Spec Evidence

Date: 2026-06-29

Project status: **NOT READY**.

2026-06-30 follow-up: Vayne pay-return pending-payment reason parsing and runtime trigger/reason payloads now validate through `UnitConquestTriggerSpecRules.TryGetTriggerByEffectKind(...)` plus `IsUnitConquestPayReturnSelfToHandTrigger(...)`. `CoreRuleEngine` no longer owns `UnitConquestPayReturnSelfToHandEffectKind`; the wire-compatible `UNIT_CONQUEST_PAY_1_RETURN_SELF_TO_HAND` payload remains unchanged.

2026-07-01 follow-up: unit-conquest runtime routing no longer exposes or calls public `TryGetUnitConquest*` helper methods. `CoreRuleEngine` enumerates `UnitConquestTriggerSpecRules.TriggersForCard(...)`, filters auto-resolvable effects with `IsSupportedUnitConquestTrigger(...)`, and uses the same generic `TryGetTrigger(...)` / `TryGetTriggerByEffectKind(...)` path for Vayne pay-return payment validation.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·039/298` 卡莎 has official text `{{急速}}（你可以选择额外支付{{1}}和{{红色}}，让我以活跃状态进场。）\n当我征服一处战场时，抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `OGN·039a/298` 卡莎 has the same official text.
- `data/official/card-catalog.zh-CN.json`: `OGN·155/298` 奇亚娜 has official text `{{法盾}}（对手必须支付{{A}}才能将我选作法术或技能的目标。）\n当我征服一处战场时，抽一张牌或召出一枚休眠的符文。`
- `data/official/card-catalog.zh-CN.json`: `UNL-222/219` 坏坏魄罗 has official text `当我征服一处战场时，打出一个休眠的“金币”装备指示物。`
- `data/official/card-catalog.zh-CN.json`: `SFD·069/221` 坏坏魄罗 has the same official text.
- `data/official/card-catalog.zh-CN.json`: `UNL-018/219` 雪人斗士 has official text `当我征服一处战场时，如果你给敌方单位分配了不低于3点的过量伤害，则打出两个休眠的“金币”装备指示物。（其具有“{{反应>}} 摧毁此牌，{{横置}}：{{获得}}{{A}}。”）`
- `data/official/card-catalog.zh-CN.json`: `OGN·034/298` 泰达米尔 has official text `当我通过进攻征服一处战场时，如果你给敌方单位造成过不低于5点的过量伤害，则你获得的分数+1。`
- `data/official/card-catalog.zh-CN.json`: `OGN·035/298` 薇恩 has official text `{{强攻3}}\n如果对手已控制任意战场，则我会以活跃状态进场。\n每当我征服一处战场时，你可以选择支付{{1}}来让我返回所属的手牌。`
- `data/official/card-catalog.zh-CN.json`: `SFD·223/221` and `SFD·223*/221` 薇恩 have official text `{{哨兵}}\n{{强攻3}}\n如果对手已控制任意战场，则我会以活跃状态进场。\n每当我征服一处战场时，你可以选择支付{{1}}来让我返回所属的手牌。`
- `data/official/card-catalog.zh-CN.json`: `SFD·232/221` 瑟提 has official text `当我被打出时、或当我征服一处战场时，给予我增益。（如果我未拥有增益，则获得一个{{S}}+1增益。）\n消耗我的增益：让我本回合内{{S}}+4。`
- `data/official/card-catalog.zh-CN.json`: `SFD·232*/221`, `OGN·164/298`, and `OGN·164a/298` 瑟提 have the same unit conquest self-boon text.
- `data/official/card-catalog.zh-CN.json`: `SFD·113/221` 卢锡安 has official text `{{百炼}}（当你打出我时，你可以选择为我{{装配}}你的一件武装，其装配费用减少{{A}}。可选择已贴附的武装。）\n每回合首次，当我征服一处战场时，让我变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `SFD·113a/221` 卢锡安 has the same unit conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `UNL-029/219` 绯红印记树怪 has official text `{{急速}}（你可以选择额外支付{{1}}和{{红色}}，让我以活跃状态进场。）\n你征服此处时的征服效果额外触发一次。\n当我征服一处战场时，给予一名友方单位{{增益}}。（如果其未拥有增益，则获得一个{{S}}+1增益。）`
- `data/official/card-catalog.zh-CN.json`: `UNL-029a/219` 绯红印记树怪 has the same unit conquest friendly-boon text.
- `data/official/card-catalog.zh-CN.json`: `UNL-027/219` 天声玄龙 has official text `当我征服一处战场时，让一名友方单位本回合内{{S}}+8。`
- `data/official/card-catalog.zh-CN.json`: `OGN·056/298` 自适应机器人 has official text `当我征服一处战场时，你可以选择摧毁一件装备，以此给予我增益。（如果我未拥有增益，则获得一个{{S}}+1增益。）`
- `data/official/card-catalog.zh-CN.json`: `OGN·286/298` 清算人竞技场 has official text `当你据守此处时，激活此处所有单位的征服效果。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog text remains the local rule authority input for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesUnitConquestDrawOneTrigger(OGN·039/298)` verifies that 卡莎's official unit text parses to `TriggerSpec.Kind = UNIT_CONQUEST_DRAW_ONE`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, and `DrawCount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestDrawOneTrigger(OGN·039a/298)` verifies the same shape for the alternate print.
- `BehaviorSpecCatalogParsesUnitConquestDrawOneOrCallRuneTrigger` verifies that 奇亚娜's official unit text parses to `TriggerSpec.Kind = UNIT_CONQUEST_DRAW_ONE_OR_CALL_RUNE`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, `DrawCount = 1`, and `RuneCallCount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestCreateDormantGoldTrigger(UNL-222/219)` verifies that 坏坏魄罗's official unit text parses to `TriggerSpec.Kind = UNIT_CONQUEST_CREATE_DORMANT_GOLD`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, `CreatedTokenCount = 1`, `CreatedTokenName = 金币`, `CreatedTokenDestination = OWNER_BASE`, `CreatedTokenExhausted = true`, and `CreatedTokenKeywords = [反应]`.
- `BehaviorSpecCatalogParsesUnitConquestCreateDormantGoldTrigger(SFD·069/221)` verifies the same shape for the SFD print.
- `BehaviorSpecCatalogParsesUnitConquestOverkillCreateDormantGoldTrigger` verifies that 雪人斗士's official overkill text parses to `TriggerSpec.Kind = UNIT_CONQUEST_OVERKILL_CREATE_DORMANT_GOLD`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, `RequiredOverkillDamage = 3`, `CreatedTokenCount = 2`, `CreatedTokenName = 金币`, `CreatedTokenDestination = OWNER_BASE`, `CreatedTokenExhausted = true`, and `CreatedTokenKeywords = [反应]`.
- `BehaviorSpecCatalogParsesUnitConquestAttackOverkillGainScoreTrigger` verifies that 泰达米尔's official attack-overkill text parses to `TriggerSpec.Kind = UNIT_CONQUEST_ATTACK_OVERKILL_GAIN_SCORE`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, `RequiredOverkillDamage = 5`, and `ScoreAmount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestPayReturnSelfToHandTrigger` verifies that the three 薇恩 prints parse their official conquest payment text to `TriggerSpec.Kind = UNIT_CONQUEST_PAY_1_RETURN_SELF_TO_HAND`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, `ManaCost = 1`, `ReturnCount = 1`, `ReturnOriginZone = BATTLEFIELD`, `ReturnDestinationZone = HAND`, and `Optional = true`.
- `BehaviorSpecCatalogParsesUnitConquestGrantSelfBoonTrigger` verifies that the four 瑟提 prints parse their official conquest self-boon text to `TriggerSpec.Kind = UNIT_CONQUEST_GRANT_SELF_BOON`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, and `BoonCount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestReadySelfOnceTrigger` verifies that the two 卢锡安 prints parse their official conquest ready-self text to `TriggerSpec.Kind = UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, and `OncePerTurn = true`.
- `BehaviorSpecCatalogParsesUnitConquestGrantFriendlyBoonTrigger` verifies that the two 绯红印记树怪 prints parse their official conquest friendly-boon text to `TriggerSpec.Kind = UNIT_CONQUEST_GRANT_FRIENDLY_BOON`, `Timing = UNIT_CONQUEST`, `TargetScope = CONTROLLED_UNIT_ON_FIELD`, and `BoonCount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestAdditionalActivationTrigger` verifies that the two 绯红印记树怪 prints parse their official `你征服此处时的征服效果额外触发一次。` text to `TriggerSpec.Kind = UNIT_CONQUEST_ADDITIONAL_ACTIVATION`, `Timing = BATTLEFIELD_CONQUERED`, `TargetScope = CONTROLLED_UNITS_AT_THIS_BATTLEFIELD`, and `AdditionalTriggerCount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestFriendlyPowerUntilEndTrigger` verifies that 天声玄龙's official conquest power text parses to `TriggerSpec.Kind = UNIT_CONQUEST_FRIENDLY_PLUS_8_THIS_TURN`, `Timing = UNIT_CONQUEST`, `TargetScope = CONTROLLED_UNIT_ON_FIELD`, `PowerDelta = 8`, and `Duration = UNTIL_END_OF_TURN`.
- `BehaviorSpecCatalogParsesUnitConquestDestroyEquipmentGrantSelfBoonTrigger` verifies that 自适应机器人的 official conquest destroy-equipment text parses to `TriggerSpec.Kind = UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON`, `Timing = UNIT_CONQUEST`, `TargetScope = EQUIPMENT_ON_FIELD`, `DestroyCount = 1`, `BoonCount = 1`, and `Optional = true`.
- `UnitConquestDrawOneTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `KaisaUnitConquestDrawCardNo` / `IsKaisaUnitConquestDrawCardNo`.
- `UnitConquestDrawOneOrCallRuneTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `QiyanaUnitConquestDrawOrRuneCardNo` / `IsQiyanaUnitConquestDrawOrRuneCardNo`.
- `UnitConquestCreateDormantGoldTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `BadPoroUnitConquestGoldCardNo` / `IsBadPoroUnitConquestGoldCardNo`.
- `UnitConquestGrantSelfBoonTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `SettUnitConquestSelfBoonCardNo` / `IsSettUnitConquestSelfBoonCardNo`.
- `UnitConquestReadySelfOnceTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `LucianUnitConquestReadyCardNo` / `IsLucianUnitConquestReadyCardNo`.
- `UnitConquestGrantFriendlyBoonTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `FriendlyBoonUnitConquestCardNo` / `IsFriendlyBoonUnitConquestCardNo`.
- `UnitConquestFriendlyPowerUntilEndTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `FriendlyPowerUnitConquestCardNo` / `IsFriendlyPowerUnitConquestCardNo`.
- `UnitConquestDestroyEquipmentGrantSelfBoonTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `DestroyEquipmentBoonUnitConquestCardNo` / `IsDestroyEquipmentBoonUnitConquestCardNo`.
- `TriggerPaymentSourceIdentityDoesNotUseDuplicatedCardNumberAllowLists` now also verifies that `CoreRuleEngine` no longer contains the old Vayne source selector `OgnVayneConquerRecallSourceEffectKind` / `OGN_VAYNE_ASSAULT3_CONQUER_RECALL_PLAY_UNIT` or the Core-local `UnitConquestPayReturnSelfToHandEffectKind` runtime alias, and instead routes the source and effect-kind validation through `UnitConquestTriggerSpecRules.TryGetTrigger(...)` / `TryGetTriggerByEffectKind(...)` with `IsUnitConquestPayReturnSelfToHandTrigger(...)`.
- `NaturalUnitConquestTriggerTests.UnitConquestTriggerRoutingEnumeratesBehaviorSpecTriggersInsteadOfEffectHelperAllowList` verifies that `CoreRuleEngine` no longer calls `UnitConquestTriggerSpecRules.TryGetUnitConquest*`, `UnitConquestTriggerSpecRules` no longer exposes public `TryGetUnitConquest*` helpers, and the runtime route references `TriggersForCard(...)` plus `IsSupportedUnitConquestTrigger(...)`.

## Runtime Evidence

- `NaturalUnitConquestTriggerTests.KaisaDrawsFromUnitConquestTriggerAfterNaturalBattlefieldConquest` verifies a real `DECLARE_BATTLE` battlefield conquest by `OGN·039/298` 卡莎 emits `BATTLEFIELD_CONQUERED`, activates `UNIT_CONQUEST_DRAW_ONE` through the shared TriggerSpec route with reason `BATTLEFIELD_CONQUERED`, emits `CARD_DRAWN`, and moves the drawn card to the controller's hand.
- `NaturalUnitConquestTriggerTests.CrimsonSignetTreantRepeatsUnitConquestTriggerAfterNaturalBattlefieldConquest` verifies a real `DECLARE_BATTLE` battlefield conquest by `UNL-029/219` 绯红印记树怪 uses its same-battlefield `UNIT_CONQUEST_ADDITIONAL_ACTIVATION` source to activate `UNIT_CONQUEST_GRANT_FRIENDLY_BOON` twice with reason `BATTLEFIELD_CONQUERED`, while the second boon event records `alreadyHadBoon = true` and does not stack another power increase.
- `NaturalUnitConquestTriggerTests.YetiBrawlerCreatesTwoDormantGoldAfterOverkillNaturalBattlefieldConquest` verifies a real `DECLARE_BATTLE` battlefield conquest by `UNL-018/219` 雪人斗士 with 5 assigned overkill damage emits `BATTLEFIELD_CONQUERED`, activates `UNIT_CONQUEST_OVERKILL_CREATE_DORMANT_GOLD` through the shared TriggerSpec route with reason `BATTLEFIELD_CONQUERED`, and creates two exhausted Gold equipment tokens in the controller's base.
- `NaturalUnitConquestTriggerTests.TryndamereGainsScoreAfterAttackOverkillNaturalBattlefieldConquest` verifies a real `DECLARE_BATTLE` battlefield conquest by `OGN·034/298` 泰达米尔 with 7 assigned overkill damage emits `BATTLEFIELD_CONQUERED`, activates `UNIT_CONQUEST_ATTACK_OVERKILL_GAIN_SCORE` through the shared TriggerSpec route with reason `BATTLEFIELD_CONQUERED`, adds 1 score after the normal conquest score, and propagates `MATCH_WON` when the extra score reaches the effective winning score.
- `TriggerPaymentTests.OgnVayneConquer*` representatives verify that visible face-up 薇恩 conquest opens the existing `TRIGGER_PAYMENT` / `PAY_COST` window using `UNIT_CONQUEST_PAY_1_RETURN_SELF_TO_HAND`, accepts `SPEND_MANA:1` to return the source unit to its owner's hand, declines without movement, rejects insufficient mana while keeping the payment window, and suppresses the trigger for face-down, standby, or opponent-controlled source states.
- `FullGameEndToEndTests.OfficialDeckMidgamePaysVayneConquestReturnAndScoreVictoryActionLogReplaysToFinalStateHash` verifies the same `UNIT_CONQUEST_PAY_1_RETURN_SELF_TO_HAND` TriggerSpec through a legal official Jhin/Lillia deck opening-derived midgame state. It uses official `OGN·035/298` 薇恩 and `OGN·096/298` Watchful Sentinel objects, opens `TRIGGER_PAYMENT` from `DECLARE_BATTLE`, pays `PAY_COST(SPEND_MANA:1)`, returns the source object to its owner's hand, closes the window, continues to score victory, and replays the post-midgame action log to the same final state hash.
- `FullGameEndToEndTests.OfficialDeckMidgameResolvesCrimsonSignetTreantConquestRepeatAndScoreVictoryActionLogReplaysToFinalStateHash` verifies an official-deck midgame state produced from the normal submit/ready/mulligan/no-legal-battle opening can still play `UNL-029/219` 绯红印记树怪 through `PLAY_CARD`, move it with `MOVE_UNIT`, stage a defender, resolve a natural `DECLARE_BATTLE` conquest, emit two `UNIT_CONQUEST_EFFECT_ACTIVATED` / `BOON_GRANTED` events for the TriggerSpec repeat path, continue to score victory, and replay the post-midgame action log to the same final state hash.
- `P79BattlefieldHeldActivateConquestEffectsCreatesGoldAndDraws` verifies the 清算人竞技场 representative still activates 卡莎's conquest draw effect, emits `CARD_DRAWN`, and moves the drawn card to the controller's hand.
- The same `P79BattlefieldHeldActivateConquestEffectsCreatesGoldAndDraws` representative verifies the 坏坏魄罗 dormant-Gold effect still emits `UNIT_CONQUEST_EFFECT_ACTIVATED` with `UNIT_CONQUEST_CREATE_DORMANT_GOLD`, creates an exhausted equipment token, and moves that token to the controller's base.
- `P79BattlefieldHeldActivateConquestEffectsQiyanaDrawsWhenMainDeckAvailable` verifies the 清算人竞技场 representative activates 奇亚娜's draw-or-rune effect as `UNIT_CONQUEST_DRAW_ONE_OR_CALL_RUNE` and draws one card when the controller's main deck is non-empty.
- `P79BattlefieldHeldActivateConquestEffectsQiyanaCallsRuneWhenMainDeckEmpty` verifies the same TriggerSpec-driven effect calls one exhausted rune from the controller's rune deck when the main deck is empty.
- `P79BattlefieldHeldActivateConquestEffectsSkipsOpponentOwnedUnits` keeps the ownership/control guard for unit-conquest activation.
- `P79BattlefieldHeldActivateConquestEffectsReadiesLucianAndGrantsSettBoon` verifies the 清算人竞技场 representative activates 卢锡安's ready-self effect as `UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN`, records the once-per-turn marker, and activates 瑟提's self-boon effect as `UNIT_CONQUEST_GRANT_SELF_BOON`.
- `P79BattlefieldHeldActivateConquestEffectsCrimsonSignetTreantGrantsFriendlyBoon` verifies the 清算人竞技场 representative activates 绯红印记树怪's friendly-boon effect as `UNIT_CONQUEST_GRANT_FRIENDLY_BOON` exactly once and grants one boon to a legal controlled battlefield unit, proving `UNIT_CONQUEST_ADDITIONAL_ACTIVATION` is scoped to natural `BATTLEFIELD_CONQUERED` rather than battlefield-held activation.
- `P79BattlefieldHeldActivateConquestEffectsSkyvoiceWyrmlingGrantsFriendlyPower` verifies the 清算人竞技场 representative activates 天声玄龙's friendly-power effect as `UNIT_CONQUEST_FRIENDLY_PLUS_8_THIS_TURN` and applies +8 power until end of turn to a legal controlled battlefield unit.
- `P79BattlefieldHeldActivateConquestEffectsAdaptiveRobotDestroysEquipmentAndGrantsSelfBoon` verifies the 清算人竞技场 representative activates 自适应机器人的 destroy-equipment self-boon effect as `UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON`, destroys the chosen equipment, and grants self-boon.
- `P79BattlefieldHeldActivateConquestEffectsAdaptiveRobotSkipsBoonWhenNoEquipment` verifies the same TriggerSpec-driven effect does not activate when no equipment exists.
- `GameHubJoinTests.P79BattlefieldHeldActivateConquestSeedOffersBattlefieldDestinationAndActivatesUnits` remains the API/seed representative for the same battlefield-held route.

## Validation

- Focused 雪人斗士 overkill TriggerSpec parser + natural conquest runtime representatives: `2/2` passing.
- Focused 泰达米尔 attack-overkill score TriggerSpec parser + natural conquest runtime representatives: `2/2` passing.
- Focused 薇恩 pay-return-self TriggerSpec parser + trigger-payment source/runtime representatives: `9/9` passing.
- Focused official-deck-derived Vayne pay-return replay representative: `1/1` passing.
- Focused natural unit conquest additional-activation + 清算人竞技场 non-repeat representatives: `5/5` passing.
- Focused official-deck midgame Treant conquest repeat replay representative: `1/1` passing.
- Adjacent `FullGameEndToEnd` / `NaturalUnitConquestTrigger` / `UnitConquest` / `MatchRecovery` representatives: `2034/2034` passing.
- Adjacent `NaturalUnitConquestTrigger` / `UnitConquest` / `P79BattlefieldHeldActivateConquest` / `BattlefieldConquer` / `DeclareBattle` / `BattleDamageAssignment` / `Score` / `MatchRecovery` / `CardCatalogBaseline` representatives: `2589/2589` passing.
- Adjacent `OfficialDeckMidgamePaysVayneConquestReturn` / `Vayne` / `UnitConquest` / `TriggerPayment` / `BattlefieldConquer` / `DeclareBattle` / `PaymentEngine` / `FullGameEndToEnd` / `MatchRecovery` / `CardCatalogBaseline` representatives: `3453/3453` passing.
- 2026-06-30 follow-up focused trigger-payment source guard / catalog representatives: `5/5` passing.
- 2026-06-30 follow-up adjacent Vayne / UnitConquest / TriggerPayment / PaymentEngine / MatchRecovery / CardCatalogBaseline representatives: `3198/3198` passing.
- 2026-07-01 follow-up focused NaturalUnitConquestTrigger / TriggerPayment helper-surface guards and representatives: `92/92` passing.
- 2026-07-01 follow-up adjacent UnitConquest / TriggerPayment / MatchRecovery representatives: `2116/2116` passing.
- 2026-07-01 follow-up backend full conformance: `9078/9078` passing.
- Backend full conformance: `9020/9020` passing.
- 2026-06-30 follow-up backend full conformance: `9038/9038` passing.
- No DevUi source or catalog TypeScript shape changed in this slice, so DevUi build was not rerun.

## Residual Risk

- The old `Is*UnitConquest*CardNo(...)` helper set in `CoreRuleEngine` is now empty for this 清算人竞技场 representative family.
- 绯红印记树怪's conquest-effect additional-activation text now has a natural-conquest representative; complete ordering and multi-source breadth remain open.
- Natural battle-conquest activation now invokes the supported TriggerSpec effects for surviving conquering units; complete APNAP ordering, simultaneous multi-source ordering, and optional-target breadth remain open.
- Complete optional yes/no target selection and hidden-information edge cases for targeted conquest effects remain open.
