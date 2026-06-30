# Plan B Balanced Disciple Source Draw Behavior Fields Evidence

Date: 2026-06-30

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `UNL-097/219` 均衡门徒 states that when the source is played, if the controller's other units have total power at least 5, the controller draws one card.

Existing engine evidence:

- The engine already computes other controlled unit power with `SumOtherControlledUnitPower`.
- Existing conformance fixtures cover the satisfied and unsatisfied condition paths for Balanced Disciple.

## Engine Evidence

Before this slice, `CoreRuleEngine` selected the branch through `BalancedDiscipleOtherPowerDrawSourceEffectKind` and direct `behavior.EffectKind` comparison.

After this slice:

- `CoreRuleEngine` no longer contains `BalancedDiscipleOtherPowerDrawSourceEffectKind`.
- `CoreRuleEngine` no longer contains `BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT` as a runtime selector.
- `CardBehaviorRegistry` stores the official condition and draw count on the `UNL-097/219` behavior row.
- `TryResolveSourceUnitConditionalDraw` reads `SourceDrawConditionKind`, `SourceDrawCount`, and `SourceDrawRequiredOtherControlledUnitPower`.

## Test Evidence

- `PlayBehaviorSourceIdentityGuardTests.BalancedDiscipleOtherPowerDrawPlaySourceUsesBehaviorFields` failed red before implementation because `CoreRuleEngine` still contained `BalancedDiscipleOtherPowerDrawSourceEffectKind`.
- The same guard now blocks reintroducing the runtime effect-kind selector and requires the source draw behavior fields.
- `CardCatalogBaselineTests.BalancedDiscipleSourceDrawCarriesOfficialOtherPowerCondition` locks the official row to threshold `5` and draw count `1`.
- `ConformanceFixtureRunnerTests.CoreRuleEnginePlaysBalancedDiscipleOtherPowerDraw` passed with unchanged draw behavior.
- Adjacent / hidden-info representative gate `PlayBehaviorSourceIdentityGuardTests|BalancedDisciple|CardCatalogBaselineTests|PaymentEngineCoverageAuditTests|MatchRecovery` passed `3024/3024`.
- Backend full conformance passed `9026/9026`.

## Non-Claims

This evidence does not claim complete conditional source draw official breadth, complete play-trigger routing, complete PaymentEngine, P0 completion, or READY.
