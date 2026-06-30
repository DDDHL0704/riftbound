# Plan B Static Spell Cost Reduction Behavior Fields Audit

Date: 2026-06-30

Project status: **NOT READY**.

## Scope

This slice removes the Eager Apprentice-specific runtime effect-kind selector from the shared play-card payment and prompt cost paths.

The stable catalog effect id `EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT` remains in `CardBehaviorRegistry`, fixtures, and matrix evidence as row identity data. `CoreRuleEngine` and `MatchSession` no longer reference that id to decide whether battlefield spell-cost reduction applies.

## Authority

- `data/official/card-catalog.zh-CN.json` row `OGN·084/298` 踊跃的学徒: while the source is on the battlefield, the controller's spell mana costs are reduced by 1 and cannot be reduced below 1 mana.
- Existing evidence index entries `p2-preflight-play-eager-apprentice-spell-cost-static` and `p2-preflight-play-eager-apprentice-spell-cost-reduction` record the official card row, `CORE-260330` unit/play/payment authorities, prompt metadata, and representative `COST_PAID.battlefieldSpellCostReductionMana=1`.
- Existing `P79BattlefieldStaticEagerApprentice*` tests cover authoritative payment, opponent-control rejection, and prompt metadata.

## Implementation

- `CardBehaviorDefinition` now carries static spell-cost reduction metadata:
  - `StaticSpellCostReductionMana`
  - `StaticSpellCostReductionMinimumManaCost`
- `OGN·084/298` fills those fields with amount `1` and minimum mana cost `1`.
- `CoreRuleEngine.ResolveBattlefieldSpellCostReductionMana` scans public, controlled source objects in the player's battlefield zones, resolves each source through `CardBehaviorRegistry.TryGetByCardNo`, and applies matching source behavior fields to spell play behavior.
- `MatchSession.PromptBattlefieldSpellCostReductionMana` mirrors the same behavior-field source scan for prompt `sourceRequirements`.
- The existing `battlefieldSpellCostReductionMana` audit metadata key is preserved to avoid protocol and recovery churn for the representative Eager Apprentice path.

## Validation

- Baseline before this slice: backend full conformance passed `9030/9030`.
- Red focused source guard failed before implementation because `StaticSpellCostReductionMana` and `StaticSpellCostReductionMinimumManaCost` did not exist.
- Green focused gate: `EagerApprenticeSpellCostSourceIdentityUsesBehaviorFields|EagerApprenticeStaticSpellCostReductionCarriesOfficialBehaviorFields|EagerApprentice` passed `9/9`.
- Adjacent / hidden-info representative gate: `EagerApprentice|BattlefieldStaticSourceIdentityGuardTests|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|P79BattlefieldStatic|MatchRecovery` passed `3036/3036`.
- Backend full conformance passed `9031/9031`.

## Holdbacks

This does not close complete static spell-cost reduction official breadth, complete static cost-modifier priority ordering, complete PaymentEngine, P0 full objective, P1, or READY.
