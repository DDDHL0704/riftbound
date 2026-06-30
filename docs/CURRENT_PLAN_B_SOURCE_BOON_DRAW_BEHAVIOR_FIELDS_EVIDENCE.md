# Plan B Source Boon Draw Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `OGN·061/298` 魄罗牧者 states that when the source is played while the controller controls a Poro unit, the source receives boon and the controller draws one card.

Existing engine evidence:

- The engine already has a generic source-boon path through `GrantsBoonToSourceUnit` and `ApplyBoon`.
- Existing conformance coverage exercises the satisfied controlled-Poro draw path and the unsatisfied no-Poro vanilla path.

## Engine Evidence

Before this slice, `CoreRuleEngine` selected the branch through `PoroHerderBoonDrawSourceEffectKind` and direct `behavior.EffectKind` comparison.

After this slice:

- `CoreRuleEngine` no longer contains `PoroHerderBoonDrawSourceEffectKind`.
- `CoreRuleEngine` no longer contains `PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT` as a runtime selector.
- `CoreRuleEngine` no longer contains `ControllerControlsFaceUpPoroUnit`.
- `CardBehaviorRegistry` stores the official condition and draw count on the `OGN·061/298` behavior row.
- `ShouldGrantBoonToSourceUnit` reads `SourceBoonConditionKind`, `SourceBoonRequiredControlledUnitTag`, and `SourceBoonDrawCount`.
- `ControllerControlsFaceUpUnitWithTag` is shared by condition kind and rejects face-down or standby units.

## Test Evidence

- `PlayBehaviorSourceIdentityGuardTests.PoroHerderBoonDrawPlaySourceUsesBehaviorFields` blocks reintroducing the runtime effect-kind selector and requires the source-boon behavior fields.
- `CardCatalogBaselineTests.PoroHerderSourceBoonDrawCarriesOfficialControlledPoroCondition` locks the official row to controlled-unit tag `魄罗` and draw count `1`.
- Existing `P79PoroHerderGrantsBoonAndDrawsWhenControllerHasPoro` passed with unchanged source boon and draw behavior.
- Adjacent / hidden-info representative gate `PlayBehaviorSourceIdentityGuardTests|PoroHerder|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3026/3026`.
- Backend full conformance passed `9027/9027`.

## Non-Claims

This evidence does not claim complete source-boon official breadth, complete play-trigger routing, complete PaymentEngine, P0 completion, or READY.
