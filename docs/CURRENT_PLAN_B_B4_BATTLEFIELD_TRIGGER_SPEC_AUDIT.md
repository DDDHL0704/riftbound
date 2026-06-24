# Plan B / B4 Battlefield Trigger Spec Audit

Date: 2026-06-25

Status: focused B4 battlefield trigger spec slices accepted for moved-unit power, held-next-spell Echo, held unit-cost increase, friendly-spell draw, spell-power bonus, high-cost spell insight, and unit-play boon; project remains **NOT READY**.

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

## Non-Closure

This is a narrow B4 cleanup slice. It does not close all battlefield trigger families, same-turn movement policy, complete battlefield lifecycle, conquest triggers, optional trigger choice prompts, frontend/browser smoke, full official coverage or READY.

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
