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
- MatchRecovery: `1989/1989`;
- backend full conformance after the held seven-units win follow-up: `8407/8407`;
- DevUi build: passed after synchronizing `BehaviorSpec.triggers` catalog typing.

## Non-Closure

This evidence proves seventeen battlefield trigger representatives have moved to BehaviorSpec-driven routing. It does not prove the complete B4 battlefield-effect family, all trigger timing windows, all movement / control-zone edge cases, optional trigger choice prompts, B0 full real-deck end-to-end game completion, all card-effect families, frontend smoke or READY.
