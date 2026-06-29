# Plan B Source-Ready Optional-Cost Spec Evidence

Date: 2026-06-29

Project status: **NOT READY**.

## Evidence Basis

Official card data:

- `data/official/card-catalog.zh-CN.json` row `UNL-122/219` 新月禁卫 states that if its controller played a spell this turn, they may pay purple as an additional cost so it enters ready.
- The existing fixture `p2-preflight-play-crescent-guard-spell-ready-payment.fixture.json` covers the server-authoritative spell memory, `SPEND_POWER:purple:1`, `UNIT_PLAYED_TO_BASE`, ready entry, and legacy event payload key.

## Engine Evidence

Before this slice, `CoreRuleEngine` and `MatchSession` used a dedicated `CrescentGuardReadyOptionalCostSourceEffectKind` runtime constant and direct `behavior.EffectKind` checks.

After this slice:

- `CardBehaviorRegistry` stores the ready optional-cost data on the `UNL-122/219` behavior row.
- `CoreRuleEngine` validates the optional cost and source entry readiness through `SourceReadyAdditionalPowerCost`, `SourceReadyAdditionalPowerTrait`, and `SourceReadyConditionKind`.
- `MatchSession` exposes the prompt optional-cost choice and payment-resource requirements through the same fields.
- The legacy `crescentGuardReadyOptionalCostPaid` payload remains data-driven through `SourceReadyOptionalCostPayloadKey`, so existing recovery and fixture expectations stay stable.

## Test Evidence

- `PlayBehaviorSourceIdentityGuardTests.CrescentGuardReadyOptionalCostSourceUsesBehaviorFields` failed red before implementation because the engine still contained `CrescentGuardReadyOptionalCostSourceEffectKind`.
- The same guard passed after implementation and asserts `CoreRuleEngine` / `MatchSession` no longer contain the Crescent Guard effect id.
- `CoreRuleEnginePlaysCrescentGuardReadyAfterSpellPayment`, `CoreRuleEngineRejectsCrescentGuardReadyPaymentWithoutSpellMemory`, and `ActionPromptExposesCrescentGuardReadyPaymentAfterSpell` passed with unchanged behavior and payload shape.
- Adjacent / hidden-info representative gate `PlayBehaviorSourceIdentityGuardTests|CrescentGuardReady|PaymentEngineCoverageAuditTests|ConformanceFixtureShapeTests|MatchRecovery` passed `2864/2864`.
- Backend full conformance passed `9024/9024`.

## Non-Claims

This evidence does not claim complete optional-cost breadth, complete source-ready official breadth, full PaymentEngine coverage, complete cleanup/replacement duration, complete targeting-stack timing, P0 completion, or READY.
