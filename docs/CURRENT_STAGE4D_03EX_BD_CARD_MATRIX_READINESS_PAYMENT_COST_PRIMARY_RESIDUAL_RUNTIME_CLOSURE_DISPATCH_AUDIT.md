# 4D-03EX-BD Card Matrix Readiness Payment-Cost Primary Residual Runtime Closure Dispatch Audit

日期：2026-05-17
结论：**DISPATCH OPENED FOR B/D RUNTIME + VERIFIER / NOT READY**

## Scope

4D-03EX-BD 接在 4D-03EW-E payment-cost matrix readiness gate-hold evidence 之后。A 主控本批只打开 fresh B/D runtime + verifier 写窗，把 `payment-cost / NEEDS_ENGINE_SUPPORT` 的 primary residual=216 交给 B/D 做后续实现或 verifier closure slice；A 不亲自写 runtime。

## Manifest

`PaymentEngineCoverageAuditTests` 新增 `Post03ExCardMatrixReadinessPaymentCostPrimaryResidualRuntimeClosureDispatchManifest`：

```txt
classification=post-03ew-bd-card-matrix-readiness-payment-cost-primary-residual-runtime-closure-dispatch
gate=B_D_ENGINE_SUPPORT_POST_03EW_PAYMENT_COST_PRIMARY_RESIDUAL_RUNTIME_CLOSURE_DISPATCH
input matrix readiness gate-hold evidence manifest=Post03EwCardMatrixReadinessPaymentCostMatrixReadinessGateHoldEvidenceManifest
input payment-cost primary residual verifier evidence manifest=Post03EtCardMatrixReadinessEngineSupportPaymentCostPrimaryResidualVerifierEvidenceManifest
selected partition=bd-engine-support-payment-cost
selected matrix row query=payment-cost
selected blocker=NEEDS_ENGINE_SUPPORT
dispatch owner=B/D_ENGINE_SUPPORT
dispatch lane=lane-1-bd-primary-engine-support-residual
```

## Write Lock

runtime write lock opened for B/D only:

- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs` only for `ResolvePendingPayCost` / `BuildPendingPaymentPlan` / `PaymentPlan` commit path
- `src/Riftbound.Engine/MatchSession.cs` only for `PAY_COST` prompt / pending payment snapshot / payment metadata surfaces

focused test write lock opened for B/D only:

- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- optional new `tests/Riftbound.ConformanceTests/PaymentCostPrimaryResidualClosureTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs` for A acceptance guard only

docs write lock opened for A/D current-state dispatch, baseline evidence, row-query trace and no matrix JSON proof only.

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

## Locked Scope

Frontend, Chrome / browser scripts, formal 18-step scripts, `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`, `data/official/card-catalog.zh-CN.json`, `fullOfficial`, READY and `riftbound-dotnet.sln` remain locked. Matrix readiness gate remains held; matrix JSON write not authorized.

## Acceptance

Future B/D acceptance requires implementation or verifier diff, focused `PaymentEngineUnificationTests` / `PaymentCostPrimaryResidualClosureTests` evidence, focused `PaymentEngineCoverageAuditTests` evidence, payment-cost row-query trace, backend full test, current `fullOfficial=false` continuity, no matrix JSON write proof and later A acceptance audit.

Current A-side dispatch validation: focused `PaymentEngineCoverageAuditTests` 272/272, backend full 4841/4841, `git diff --check` passed.

## Non-Closure

4D-03EX-BD is dispatch only. Project remains **NOT READY**. payment-cost blocker closure remains open；B/D_ENGINE_SUPPORT remains open；P0-005 remains open；P0-004 adjacency audit-sensitive remains open；P1 remains open；full official PaymentEngine matrix closure remains open；E_CARD_MATRIX_READINESS remains open；card matrix remains open；READY remains open。

Chrome smoke not run because there were no frontend or browser-script changes.
