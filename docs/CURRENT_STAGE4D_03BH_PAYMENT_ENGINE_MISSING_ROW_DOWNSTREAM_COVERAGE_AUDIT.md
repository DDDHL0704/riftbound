# Stage 4D-03BH PaymentEngine Missing Row Downstream Coverage Audit

审计日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批在 4D-03BE / 03BF / 03BG 分别拆解 rollback failure、cross-window generation / consumption、card matrix alignment 三个 missing official rows 后，补一层聚合 verifier：确保 4D-03BC 的所有 `missing-official-row` 都有 downstream representative manifest 覆盖，且 `ROW_MOVE_UNIT_POLICY_DEFERRED` 仍保持 policy-deferred，不被误算为 PaymentEngine payment row。

本批只修改 conformance test 与 docs，不修改 runtime、frontend、card matrix JSON，不升级 `fullOfficial=true`，不关闭 P0-005 或 READY。

## 2. Coverage Contract

`PaymentEngineOfficialMatrixSeedRowManifest` 当前允许三类 row status：

- `representative-seed`
- `missing-official-row`
- `policy-deferred-row`

本批新增的聚合 verifier 要求：

- `missing-official-row` 精确等于 3 行：
  - `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING`
  - `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING`
  - `ROW_CARD_MATRIX_ALIGNMENT_MISSING`
- downstream manifests 精确覆盖上述 3 行：
  - `RollbackFailureRowManifest`
  - `CrossWindowGenerationConsumptionRowManifest`
  - `CardMatrixAlignmentRowManifest`
- 每个 missing row 都保留本审计 doc anchor。
- downstream manifest family 名称不得重复。
- `ROW_MOVE_UNIT_POLICY_DEFERRED` 不进入 downstream representative manifests。
- 聚合文本仍保留 NOT READY / P0-005 remains open closure，并不得出现 `FullOfficialRulePass` 或 `fullOfficial=true`。

## 3. Guards Added

新增 focused verifier 覆盖以下约束：

- `PaymentEngineOfficialMatrixMissingRowsAllHaveDownstreamRepresentativeManifests`
- `PaymentEngineOfficialMatrixMissingRowCoverageKeepsDownstreamFamiliesAndDocsVisible`
- `PaymentEngineOfficialMatrixMissingRowCoverageDoesNotClaimP0005Closure`

## 4. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 64/64 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 622/622 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4501/4501 passed

## 5. Remaining Risk

4D-03BH 只证明 missing official rows 的 downstream representative routing 完整、可回归、且没有误把 policy-deferred MOVE_UNIT 纳入 PaymentEngine payment row。它不生成 full official matrix，不修改 card matrix JSON，不证明每个官方卡牌支付分支和失败分支已覆盖，也不替代 frontend final validation 或 completion audit。

项目仍 **NOT READY**；P0-005 remains open.
