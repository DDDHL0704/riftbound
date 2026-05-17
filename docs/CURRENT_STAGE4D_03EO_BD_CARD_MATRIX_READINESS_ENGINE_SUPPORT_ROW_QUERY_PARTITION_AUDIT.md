# 4D-03EO-BD Card Matrix Readiness Engine-Support Row-Query Partition Audit

日期：2026-05-17
结论：**ACCEPTED AS TEST/DOCS-ONLY PARTITION / NOT READY**

## Scope

4D-03EO-BD 把 4D-03EN-BD 的 `B/D_ENGINE_SUPPORT / NEEDS_ENGINE_SUPPORT=1926` handoff 拆成可执行的 row-query partitions。A 侧只记录分区、首个建议实现切片和写锁边界；本批不实现 runtime，不运行 Chrome，不修改 frontend，不打开 matrix JSON 写窗。

## Manifest

`PaymentEngineCoverageAuditTests` 新增 `Post03EoCardMatrixReadinessEngineSupportRowQueryPartitionManifest`：

```txt
classification=post-03en-bd-card-matrix-readiness-engine-support-row-query-partition
gate=B_D_ENGINE_SUPPORT_POST_03EN_BD_ENGINE_SUPPORT_ROW_QUERY_PARTITION
input engine-support implementation handoff manifest=Post03EnCardMatrixReadinessEngineSupportImplementationHandoffManifest
input blocker disposition manifest=Post03EgCardMatrixReadinessJsonWriteAuthorizationBlockerDispositionVerifierManifest
downstream owner=B/D_ENGINE_SUPPORT
selected lane=lane-3-bd-engine-support-fresh-dispatch
selected owner workstream=B/D_ENGINE_SUPPORT / NEEDS_ENGINE_SUPPORT=1926
```

## Row-Query Partitions

```txt
all-functional-units / NEEDS_ENGINE_SUPPORT=762
payment-cost / NEEDS_ENGINE_SUPPORT=360
payment-or-targeting-stack-timing / NEEDS_ENGINE_SUPPORT=548
payment-and-targeting-stack-timing / NEEDS_ENGINE_SUPPORT=256
total NEEDS_ENGINE_SUPPORT row-query blocker hits=1926
```

The first recommended B/D implementation or D-side verifier slice is `payment-cost / NEEDS_ENGINE_SUPPORT=360`, because it is the narrowest PaymentEngine cost surface before broader timing or all-functional-unit partitions.

## Locked Scope

- Runtime implementation remains locked.
- Frontend and Chrome / browser scripts remain locked.
- Formal 18-step scripts remain locked.
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json` remains locked.
- `data/official/card-catalog.zh-CN.json` remains locked.
- `fullOfficial`, final readiness and READY remain locked.
- `riftbound-dotnet.sln` remains untouched.

## Non-Closure

4D-03EO-BD is only an A-side row-query partition. It does not close `B/D_ENGINE_SUPPORT`, `P0-005`, `P0-004 adjacency audit-sensitive`, `P1`, full official PaymentEngine matrix, `E_CARD_MATRIX_READINESS`, card matrix, frontend final validation, formal 18 final validation or READY. Project remains **NOT READY**.
