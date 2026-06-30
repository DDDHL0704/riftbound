# Plan B Static Spell Cost Reduction Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·084/298` 踊跃的学徒 states that while the source is on the battlefield, the controller's spell mana costs are reduced by 1 and cannot cost less than 1 mana.

Existing engine evidence:

- `P79BattlefieldStaticEagerApprenticeReducesSpellCost` proves authoritative payment reduction and `COST_PAID.battlefieldSpellCostReductionMana=1`.
- `P79BattlefieldStaticEagerApprenticeSkipsOpponentControlledSource` proves an opponent-controlled dirty source does not reduce the spell cost.
- `P79BattlefieldStaticEagerApprenticePromptShowsSpellCostReduction` proves prompt `sourceRequirements` expose the reduced cost and metadata.
- `docs/rules-evidence-index.md` already records the `p2-preflight-play-eager-apprentice-spell-cost-static`, `p2-preflight-play-eager-apprentice-spell-cost-reduction`, and target-rejection evidence rows.

## Engine Evidence

Before this slice, `CoreRuleEngine` and `MatchSession` selected the branch through `EagerApprenticeSpellCostStaticSourceEffectKind = EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT` and applied fixed amount/floor logic.

After this slice:

- `CoreRuleEngine` no longer contains `EagerApprenticeSpellCostStaticSourceEffectKind`.
- `MatchSession` no longer contains `EagerApprenticeSpellCostStaticSourceEffectKind`.
- `CoreRuleEngine` and `MatchSession` no longer contain `EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT` as a runtime selector.
- `CardBehaviorRegistry` stores the official amount and minimum cost on the `OGN·084/298` behavior row.
- `CoreRuleEngine` and `MatchSession` scan public controlled battlefield source objects and apply any matching `StaticSpellCostReduction*` behavior fields.

## Test Evidence

- `BattlefieldStaticSourceIdentityGuardTests.EagerApprenticeSpellCostSourceIdentityUsesBehaviorFields` blocks reintroducing the Eager Apprentice runtime effect-kind selector in Core or MatchSession.
- `CardCatalogBaselineTests.EagerApprenticeStaticSpellCostReductionCarriesOfficialBehaviorFields` locks the official row to amount `1` and minimum mana cost `1`.
- Existing Eager Apprentice focused regressions passed unchanged, proving prompt/payment behavior is preserved.
- Baseline before this slice: backend full conformance passed `9030/9030`.
- Focused behavior-field gate passed `9/9`.
- Adjacent / hidden-info representative gate `EagerApprentice|BattlefieldStaticSourceIdentityGuardTests|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|P79BattlefieldStatic|MatchRecovery` passed `3036/3036`.
- Backend full conformance passed `9031/9031`.

## Non-Claims

This evidence does not claim complete static spell-cost reduction official breadth, complete static cost-modifier priority ordering, complete PaymentEngine, P0 completion, P1, or READY.
