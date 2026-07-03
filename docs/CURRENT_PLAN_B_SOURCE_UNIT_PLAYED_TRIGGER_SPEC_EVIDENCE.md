# Plan B / Source Unit Played Trigger Spec Evidence

Date: 2026-07-03

Project status: **NOT READY**.

## Rule Sources

- `data/official/card-catalog.zh-CN.json`: `SFD·140/221` 菲兹 has official text `当你打出我时，你可以选择从你的废牌堆中打出一个法力费用不高于{{3}}的法术，无需支付其法力费用（仍需支付所有符能费用）。打出该法术后，将其回收。`
- `docs/rules-authority-and-audit.md` and `docs/rules-evidence-index.md`: official card text plus the core play/stack/zone rules remain the local authority inputs for this representative slice.

## BehaviorSpec Evidence

- `BehaviorSpecCatalogParsesSourceUnitPlayedPlayLowCostGraveyardSpellRecycleTrigger` verifies that 菲兹 parses to `TriggerSpec.Kind = SOURCE_UNIT_PLAYED_PLAY_LOW_COST_GRAVEYARD_SPELL_RECYCLE`, `Timing = SOURCE_UNIT_PLAYED`, `TargetScope = CONTROLLED_SPELL_IN_GRAVEYARD`, `PlayCount = 1`, `PlayOriginZone = GRAVEYARD`, `PlayDestinationZone = STACK`, `PlayCardFilter = TAG:CARD_TYPE:SPELL`, `MaximumPlayedCardManaCost = 3`, `IgnorePlayManaCost = true`, `PayPlayPowerCosts = true`, `RecyclePlayedCardOnResolution = true`, and `Optional = true`.
- `SourceUnitPlayedTriggerSpecRules` reads BehaviorSpec trigger data from the catalog and accepts this family through a generic predicate. `CoreRuleEngine` does not branch on `SFD·140/221`; it resolves the trigger by enumerating `SourceUnitPlayedTriggerSpecRules.TriggersForCard(...)`.

## Runtime Evidence

- `SourceUnitPlayedTriggerTests.FizzPlaysLowCostGraveyardSpellAndRecyclesItAfterSourceUnitPlayed` verifies a real `PLAY_CARD` / pass-pass stack resolution for `SFD·140/221` 菲兹. After the source unit enters the controller's base, the shared source-unit-played TriggerSpec route chooses controlled graveyard `OGN·048/298` 冥想 with mana cost 2, emits `SOURCE_UNIT_PLAYED_EFFECT_ACTIVATED`, emits `CARD_PLAYED_FROM_GRAVEYARD` with `sourceZone = GRAVEYARD`, `destinationZone = STACK`, `ignorePlayManaCost = true`, and `payPlayPowerCosts = true`, resolves the no-target draw spell through existing stack resolution, then emits `CARDS_RECYCLED` and moves the played spell to the controller's main deck.
- The graveyard spell execution helper is shared with the existing Kai'Sa conquest representative. It is intentionally limited to no-target draw spells that can reuse existing stack resolution without extra target, rune-pool, experience, pending-payment, or token-state handoff.

## Validation

- 2026-07-03 focused source-unit-played TriggerSpec parser + runtime representative: `2/2` passing.
- 2026-07-03 adjacent SourceUnitPlayed / UnitConquest graveyard-spell helper / Stack / CardCatalogBaseline / MatchRecovery representatives: `2882/2882` passing.
- 2026-07-03 backend full conformance: `9150/9150` passing.

## Residual Risk

- Complete optional yes/no prompts, explicit graveyard spell selection, targeted spell legality, power-cost prompt routing, and broader spell effect state handoff remain open.
- This slice does not claim full official 菲兹 coverage; it only closes the no-target draw-spell representative path for the official graveyard-spell free-play and recycle text.
