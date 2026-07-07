# Plan B Sea Monster Hook Activated Ability Audit

Date: 2026-07-07

Project status: **NOT READY**.

## Scope

This slice opens the first BehaviorSpec-driven representative path for `OGN·242/298` Sea Monster Hook / 海兽钓钩 activated ability:

- parse the official activated ability text into `BehaviorSpec.ActivatedAbilities`;
- derive a `P4ActivatedAbilityDefinition` from BehaviorSpec instead of a card-number runtime table;
- expose the legal `ACTIVATE_ABILITY` prompt for ready base equipment sources and friendly unit targets;
- pay `1` mana plus `1` yellow rune power, exhaust the source, and put the ability on the stack;
- on stack resolution, destroy the targeted friendly unit, look at the top five main-deck cards without public reveal, auto-play exactly one eligible unit when the legal set is unique, or open a private controller-only card choice when multiple eligible units are available;
- resolve `CHOOSE_CARDS` by playing the selected eligible unit for free or accepting an empty choice, then privately recycle the remaining looked cards.

2026-07-06 follow-up: added B0 official-deck-derived full-game replay coverage for the same representative path. The replay starts from legal official decks, stages a midgame Sea Monster Hook activation, verifies action-log replay to final state hash, and continues to score victory without changing the engine implementation.

2026-07-07 follow-up: added B0 official-deck-derived full-game replay coverage for the multi-eligible top-five branch. The replay reaches the generic private `CARD_CHOICE` prompt, submits `CHOOSE_CARDS` through the action log, verifies replay to the same final state hash, and continues to score victory.

2026-07-07 follow-up: added focused and B0 official-deck-derived replay coverage for the zero-eligible top-five branch. The replay verifies that no `CARD_CHOICE` window opens, all looked cards are recycled privately, action-log replay reaches the same final state hash, and the game can still continue to score victory.

## Authority

Official card data:

- `data/official/card-catalog.zh-CN.json`, `OGN·242/298`, cardName `海兽钓钩`, category `equipment`, color `yellow`.
- Official text: `支付{{1}}和{{黄色}}，{{横置}}：摧毁一名友方单位，然后查看主牌堆顶部的五张牌。你可以选择从中打出一名战力比被摧毁单位最多高1点的单位卡牌，无视费用，然后回收其余的卡牌。`

The prior Sea Monster Hook evidence was only the ordinary 0-target equipment play guard:

- `docs/CURRENT_STAGE4C_BATCH40_SEA_MONSTER_HOOK_PLAY_GUARD_AUDIT.md`
- `docs/CURRENT_STAGE4C_BATCH40_SEA_MONSTER_HOOK_PLAY_GUARD_EVIDENCE.md`

## Implementation

- `ActivatedAbilitySpec` now carries generic cost, target, source-zone, deck-look, free-play, and recycle metadata.
- `ActivatedAbilityParser` merges multi-sentence activated ability text and emits `DESTROY_FRIENDLY_UNIT_LOOK_TOP_PLAY_POWER_PLUS_ONE_RECYCLE_REST`.
- `P4ActivatedAbilityCatalog` derives this ability row from `BehaviorSpecCatalogBuilder`.
- `MatchSession` builds target choices from the generic friendly-unit scope.
- `CoreRuleEngine` resolves activation payment through `PaymentCostRules` and resolves the stack effect without emitting `CARDS_REVEALED`.
- `FullGameEndToEndTests` now includes B0 score-victory replays that exercise the BehaviorSpec-derived prompt, payment, stack resolution, private top-five look, unique eligible unit auto-play, multi-eligible private card choice, zero-eligible recycle-all, recycle event, action log, and final hash replay path.
- `CommandTypes.ChooseCards`, `PromptTypes.CardChoice`, and `PendingCardChoiceState` provide the generic private card-choice window used by Sea Monster Hook multi-eligible top-five resolution. Non-choosing players receive only the pending window summary and counts, not legal/context object ids.
- `FullGameEndToEndTests.RawCommand(...)` now preserves `ChooseCardsCommand` payload fields so B0 action-log replay can recover private card-choice submissions deterministically.

## Validation

- Focused B0 replay: `1/1`.
- Focused multi-eligible B0 replay: `1/1`.
- Focused zero-eligible guard: `1/1`.
- Focused zero-eligible B0 replay: `1/1`.
- Focused SeaMonsterHook guard: `13/13`.
- Focused SeaMonsterHook + MatchRecovery hidden-info regression: `2001/2001`.
- Adjacent SeaMonsterHook / CardChoice / ChooseCards / FullGameEndToEnd / MatchRecovery / PaymentEngine regression: `2926/2926`.
- Backend full conformance: `9196/9196`.

## Holdbacks

Full official Sea Monster Hook remains open: FAQ adjudication, complete top-five hidden-info UX across clients/recovery, full PaymentEngine matrix, card-matrix fullOfficial, P0/P1, and READY are not closed.
