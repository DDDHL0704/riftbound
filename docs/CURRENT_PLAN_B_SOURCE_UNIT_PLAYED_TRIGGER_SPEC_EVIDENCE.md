# Plan B / Source Unit Played Trigger Spec Evidence

Date: 2026-07-03

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `SFD·140/221` 菲兹 has official text `当你打出我时，你可以选择从你的废牌堆中打出一个法力费用不高于{{3}}的法术，无需支付其法力费用（仍需支付所有符能费用）。打出该法术后，将其回收。`
- `data/official/card-catalog.zh-CN.json`: `OGN·134/298` 动员 is an official 2-mana no-target spell with text `召出一枚休眠的符文。如果你无法达成，则抽一张牌。`, used as the source-unit-played no-target rune-call / failed-rune-call draw representative for the same graveyard-spell bridge.
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text plus the core play/stack/zone rules remain the local authority inputs for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesSourceUnitPlayedPlayLowCostGraveyardSpellRecycleTrigger` verifies that 菲兹 parses to `TriggerSpec.Kind = SOURCE_UNIT_PLAYED_PLAY_LOW_COST_GRAVEYARD_SPELL_RECYCLE`, `Timing = SOURCE_UNIT_PLAYED`, `TargetScope = CONTROLLED_SPELL_IN_GRAVEYARD`, `PlayCount = 1`, `PlayOriginZone = GRAVEYARD`, `PlayDestinationZone = STACK`, `PlayCardFilter = TAG:CARD_TYPE:SPELL`, `MaximumPlayedCardManaCost = 3`, `IgnorePlayManaCost = true`, `PayPlayPowerCosts = true`, `RecyclePlayedCardOnResolution = true`, and `Optional = true`.
- `SourceUnitPlayedTriggerSpecRules` reads BehaviorSpec trigger data from the catalog and accepts this family through a generic predicate. `CoreRuleEngine` does not branch on `SFD·140/221`; it resolves the trigger by enumerating `SourceUnitPlayedTriggerSpecRules.TriggersForCard(...)`.

## Runtime Evidence

- `SourceUnitPlayedTriggerTests.FizzPlaysLowCostGraveyardSpellAndRecyclesItAfterSourceUnitPlayed` verifies a real `PLAY_CARD` / pass-pass stack resolution for `SFD·140/221` 菲兹. After the source unit enters the controller's base, the shared source-unit-played TriggerSpec route chooses controlled graveyard `OGN·048/298` 冥想 with mana cost 2, emits `SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED`, emits `CARD_PLAYED_FROM_GRAVEYARD` with `sourceZone = GRAVEYARD`, `destinationZone = STACK`, `ignorePlayManaCost = true`, and `payPlayPowerCosts = true`, resolves the no-target draw spell through existing stack resolution, then emits `CARDS_RECYCLED` and moves the played spell to the controller's main deck.
- `SourceUnitPlayedTriggerTests.FizzPlaysLowCostGraveyardRuneSpellAndRecyclesItAfterSourceUnitPlayed` verifies the same route can choose controlled graveyard `OGN·134/298` 动员 with mana cost 2, emit `CARD_PLAYED_FROM_GRAVEYARD`, reuse existing stack resolution to call one exhausted rune from the controller's rune deck, then recycle 动员 to the controller's main deck.
- `SourceUnitPlayedTriggerTests.FizzGraveyardRuneSpellDrawsAndRecyclesWhenRuneCallFailsAfterSourceUnitPlayed` verifies the same 动员 route when the controller's rune deck is empty: stack resolution emits `RUNES_CALLED` with count 0, applies 动员's official fallback draw one, then recycles the played spell to the controller's main deck.
- `FullGameEndToEndTests.OfficialDeckMidgameResolvesFizzGraveyardRuneSpellAndScoreVictoryActionLogReplaysToFinalStateHash` starts from legal official orange/purple `UNL-201/219` / `UNL-119/219` deck submission/opening with required `SFD·140/221` 菲兹 and `OGN·134/298` 动员, derives a focused midgame state, submits server-authored `PLAY_CARD` for 菲兹, resolves the BehaviorSpec source-unit-played graveyard 动员 route through stack pass-pass, then continues through score victory and verifies `MatchActionLogReplayer` reaches the same final state hash.
- The graveyard spell execution helper is shared with the existing Kai'Sa conquest representative. It is intentionally limited to no-target draw and no-target rune-call spells that can reuse existing stack resolution without extra target, power-cost prompt, experience, pending-payment, token-state handoff, or source-zone replacement.

## Validation

- 2026-07-03 focused source-unit-played TriggerSpec parser + runtime draw / rune-call / failed-rune-call representatives: `4/4` passing.
- 2026-07-03 focused B0 official-deck Fizz graveyard-rune-spell replay: `1/1` passing.
- 2026-07-03 adjacent SourceUnitPlayed / UnitConquest graveyard-spell helper / FullGameEndToEnd / Stack / CardCatalogBaseline / MatchRecovery representatives: `3016/3016` passing.
- 2026-07-03 backend full conformance after B0 Fizz official-deck replay: `9164/9164` passing.

## Residual Risk

- Complete optional yes/no prompts, explicit graveyard spell selection, targeted spell legality, power-cost prompt routing, and broader spell effect state handoff remain open.
- This slice does not claim full official 菲兹 coverage; it only closes the no-target draw and no-target rune-call representative paths for the official graveyard-spell free-play and recycle text.
