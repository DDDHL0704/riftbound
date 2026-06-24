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
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text and local evidence remain the rule authority inputs for this battlefield-domain slice.
- Existing representative tests `P79BattlefieldMovedUnitGainsTemporaryPower`, `P79BattlefieldMovedUnitPowerSkipsOpponentControlledSource`, and `P79BattlefieldMovePowerSeedMovesUnitAndAppliesBonus` remain the runtime evidence for this narrow behavior.
- Existing representative tests `P79BattlefieldHeldNextSpellEcho...` and GameHub `P79BattlefieldHeldNextSpellEcho...` remain the runtime evidence for the held-next-spell Echo behavior.
- Existing representative tests `P79BattlefieldHeldUnitCostIncrease...` and GameHub `P79BattlefieldHeldUnitCostIncrease...` remain the runtime evidence for the held unit-cost increase behavior.
- Existing representative tests `P79BattlefieldFriendlySpellTarget...` and GameHub `P79BattlefieldFriendlySpellDrawSeed...` remain the runtime evidence for the friendly-spell draw behavior.
- Existing representative tests `P79BattlefieldSpellPowerBonus...` and GameHub `P79BattlefieldSpellPowerBonusSeed...` remain the runtime evidence for the spell-power bonus behavior.
- Existing representative tests `P79BattlefieldHighCostSpellInsight...` and GameHub `P79BattlefieldHighCostSpellInsightSeed...` remain the runtime evidence for the high-cost spell insight behavior.

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
- MatchRecovery: `1989/1989`;
- backend full conformance after the high-cost spell insight follow-up: `8382/8382`;
- DevUi build/browser smoke: not repeated for the high-cost spell insight follow-up; this slice did not touch DevUi files or frontend behavior.

## Non-Closure

This evidence proves six battlefield trigger representatives have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, all trigger timing windows, all movement / control-zone edge cases, optional trigger choice prompts, all card-effect families, frontend smoke or READY.
