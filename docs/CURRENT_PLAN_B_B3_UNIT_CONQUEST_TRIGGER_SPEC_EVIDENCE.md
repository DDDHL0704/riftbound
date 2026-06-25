# Plan B / B3 Unit Conquest Trigger Spec Evidence

Date: 2026-06-25

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `OGN·039/298` 卡莎 has official text `{{急速}}（你可以选择额外支付{{1}}和{{红色}}，让我以活跃状态进场。）\n当我征服一处战场时，抽一张牌。`
- `data/official/card-catalog.zh-CN.json`: `OGN·039a/298` 卡莎 has the same official text.
- `data/official/card-catalog.zh-CN.json`: `OGN·155/298` 奇亚娜 has official text `{{法盾}}（对手必须支付{{A}}才能将我选作法术或技能的目标。）\n当我征服一处战场时，抽一张牌或召出一枚休眠的符文。`
- `data/official/card-catalog.zh-CN.json`: `UNL-222/219` 坏坏魄罗 has official text `当我征服一处战场时，打出一个休眠的“金币”装备指示物。`
- `data/official/card-catalog.zh-CN.json`: `SFD·069/221` 坏坏魄罗 has the same official text.
- `data/official/card-catalog.zh-CN.json`: `SFD·232/221` 瑟提 has official text `当我被打出时、或当我征服一处战场时，给予我增益。（如果我未拥有增益，则获得一个{{S}}+1增益。）\n消耗我的增益：让我本回合内{{S}}+4。`
- `data/official/card-catalog.zh-CN.json`: `SFD·232*/221`, `OGN·164/298`, and `OGN·164a/298` 瑟提 have the same unit conquest self-boon text.
- `data/official/card-catalog.zh-CN.json`: `SFD·113/221` 卢锡安 has official text `{{百炼}}（当你打出我时，你可以选择为我{{装配}}你的一件武装，其装配费用减少{{A}}。可选择已贴附的武装。）\n每回合首次，当我征服一处战场时，让我变为活跃状态。`
- `data/official/card-catalog.zh-CN.json`: `SFD·113a/221` 卢锡安 has the same unit conquest ready-self text.
- `data/official/card-catalog.zh-CN.json`: `UNL-029/219` 绯红印记树怪 has official text `{{急速}}（你可以选择额外支付{{1}}和{{红色}}，让我以活跃状态进场。）\n你征服此处时的征服效果额外触发一次。\n当我征服一处战场时，给予一名友方单位{{增益}}。（如果其未拥有增益，则获得一个{{S}}+1增益。）`
- `data/official/card-catalog.zh-CN.json`: `UNL-029a/219` 绯红印记树怪 has the same unit conquest friendly-boon text.
- `data/official/card-catalog.zh-CN.json`: `OGN·286/298` 清算人竞技场 has official text `当你据守此处时，激活此处所有单位的征服效果。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official catalog text remains the local rule authority input for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesUnitConquestDrawOneTrigger(OGN·039/298)` verifies that 卡莎's official unit text parses to `TriggerSpec.Kind = UNIT_CONQUEST_DRAW_ONE`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, and `DrawCount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestDrawOneTrigger(OGN·039a/298)` verifies the same shape for the alternate print.
- `BehaviorSpecCatalogParsesUnitConquestDrawOneOrCallRuneTrigger` verifies that 奇亚娜's official unit text parses to `TriggerSpec.Kind = UNIT_CONQUEST_DRAW_ONE_OR_CALL_RUNE`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, `DrawCount = 1`, and `RuneCallCount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestCreateDormantGoldTrigger(UNL-222/219)` verifies that 坏坏魄罗's official unit text parses to `TriggerSpec.Kind = UNIT_CONQUEST_CREATE_DORMANT_GOLD`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, `CreatedTokenCount = 1`, `CreatedTokenName = 金币`, `CreatedTokenDestination = OWNER_BASE`, `CreatedTokenExhausted = true`, and `CreatedTokenKeywords = [反应]`.
- `BehaviorSpecCatalogParsesUnitConquestCreateDormantGoldTrigger(SFD·069/221)` verifies the same shape for the SFD print.
- `BehaviorSpecCatalogParsesUnitConquestGrantSelfBoonTrigger` verifies that the four 瑟提 prints parse their official conquest self-boon text to `TriggerSpec.Kind = UNIT_CONQUEST_GRANT_SELF_BOON`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, and `BoonCount = 1`.
- `BehaviorSpecCatalogParsesUnitConquestReadySelfOnceTrigger` verifies that the two 卢锡安 prints parse their official conquest ready-self text to `TriggerSpec.Kind = UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN`, `Timing = UNIT_CONQUEST`, `TargetScope = SOURCE_UNIT`, and `OncePerTurn = true`.
- `BehaviorSpecCatalogParsesUnitConquestGrantFriendlyBoonTrigger` verifies that the two 绯红印记树怪 prints parse their official conquest friendly-boon text to `TriggerSpec.Kind = UNIT_CONQUEST_GRANT_FRIENDLY_BOON`, `Timing = UNIT_CONQUEST`, `TargetScope = CONTROLLED_UNIT_ON_FIELD`, and `BoonCount = 1`.
- `UnitConquestDrawOneTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `KaisaUnitConquestDrawCardNo` / `IsKaisaUnitConquestDrawCardNo`.
- `UnitConquestDrawOneOrCallRuneTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `QiyanaUnitConquestDrawOrRuneCardNo` / `IsQiyanaUnitConquestDrawOrRuneCardNo`.
- `UnitConquestCreateDormantGoldTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `BadPoroUnitConquestGoldCardNo` / `IsBadPoroUnitConquestGoldCardNo`.
- `UnitConquestGrantSelfBoonTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `SettUnitConquestSelfBoonCardNo` / `IsSettUnitConquestSelfBoonCardNo`.
- `UnitConquestReadySelfOnceTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `LucianUnitConquestReadyCardNo` / `IsLucianUnitConquestReadyCardNo`.
- `UnitConquestGrantFriendlyBoonTriggerDoesNotUseCardNumberAllowList` verifies that `CoreRuleEngine` no longer contains `FriendlyBoonUnitConquestCardNo` / `IsFriendlyBoonUnitConquestCardNo`.

## Runtime Evidence

- `P79BattlefieldHeldActivateConquestEffectsCreatesGoldAndDraws` verifies the 清算人竞技场 representative still activates 卡莎's conquest draw effect, emits `CARD_DRAWN`, and moves the drawn card to the controller's hand.
- The same `P79BattlefieldHeldActivateConquestEffectsCreatesGoldAndDraws` representative verifies the 坏坏魄罗 dormant-Gold effect still emits `UNIT_CONQUEST_EFFECT_ACTIVATED` with `UNIT_CONQUEST_CREATE_DORMANT_GOLD`, creates an exhausted equipment token, and moves that token to the controller's base.
- `P79BattlefieldHeldActivateConquestEffectsQiyanaDrawsWhenMainDeckAvailable` verifies the 清算人竞技场 representative activates 奇亚娜's draw-or-rune effect as `UNIT_CONQUEST_DRAW_ONE_OR_CALL_RUNE` and draws one card when the controller's main deck is non-empty.
- `P79BattlefieldHeldActivateConquestEffectsQiyanaCallsRuneWhenMainDeckEmpty` verifies the same TriggerSpec-driven effect calls one exhausted rune from the controller's rune deck when the main deck is empty.
- `P79BattlefieldHeldActivateConquestEffectsSkipsOpponentOwnedUnits` keeps the ownership/control guard for unit-conquest activation.
- `P79BattlefieldHeldActivateConquestEffectsReadiesLucianAndGrantsSettBoon` verifies the 清算人竞技场 representative activates 卢锡安's ready-self effect as `UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN`, records the once-per-turn marker, and activates 瑟提's self-boon effect as `UNIT_CONQUEST_GRANT_SELF_BOON`.
- `P79BattlefieldHeldActivateConquestEffectsCrimsonSignetTreantGrantsFriendlyBoon` verifies the 清算人竞技场 representative activates 绯红印记树怪's friendly-boon effect as `UNIT_CONQUEST_GRANT_FRIENDLY_BOON` and grants a boon to a legal controlled battlefield unit.
- `P79BattlefieldHeldActivateConquestEffectsAdaptiveRobotDestroysEquipmentAndGrantsSelfBoon` and `P79BattlefieldHeldActivateConquestEffectsAdaptiveRobotSkipsBoonWhenNoEquipment` remain adjacent representatives for the still-open unit-conquest family.
- `GameHubJoinTests.P79BattlefieldHeldActivateConquestSeedOffersBattlefieldDestinationAndActivatesUnits` remains the API/seed representative for the same battlefield-held route.

## Validation

- Focused 绯红印记树怪 grant-friendly-boon behavior-spec/source-guard/runtime tests: `4/4` passing.
- Adjacent unit-conquest / `P79BattlefieldHeldActivateConquest...` representatives: `30/30` passing.
- `MatchRecovery`: `1989/1989` passing.
- Backend full conformance: `8493/8493` passing.
- No DevUi source or catalog TypeScript shape changed in this slice, so DevUi build was not rerun.

## Residual Risk

- The remaining two unit-conquest effect helpers are still card-number based and must be migrated in follow-up slices.
- 绯红印记树怪's conquest-effect doubling text is not implemented in this slice.
- The current runtime validation uses 清算人竞技场 to activate unit conquest effects; complete natural conquest trigger queuing remains open.
- Complete optional target selection and hidden-information edge cases for targeted conquest effects remain open.
