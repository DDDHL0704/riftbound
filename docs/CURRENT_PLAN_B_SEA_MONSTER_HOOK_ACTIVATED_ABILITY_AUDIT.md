# Plan B Sea Monster Hook Activated Ability Audit

Date: 2026-07-06

Project status: **NOT READY**.

## Scope

This slice opens the first BehaviorSpec-driven representative path for `OGN·242/298` Sea Monster Hook / 海兽钓钩 activated ability:

- parse the official activated ability text into `BehaviorSpec.ActivatedAbilities`;
- derive a `P4ActivatedAbilityDefinition` from BehaviorSpec instead of a card-number runtime table;
- expose the legal `ACTIVATE_ABILITY` prompt for ready base equipment sources and friendly unit targets;
- pay `1` mana plus `1` yellow rune power, exhaust the source, and put the ability on the stack;
- on stack resolution, destroy the targeted friendly unit, look at the top five main-deck cards without public reveal, auto-play exactly one eligible unit when the legal set is unique, and recycle the rest.

The implementation is intentionally representative. It does not implement the full hidden controller-only choice prompt for multiple eligible units.

2026-07-06 follow-up: added B0 official-deck-derived full-game replay coverage for the same representative path. The replay starts from legal official decks, stages a midgame Sea Monster Hook activation, verifies action-log replay to final state hash, and continues to score victory without changing the engine implementation.

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
- `FullGameEndToEndTests` now includes a B0 score-victory replay that exercises the BehaviorSpec-derived prompt, payment, stack resolution, private top-five look, unique eligible unit play, recycle event, action log, and final hash replay path.

## Validation

- Focused B0 replay: `1/1`.
- Adjacent SeaMonsterHook / FullGameEndToEnd / MatchRecovery / PaymentEngine regression: `2921/2921`.
- Backend full conformance: `9191/9191`.

## Holdbacks

Full official Sea Monster Hook remains open: multi-eligible controller choice prompt, zero-eligible optional decision surface, FAQ adjudication, complete top-five hidden-info UX, full PaymentEngine matrix, card-matrix fullOfficial, P0/P1, and READY are not closed.
