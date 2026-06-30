# Plan B Static Unit Cost Reduction Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the Dragon Caller-specific runtime selector and fixed constants from the shared play-card payment and prompt cost paths.

The stable catalog effect id `DRAGON_CALLER_COST_STATIC_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. `CoreRuleEngine` and `MatchSession` no longer reference that id to decide whether a field static unit-cost reduction applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·140/298` 唤龙使者: the controller's Dragon units cost 2 less and cannot be reduced below 1 mana.
- Existing evidence index entry `p2-preflight-play-dragon-caller-cost-static` records the official card row and `CORE-260330` unit/play/payment authorities.
- Existing `DragonCallerCostStaticTests` cover prompt reduction, payment reduction, public controlled source requirements, face-down/non-controller rejection, non-Dragon rejection, source removal recomputation, stacking, and the one-mana floor.

## Implementation

- `CardBehaviorDefinition` now carries static unit-cost reduction metadata:
  - `StaticUnitCostReductionMana`
  - `StaticUnitCostReductionRequiredUnitTag`
  - `StaticUnitCostReductionMinimumManaCost`
- `OGN·140/298` fills those fields with amount `2`, required unit tag `龙`, and minimum mana cost `1`.
- `CoreRuleEngine.ResolveStaticUnitCostReductionMana` scans public, controlled field unit sources, resolves each source through `CardBehaviorRegistry.TryGetByCardNo`, and applies matching source behavior fields to the played unit behavior.
- `MatchSession.PromptStaticUnitCostReductionMana` mirrors the same behavior-field source scan for prompt `sourceRequirements`.
- The existing `dragonUnitCostReductionMana` audit metadata key is preserved to avoid protocol and recovery churn for the representative Dragon Caller path.

## Validation

- Red focused source guard failed before implementation because `StaticUnitCostReductionMana`, `StaticUnitCostReductionRequiredUnitTag`, and `StaticUnitCostReductionMinimumManaCost` did not exist.
- Green focused gate: `DragonCallerUnitCostStaticSourceUsesBehaviorFields|DragonCallerStaticUnitCostReductionCarriesOfficialBehaviorFields|DragonCallerCostStaticTests` passed `11/11`.
- Adjacent / hidden-info representative gate: `DragonCaller|PlayBehaviorSourceIdentityGuardTests|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3037/3037`.
- Backend full conformance passed `9030/9030`.

## Holdbacks

This does not close complete static unit-cost reduction official breadth, complete static cost-modifier priority ordering, complete PaymentEngine, P0 full objective, P1, or READY.
