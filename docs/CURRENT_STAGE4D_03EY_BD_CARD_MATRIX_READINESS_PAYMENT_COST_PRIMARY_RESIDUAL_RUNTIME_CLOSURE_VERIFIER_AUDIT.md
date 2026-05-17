# 4D-03EY-BD Card Matrix Readiness Payment-Cost Primary Residual Runtime Closure Verifier Audit

日期：2026-05-17
结论：**NARROW B/D RUNTIME VERIFIER ACCEPTED / NOT READY**

## Scope

4D-03EY-BD 接在 4D-03EX-BD payment-cost primary residual runtime closure dispatch 之后。B/D 本批只处理 pending `PAY_COST` 使用 temporary payment resource 时的 runtime / audit parity：服务端已经 quote 合法 temporary payment resource，但提交 `PAY_COST` 时 `PaymentPlan.paymentResourceActionIds` 需要保留实际提交的 temporary / materialized Blue Sentinel payment resource action，保证 `COST_PAID` authoritative audit payload 与 ActionPrompt / command 一致。

## Runtime Diff

- `src/Riftbound.Engine/CoreRuleEngine.cs`：`ResolvePendingPayCost` 在 recycle rune、temporary payment resource 与 Blue Sentinel delayed resource materialization 后再构建 `PaymentPlan`。
- `BuildSubmittedPendingPaymentResourceActions` 记录提交顺序内的 recycle resource action、submitted `TEMP_PAYMENT_RESOURCE:*` action，以及 Blue Sentinel delayed resource materialized 后的 temporary action，并去重后写入 `PaymentPlan.paymentResourceActionIds`。
- `src/Riftbound.Engine/PaymentCostRules.cs` 未改；`src/Riftbound.Engine/MatchSession.cs` 未改；前端、Chrome / browser scripts、formal 18-step、card matrix JSON、official catalog、fullOfficial / READY 均未改。

## Verifier Evidence

`tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs` 新增：

- `PendingPayCostCommitsGenericTemporaryPaymentResourceThroughPaymentPlan`
- `PendingPayCostCommitsTypedTemporaryPaymentResourceThroughPaymentPlan`

这些 verifier 证明 pending `PAY_COST` 成功消耗 generic / typed temporary payment resource 后：

- `PendingPayment` 清空；
- `TemporaryPaymentResources` 清理；
- `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `TEMPORARY_PAYMENT_RESOURCE_CLEARED` / `COST_PAID` / `PAYMENT_WINDOW_CLOSED` 顺序稳定；
- `COST_PAID.paymentResourceActions` 包含提交的 temporary payment resource action；
- `temporaryPaymentResourceIds`、`temporaryPaymentResourcePower`、`paymentChoiceIds`、`legalPaymentChoiceIds` 与 typed `powerByTrait` audit payload 保持服务端权威。

## Manifest

`PaymentEngineCoverageAuditTests` 新增 `Post03EyCardMatrixReadinessPaymentCostPrimaryResidualRuntimeClosureVerifierEvidenceManifest`：

```txt
classification=post-03ex-bd-card-matrix-readiness-payment-cost-primary-residual-runtime-closure-verifier-evidence
gate=B_D_ENGINE_SUPPORT_POST_03EX_PAYMENT_COST_PRIMARY_RESIDUAL_RUNTIME_CLOSURE_VERIFIER_EVIDENCE
input runtime closure dispatch manifest=Post03ExCardMatrixReadinessPaymentCostPrimaryResidualRuntimeClosureDispatchManifest
selected partition=bd-engine-support-payment-cost
selected matrix row query=payment-cost
selected blocker=NEEDS_ENGINE_SUPPORT
dispatch owner=B/D_ENGINE_SUPPORT
dispatch lane=lane-1-bd-primary-engine-support-residual
```

## Row Query

```txt
payment-cost functionalUnits=360
NEEDS_ENGINE_SUPPORT=360
NEEDS_AUTOMATED_TEST_EVIDENCE=328
NEEDS_FAQ_REVIEW=92
freeze statuses=IMPLEMENTED_TESTED=31; SHARED_ORACLE_IMPLEMENTATION=52; NEEDS_ENGINE_SUPPORT=216; NEEDS_FAQ_REVIEW=61
primary residual=216
fullOfficialTrue=0
ready=false
```

## Validation

```txt
focused PaymentEngineUnificationTests=42/42
focused BlueSentinelResourceSkillTests=12/12
focused PaymentEngineCoverageAuditTests=274/274
backend full current HEAD=4845/4845
git diff --check=passed
```

Chrome smoke not run because there were no frontend or browser-script changes.

## Non-Closure

4D-03EY-BD accepts only one narrow runtime/verifier surface. It does not rewrite card matrix counts and does not authorize matrix JSON writes. payment-cost blocker closure remains open；B/D_ENGINE_SUPPORT remains open；P0-005 remains open；P0-004 adjacency audit-sensitive remains open；P1 remains open；full official PaymentEngine matrix closure remains open；E_CARD_MATRIX_READINESS remains open；card matrix remains open；READY remains open。
