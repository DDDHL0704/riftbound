# 4D-03FR-BD Payment-Cost Echo-Ready Automated Evidence Audit

日期：2026-06-26
结论：**FOCUSED AUTOMATED EVIDENCE ACCEPTED / PROJECT NOT READY**

本审计补强 4D-03FR-E 的 row-level candidate：`FU-05f5d81d5a / UNL-009/219 大幕渐起 / THE_CURTAIN_RISES_READY_UNIT` 不再只停留在 `IMPLEMENTED_UNTESTED` 候选口径。本批新增 focused C# conformance guard，直接覆盖 prompt、payment-cost、ECHO optional cost、targeting-stack、stack pass-pass ready 结算与 insufficient-mana no-mutation。

## Scope

- selected partition: `bd-engine-support-payment-cost`
- selected matrix row query: `payment-cost`
- selected secondary matrix row query: `payment-and-targeting-stack-timing`
- selected functionalUnit: `FU-05f5d81d5a`
- selected card: `UNL-009/219 大幕渐起`
- selected effect: `THE_CURTAIN_RISES_READY_UNIT`
- source candidate: `docs/CURRENT_STAGE4D_03FR_E_CARD_MATRIX_READINESS_PAYMENT_COST_ECHO_READY_TARGETING_STACK_BLOCKER_CLOSURE_CANDIDATE.md`

## Accepted Evidence

- New focused tests: `tests/Riftbound.ConformanceTests/TheCurtainRisesPaymentCostTests.cs`
- Prompt evidence:
  - `PLAY_CARD` exposes `UNL-009/219` as a legal source.
  - Target choices include a public battlefield unit.
  - Optional cost choices include `ECHO`.
  - source requirement records mana cost 2, target count 1, `ANY_UNIT` target scope and available mana.
- Command/payment evidence:
  - Base cast pays 2 mana and records `COST_PAID` with reason `THE_CURTAIN_RISES_READY_UNIT`.
  - ECHO cast pays base 2 plus echo 2, records `optionalCosts=["ECHO"]`, and creates a stack item with repeat count 2.
- Stack/outcome evidence:
  - Pass-pass resolves the stack item.
  - The target becomes active.
  - The spell source moves from hand to graveyard.
- Rollback evidence:
  - ECHO with only 3 available mana rejects with `INSUFFICIENT_COST`.
  - Rejection emits no events, does not advance stack, keeps hand and rune pool unchanged, and leaves `PendingPayment` null.

## Validation

- focused: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TheCurtainRisesPaymentCostTests" --nologo` -> 4/4 passed.
- adjacent: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~TheCurtainRisesPaymentCostTests|FullyQualifiedName~CoreRuleEnginePlaysTheCurtainRises|FullyQualifiedName~AnyUnitTargetScopeGuardTests|FullyQualifiedName~PaymentEngineUnificationTests" --nologo` -> 113/113 passed.
- hidden-information representative: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --filter "FullyQualifiedName~MatchRecovery" --nologo` -> 1989/1989 passed.
- backend full: `dotnet test tests/Riftbound.ConformanceTests/Riftbound.ConformanceTests.csproj --nologo` -> 8704/8704 passed.

## Non-Closure

This is focused automated evidence for one payment-cost echo-ready row only. It does not close payment-cost blocker closure, full official PaymentEngine matrix, E_CARD_MATRIX_READINESS, card matrix, P0-005, P1, or READY.
