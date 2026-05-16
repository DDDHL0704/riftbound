# 4D-03DC PaymentEngine Official Breadth Concrete B Dispatch Audit

日期：2026-05-16
结论：**ACCEPTED A-SIDE DISPATCH CONTRACT / PROJECT NOT READY**

## 范围

4D-03DC 承接 4D-03DB。03DB 已把 03CX / 03CY / 03CZ / 03DA runtime/card-row evidence 与 03CV 192-row matrix 固定为 `B_PAYMENT_ENGINE_OFFICIAL_BREADTH` representative proxy evidence only；本批不再停留在抽象 “future B” 文案，而是把下一枚具体 B-side official breadth 切片锁定为：

`B_PAYMENT_ENGINE_RESOURCE_SKILL_RUNTIME_CARD_ROW_PARITY_VERIFIER`

该切片的目标是从当前 32 个 official resource-skill candidates 中选择一组 high-signal source-card groups，把 `ResourceSkillOfficialRuntimeCardRowEvidenceManifest` 与 `ResourceSkillOfficialRowInteractionMatrixManifest` 转成可执行的 prompt / command / audit / generated-resource lifetime / rollback / source-card / official card-row parity verifier。建议首批覆盖 Malzahar、Lux、Dragon Soul Sage、conversion resource skill、Gold token 与至少一个 `LegendResourceBridgeResourceSkillClosureManifest` bridge-closed group。若 verifier 暴露具体服务端 mismatch，B 只能做最小修复。

## 本批改动

- `tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`
  - 更新 `RemainingOfficialClosureGateManifest` 的 `B_PAYMENT_ENGINE_OFFICIAL_BREADTH` gate，把 4D-03DC 记录为具体 B dispatch contract。
  - 新增 `PaymentEngineOfficialBreadthGateRecordsConcreteBDispatchContractAfterRuntimeCardRowEvidence`，防止 03DC 被误读为 P0-005、`fullOfficial` 或 READY closure。
- `docs/CURRENT_STAGE4D_03DC_PAYMENT_ENGINE_OFFICIAL_BREADTH_CONCRETE_B_DISPATCH_AUDIT.md`
- `docs/CURRENT_STAGE4D_03DC_PAYMENT_ENGINE_OFFICIAL_BREADTH_CONCRETE_B_DISPATCH_EVIDENCE.md`

## Future B 写锁

4D-03DC 只打开下一步 B 的合同边界，不在本批实现 runtime。B 后续必须保持以下写锁：

- primary verifier：`tests/Riftbound.ConformanceTests/PaymentEngineCoverageAuditTests.cs`，以及被选中 resource-skill row 已有的 focused conformance verifier 文件。
- runtime optional only if verifier exposes a concrete mismatch：`src/Riftbound.Engine/P4ActivatedAbilityCatalog.cs`、`src/Riftbound.Engine/CoreRuleEngine.cs`、`src/Riftbound.Engine/MatchSession.cs`。
- docs：未来 4D-03DC-B audit / evidence pair，以及 A-side checkpoint / completion / dispatch docs。

## No-Go Scope

本批与后续 B worker 都不得在没有新 A 写锁的情况下触碰：

- frontend runtime、Chrome / browser scripts、formal 18-step scripts；
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`；
- broad PaymentEngine rewrite、LayerEngine、battle lifecycle、hidden-info redaction；
- `fullOfficial` / READY status；
- `riftbound-dotnet.sln`。

## 验收口径

4D-03DC 不是 B implementation acceptance，也不是 full official closure。验收只要求：

1. `B_PAYMENT_ENGINE_OFFICIAL_BREADTH` gate 明确记录 4D-03DC concrete B dispatch contract。
2. 选中切片必须绑定 selected high-signal source-card groups、03CY runtime/card-row evidence、03CV row-interaction matrix 与 source-card / official card-row parity。
3. `PaymentEngineCoverageAuditTests` focused guard 160/160 通过。
4. Adjacent PaymentEngine / resource skill / prompt / GameHub regression 664/664 通过。
5. Backend full 4729/4729 通过。
6. `git diff --check` 通过。

## 非闭合项

P0-005、P1、full official PaymentEngine matrix、完整 `[A]` / `[C]` resource-skill runtime/card-row interactions、完整 target-bearing activated ability official family、full-card matrix、final frontend state reruns 与 READY 仍未关闭。项目仍 **NOT READY**。
