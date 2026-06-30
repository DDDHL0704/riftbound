# Plan B Static Unit Cost Reduction Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·140/298` 唤龙使者 states that the controller's Dragon units cost 2 less and cannot cost less than 1 mana.

Existing engine evidence:

- `DragonCallerCostStaticTests` already proves the representative rule behavior across prompt, authoritative payment, source visibility/control, source removal, stacking, non-Dragon rejection, and the one-mana floor.
- `docs/rules-evidence-index.md` already records the `p2-preflight-play-dragon-caller-cost-static` and target-rejection evidence rows for the official card.

## Engine Evidence

Before this slice, `CoreRuleEngine` and `MatchSession` selected the branch through `DragonCallerCostStaticSourceEffectKind = DRAGON_CALLER_COST_STATIC_PLAY_UNIT` and applied fixed constants `DragonCallerUnitCostReductionMana = 2` and `DragonCallerMinimumUnitManaCost = 1`.

After this slice:

- `CoreRuleEngine` no longer contains `DragonCallerCostStaticSourceEffectKind`.
- `MatchSession` no longer contains `DragonCallerCostStaticSourceEffectKind`.
- `CoreRuleEngine` and `MatchSession` no longer contain `DRAGON_CALLER_COST_STATIC_PLAY_UNIT` as a runtime selector.
- `CoreRuleEngine` and `MatchSession` no longer contain `DragonCallerUnitCostReductionMana` or `DragonCallerMinimumUnitManaCost`.
- `CardBehaviorRegistry` stores the official amount, required target unit tag, and minimum cost on the `OGN·140/298` behavior row.
- `CoreRuleEngine` and `MatchSession` scan public controlled field unit sources and apply any matching `StaticUnitCostReduction*` behavior fields.

## Test Evidence

- `PlayBehaviorSourceIdentityGuardTests.DragonCallerUnitCostStaticSourceUsesBehaviorFields` blocks reintroducing the Dragon Caller runtime effect-kind selector and fixed constants in Core or MatchSession.
- `CardCatalogBaselineTests.DragonCallerStaticUnitCostReductionCarriesOfficialBehaviorFields` locks the official row to amount `2`, required tag `龙`, and minimum mana cost `1`.
- Existing `DragonCallerCostStaticTests` passed unchanged, proving the prompt/payment behavior is preserved.
- Adjacent / hidden-info representative gate `DragonCaller|PlayBehaviorSourceIdentityGuardTests|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3037/3037`.
- Backend full conformance passed `9030/9030`.

## Non-Claims

This evidence does not claim complete static unit-cost reduction official breadth, complete static cost-modifier priority ordering, complete PaymentEngine, P0 completion, P1, or READY.
