# Plan B / B4 Battlefield Trigger Spec Audit

Date: 2026-06-25

Status: focused B4 battlefield trigger spec slices accepted for moved-unit power, held-next-spell Echo, held unit-cost increase, held draw-one, held call-rune, held each-player call-rune, held move-unit-to-base, held grant-boon, held create-minion, friendly-spell draw, spell-power bonus, high-cost spell insight, unit-play boon, unit-returned call-rune, and first-unit-play move-other-to-base; project remains **NOT READY**.

## Scope

This slice moves one implemented battlefield trigger away from engine card-number branching:

- `OGN·277/298` / 后巷酒吧 official text: `每当一名单位从此处向别处移动时，让其本回合内{{S}}+1。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER`
  - `Timing = BATTLEFIELD_UNIT_MOVED_AWAY`
  - `TargetScope = MOVED_UNIT`
  - `PowerDelta = 1`
  - `Duration = UNTIL_END_OF_TURN`
- `CoreRuleEngine.ApplyBattlefieldMovedUnitPowerPlusOne` now finds eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldMovedUnitPowerModifierTrigger(...)` and reads the power delta from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldMovedUnitPowerPlusOneCardNo` constant.

The 2026-06-25 follow-up also moves one implemented held-battlefield trigger away from engine card-number branching:

- `UNL-216/219` / 皮城学院 official text: `当你据守此处时，在本回合内，你的下一个法术获得等同于其基础费用的{{回响}}。（你可以选择支付此额外费用，以重复此法术效果。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO`
  - `Timing = BATTLEFIELD_HELD`
  - `Duration = UNTIL_END_OF_TURN`
- `CoreRuleEngine.TryResolveBattlefieldHeldNextSpellEchoTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldNextSpellEchoTrigger(...)`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldNextSpellEchoCardNo` constant.
- The old `BattlefieldHeldNextSpellEchoCardNo` / `IsBattlefieldHeldNextSpellEchoCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `92` total / `88` in `CoreRuleEngine`.

The 2026-06-25 held unit-cost follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `UNL-219/219` / 海力亚秘库 official text: `当你据守此处时，你的非指示物单位在本回合内的打出费用增加{{1}}。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE`
  - `Timing = BATTLEFIELD_HELD`
  - `Duration = UNTIL_END_OF_TURN`
  - `ManaDelta = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldUnitCostIncreaseTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldUnitCostIncreaseTrigger(...)`.
- `CoreRuleEngine` and `MatchSession` read the mana increase from the until-end marker; the `+1` case keeps the existing marker shape `BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:{playerId}` for compatibility, while larger future deltas can be encoded as a marker suffix.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldUnitCostIncreaseCardNo` constant.
- The old `BattlefieldHeldUnitCostIncreaseCardNo` / `IsBattlefieldHeldUnitCostIncreaseCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `91` total / `87` in `CoreRuleEngine`; Core battlefield helper count is `43`.

The 2026-06-25 friendly-spell draw follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `OGN·292/298` / 幻梦之树 official text: `每回合首次：当你对此处的友方单位使用法术时，抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE`
  - `Timing = BATTLEFIELD_FRIENDLY_SPELL_TARGETED`
  - `TargetScope = FRIENDLY_UNIT_AT_THIS_BATTLEFIELD`
  - `DrawCount = 1`
- `CoreRuleEngine.TryGetBattlefieldFriendlySpellDrawSource` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldFriendlySpellDrawTrigger(...)` and reads the draw count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldFriendlySpellDrawCardNo` constant.
- The old `BattlefieldFriendlySpellDrawCardNo` / `IsBattlefieldFriendlySpellDrawCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `90` total / `86` in `CoreRuleEngine`; Core battlefield helper count is `42`.

The 2026-06-25 spell-power bonus follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-205/219` / 废弃大厅 official text: `当一名玩家打出法术时，该玩家可以选择让自己在此处控制的一名单位在本回合内{{S}}+1。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_SPELL_POWER_PLUS_1`
  - `Timing = BATTLEFIELD_SPELL_PLAYED`
  - `TargetScope = FRIENDLY_UNIT_AT_THIS_BATTLEFIELD`
  - `PowerDelta = 1`
  - `Duration = UNTIL_END_OF_TURN`
- `CoreRuleEngine.TryResolveBattlefieldSpellPowerBonusTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldSpellPowerBonusTrigger(...)` and reads the power delta from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldSpellPowerBonusCardNo` constant.
- The old `BattlefieldSpellPowerBonusCardNo` / `IsBattlefieldSpellPowerBonusCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `89` total / `85` in `CoreRuleEngine`; Core battlefield helper count is `41`.

The 2026-06-25 high-cost spell insight follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-211/219` / 失落书库 official text: `若此战场受你控制，当你打出一张法术牌时，如果消耗了不低于{{4}}法力，则进行{{洞察}}。（查看你主牌堆顶部的一张牌。你可以选择将其回收。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE`
  - `Timing = BATTLEFIELD_SPELL_PLAYED`
  - `MinimumPaidMana = 4`
  - `RecycleCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHighCostSpellInsightTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHighCostSpellInsightRecycleTrigger(...)` and reads both the paid-mana threshold and recycle count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHighCostSpellInsightCardNo` constant.
- The old `BattlefieldHighCostSpellInsightCardNo` / `IsBattlefieldHighCostSpellInsightCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `87` total / `83` in `CoreRuleEngine`; Core battlefield helper count is `39`.

The 2026-06-25 unit-play boon follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-218/219` / 偶像谷 official text: `当一名玩家在此处打出一名单位时，该玩家可以选择支付{{1}}，以此给予该单位{{增益}}。（未拥有增益的单位获得一个{{S}}+1增益。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON`
  - `Timing = BATTLEFIELD_UNIT_PLAYED`
  - `TargetScope = PLAYED_UNIT_AT_THIS_BATTLEFIELD`
  - `ManaCost = 1`
  - `BoonCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldPlayUnitPayOneBoonTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldPlayUnitPayBoonTrigger(...)` and reads both the mana cost and boon count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldPlayUnitPayOneBoonCardNo` constant.
- The old `BattlefieldPlayUnitPayOneBoonCardNo` / `IsBattlefieldPlayUnitPayOneBoonCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `85` total / `81` in `CoreRuleEngine`; Core battlefield helper count is `37`.

The 2026-06-25 unit-returned call-rune follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-214/219` / 鬼影湾 official text: `当此处的一名单位返回到一名玩家的手牌时，该玩家可以选择支付{{1}}，以此召出一枚休眠的符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE`
  - `Timing = BATTLEFIELD_UNIT_RETURNED`
  - `TargetScope = RETURNED_UNIT_AT_THIS_BATTLEFIELD`
  - `ManaCost = 1`
  - `RuneCallCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldUnitReturnedCallRuneTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldUnitReturnedPayCallRuneTrigger(...)` and reads both the mana cost and rune-call count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldUnitReturnedCallRuneCardNo` constant.
- The old `BattlefieldUnitReturnedCallRuneCardNo` / `IsBattlefieldUnitReturnedCallRuneCardNo` / `BattlefieldUnitReturnedCallRuneManaCost` card-number and fixed-cost branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `84` total / `80` in `CoreRuleEngine`; Core battlefield helper count is `36`.

The 2026-06-25 first-unit-play move-other-to-base follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `UNL-215/219` / 流星疗泉 official text: `每回合首次，当玩家在此处打出一名非指示物单位时，该玩家可以选择将自己在此处控制的另一名单位移动到其基地。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE`
  - `Timing = BATTLEFIELD_UNIT_PLAYED`
  - `TargetScope = OTHER_CONTROLLED_UNIT_AT_THIS_BATTLEFIELD`
  - `MoveCount = 1`
  - `MoveDestination = OWNER_BASE`
  - `OncePerTurn = true`
  - `ExcludesTokens = true`
- `CoreRuleEngine.TryResolveBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger(...)` and reads the once-per-turn, non-token, target-scope and movement parameters from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo` constant.
- The old `BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo` / `IsBattlefieldFirstUnitPlayedMoveOtherToBaseCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `83` total / `79` in `CoreRuleEngine`; Core battlefield helper count is `35`.

The 2026-06-25 held draw-one follow-up moves another implemented battlefield trigger away from engine card-number branching:

- `OGN·280/298` official text: `当你据守此处时，抽一张牌。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_DRAW_ONE`
  - `Timing = BATTLEFIELD_HELD`
  - `DrawCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldDrawTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldDrawTrigger(...)` and reads the draw count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldDrawCardNo` constant.
- The old `BattlefieldHoldDrawCardNo` / `IsBattlefieldHoldDrawCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `82` total / `78` in `CoreRuleEngine`; Core battlefield helper count is `34`.

The 2026-06-25 held call-rune follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `OGN·288/298` / 星尖峰 official text: `当你据守此处时，你可以选择召出一枚休眠的符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_CALL_RUNE`
  - `Timing = BATTLEFIELD_HELD`
  - `RuneCallCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldCallRuneTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldCallRuneTrigger(...)` and reads the rune-call count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldCallRuneCardNo` constant.
- The old `BattlefieldHoldCallRuneCardNo` / `IsBattlefieldHoldCallRuneCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `81` total / `77` in `CoreRuleEngine`; Core battlefield helper count is `33`.

The 2026-06-25 held each-player call-rune follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `SFD·219/221` / 彩纸灵树 official text: `当你据守此处时，每名玩家召出一枚休眠的符文。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_EACH_PLAYER_CALL_RUNE`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = EACH_PLAYER`
  - `RuneCallCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldEachPlayerCallRuneTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldEachPlayerCallRuneTrigger(...)` and reads the target scope and rune-call count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldEachPlayerCallRuneCardNo` constant.
- The old `BattlefieldHoldEachPlayerCallRuneCardNo` / `IsBattlefieldHoldEachPlayerCallRuneCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `80` total / `76` in `CoreRuleEngine`; Core battlefield helper count is `32`.

The 2026-06-25 held move-unit-to-base follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `UNL-207/219` / 业余排练厅 official text: `当你据守此处时，你可以选择将战场上的一名单位移动到其基地。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = UNIT_AT_THIS_BATTLEFIELD`
  - `MoveCount = 1`
  - `MoveDestination = OWNER_BASE`
- `CoreRuleEngine.TryResolveBattlefieldHeldMoveUnitToBaseTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldMoveUnitToBaseTrigger(...)` and reads the target scope and movement parameters from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHeldMoveUnitToBaseCardNo` constant.
- The old `BattlefieldHeldMoveUnitToBaseCardNo` / `IsBattlefieldHeldMoveUnitToBaseCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `79` total / `75` in `CoreRuleEngine`; Core battlefield helper count is `31`.

The 2026-06-25 held grant-boon follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `OGN·283/298` / 纳沃利角斗场 official text: `当你据守此处时，给予此处的一名单位增益。（如果该单位未拥有增益，则获得一个{{S}}+1增益。）`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_GRANT_BOON`
  - `Timing = BATTLEFIELD_HELD`
  - `TargetScope = UNIT_AT_THIS_BATTLEFIELD`
  - `BoonCount = 1`
- `CoreRuleEngine.TryResolveBattlefieldHeldGrantBoonTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldGrantBoonTrigger(...)` and reads the target scope and boon count from `BehaviorSpec.Triggers`.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldGrantBoonCardNo` constant.
- The old `BattlefieldHoldGrantBoonCardNo` / `IsBattlefieldHoldGrantBoonCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `78` total / `74` in `CoreRuleEngine`; Core battlefield helper count is `30`.

The 2026-06-25 held create-minion follow-up moves another implemented held-battlefield trigger away from engine card-number branching:

- `OGN·275/298` / 团结圣坛 official text: `当你据守此处时，打出一名1{{S}}的“随从”到你的基地。`
- `RuleTextParser` now parses that text as `TriggerSpec` with:
  - `Kind = BATTLEFIELD_HELD_CREATE_MINION`
  - `Timing = BATTLEFIELD_HELD`
  - `CreatedTokenCount = 1`
  - `CreatedTokenName = 随从`
  - `CreatedTokenPower = 1`
  - `CreatedTokenDestination = OWNER_BASE`
- `CoreRuleEngine.TryResolveBattlefieldHeldCreateMinionTrigger` now recognizes eligible battlefield sources through `BattlefieldTriggerSpecRules.TryGetBattlefieldHeldCreateMinionTrigger(...)` and reads token count, token family, token power and destination from `BehaviorSpec.Triggers`.
- The token identity is resolved through `P6TokenFactoryCatalog` by token family, power and unit tag; the current official representative continues to create `OGN·271/298`, but the runtime no longer names that card number as the trigger's source condition.
- `MatchSession` battlefield-object recognition now uses the same trigger-spec query instead of the old `BattlefieldHoldCreateMinionCardNo` constant.
- The old `BattlefieldHoldCreateMinionCardNo` / `IsBattlefieldHoldCreateMinionCardNo` card-number branch is removed. Current source-helper count for `private static bool Is*CardNo(...)` is `76` total / `73` in `CoreRuleEngine`; Core battlefield helper count is `29`.

## Non-Closure

This is a narrow B4 cleanup slice. It does not close all battlefield trigger families, same-turn movement policy, complete battlefield lifecycle, conquest triggers, optional trigger choice prompts, B0 full real-deck end-to-end game completion, frontend/browser smoke, full official coverage or READY.

## Validation

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldMoved / BattlefieldMovePower / MoveUnit / BoardTaskQueue / FullGame / GameHub: passed `326/326`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8369/8369`;
- DevUi build: passed after adding `/opt/homebrew/bin` to PATH for local `npm`;
- `git diff --check`: passed.

2026-06-25 held-next-spell Echo follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / BattlefieldMovedUnitPower / BattlefieldMovePower / GameHub battlefield representatives: passed `102/102`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8373/8373`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi.

2026-06-25 held unit-cost increase follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / PaymentEngine / PlayCard / GameHub battlefield representatives / BoardTaskQueue / FullGame: passed `1156/1156`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after adding `/opt/homebrew/bin` to PATH for local `npm`;
- backend full conformance: passed `8375/8375`.

2026-06-25 friendly-spell draw follow-up validation:

- focused behavior-spec/source guard/runtime representative: passed `5/5`;
- adjacent BattlefieldFriendlySpellDraw / BattlefieldFriendlySpellTarget / BattlefieldTriggerSpec / held-trigger representatives: passed `12/12`;
- MatchRecovery: passed `1989/1989`;
- DevUi build: passed after adding `/opt/homebrew/bin` to PATH for local `npm`;
- backend full conformance: passed `8377/8377`.

2026-06-25 spell-power bonus follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldSpellPowerBonus / BattlefieldTriggerSpec / recently migrated battlefield trigger representatives: passed `21/21`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8379/8379`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 high-cost spell insight follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `6/6`;
- adjacent BattlefieldHighCostSpellInsight / BattlefieldTriggerSpec / BattlefieldSpellPowerBonus / BattlefieldFriendlySpellDraw / P6 battlefield surface representatives: passed `15/15`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8382/8382`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 unit-play boon follow-up validation:

- focused behavior-spec/source guard/runtime representative: passed `6/6`;
- adjacent BattlefieldPlayUnitBoon / BattlefieldTriggerSpec / PlayCard / Boon / GameHub / P6 battlefield surface representatives: passed `331/331`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8385/8385`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 unit-returned call-rune follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `6/6`;
- adjacent BattlefieldReturnCallRune / BattlefieldReturnedUnit / BattlefieldTriggerSpec / recent battlefield trigger representatives / call-rune representatives: passed `23/23`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8387/8387`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 first-unit-play move-other-to-base follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldFirstUnit / BattlefieldPlayUnitBoon / BattlefieldTriggerSpec / MoveUnit / PlayCard / GameHub representatives: passed `342/342`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8389/8389`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

2026-06-25 held draw-one follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `5/5`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / Dunehorn / held-trigger GameHub representatives: passed `63/63`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8391/8391`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held call-rune follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / BattlefieldReturnCallRune representatives: passed `64/64`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8393/8393`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held each-player call-rune follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / CallRune / Runes representatives: passed `103/103`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8395/8395`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held move-unit-to-base follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / MoveUnit / FullGame / GameHub representatives: passed `360/360`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8397/8397`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held grant-boon follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / Boon / FullGame / GameHub representatives: passed `360/360`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8399/8399`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.

2026-06-25 held create-minion follow-up validation:

- focused behavior-spec/source guard/runtime/GameHub representative: passed `4/4`;
- adjacent BattlefieldHeld / BattlefieldTriggerSpec / Token / FullGame / GameHub representatives: passed `349/349`;
- MatchRecovery: passed `1989/1989`;
- backend full conformance: passed `8401/8401`;
- DevUi build/browser smoke: not repeated; this slice did not touch DevUi files or frontend behavior.
