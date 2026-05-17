# 4D-03EP-BD Card Matrix Readiness Engine-Support Payment-Cost Implementation Dispatch Audit

日期：2026-05-17
结论：**ACCEPTED AS A-SIDE B/D DISPATCH / NOT READY**

## Scope

4D-03EP-BD 接在 4D-03EO-BD 之后，只把 `payment-cost / NEEDS_ENGINE_SUPPORT=360` 转成下一步 B/D implementation 或 D-side verifier 的最小可执行写锁。A 侧本批不写 runtime、不修改矩阵 JSON、不运行 Chrome、不改 frontend，也不声明 B/D closure。

## Manifest

`PaymentEngineCoverageAuditTests` 新增 `Post03EpCardMatrixReadinessEngineSupportPaymentCostImplementationDispatchManifest`：

```txt
classification=post-03eo-bd-card-matrix-readiness-engine-support-payment-cost-implementation-dispatch
gate=B_D_ENGINE_SUPPORT_POST_03EO_BD_PAYMENT_COST_IMPLEMENTATION_DISPATCH
input engine-support row-query partition manifest=Post03EoCardMatrixReadinessEngineSupportRowQueryPartitionManifest
selected partition=bd-engine-support-payment-cost
selected matrix row query=payment-cost
selected blocker=NEEDS_ENGINE_SUPPORT
downstream owner=B/D_ENGINE_SUPPORT
```

## Payment-Cost Row Query

```txt
payment-cost functionalUnits=360
NEEDS_ENGINE_SUPPORT=360
NEEDS_AUTOMATED_TEST_EVIDENCE=328
NEEDS_FAQ_REVIEW=92
fullOfficialTrue=0
freeze statuses=IMPLEMENTED_TESTED=31; SHARED_ORACLE_IMPLEMENTATION=52; NEEDS_ENGINE_SUPPORT=216; NEEDS_FAQ_REVIEW=61
```

这说明 360 行不是单纯归档缺口：03EO-BD 已完成 partition，03EP-BD 只把它交给 B/D 做 implementation diff 或 D-side verifier evidence。

## Write Lock

- B/D worker write lock is limited to `payment-cost / NEEDS_ENGINE_SUPPORT=360`.
- Runtime implementation, if actually required later, may only touch `src/Riftbound.Engine/PaymentCostRules.cs` and the smallest local `PAY_COST` / `PaymentPlan` / legal payment choice / prompt / commit path in `src/Riftbound.Engine/MatchSession.cs`.
- D-side verifier alternative may prove existing runtime coverage for payment-cost rows without runtime changes.
- Required future acceptance evidence: implementation or verifier diff, focused `PaymentEngineCoverageAuditTests`, payment-cost row-query trace, backend full test, current `fullOfficial=false` continuity, no matrix JSON write proof and later A acceptance audit.

## Locked Scope

- Frontend and Chrome / browser scripts remain locked.
- Formal 18-step scripts remain locked.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` remains locked.
- `data/official/card-catalog.zh-CN.json` remains locked.
- `fullOfficial`, final readiness and READY remain locked.
- `riftbound-dotnet.sln` remains untouched.

## Non-Closure

4D-03EP-BD is payment-cost implementation dispatch only. It does not close `B/D_ENGINE_SUPPORT`, `P0-005`, `P0-004 adjacency audit-sensitive`, `P1`, full official PaymentEngine matrix, `E_CARD_MATRIX_READINESS`, card matrix, frontend final validation, formal 18 final validation or READY. Project remains **NOT READY**.
