# Stage 4D-03BJ PaymentEngine Representative Seed Upstream Coverage Audit

审计日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批在 4D-03BH 确认 3 个 `missing-official-row` 均有 downstream representative manifest 后，补齐对称的 seed-row 聚合 verifier：确保 4D-03BC 的 9 个 `representative-seed` row 都精确回连到对应上游 audit manifest，并保持 prompt / command / audit / rollback evidence 分离。

本批只修改 conformance test 与 docs，不修改 runtime、frontend、card matrix JSON，不升级 `fullOfficial=true`，不关闭 P0-005 或 READY。

## 2. Coverage Contract

`PaymentEngineOfficialMatrixSeedRowManifest` 当前包含：

- 9 个 `representative-seed`
- 3 个 `missing-official-row`
- 1 个 `policy-deferred-row`

本批新增的聚合 verifier 要求：

- 9 个 `representative-seed` row 精确等于当前 seed list。
- 每个 seed row 保留对应 residual axis。
- 每个 seed row 回连到 2 个上游 audit docs，例如 action-window / typed-resource、resource-skill / Malzahar、target-tax / Spellshield、keyword / HASTE_READY、Azir optional branch、battlefield held replacement、trigger payment 等。
- seed row 不得回连 4D-03BH missing-row downstream aggregate doc。
- seed row 的 prompt / command / audit anchor 不得含 `Missing official row` 口径。
- seed row 的 prompt / command / audit / no-mutation rollback evidence 必须保持分离。
- 聚合文本仍保留 NOT READY / P0-005 remains open closure，并不得出现 `FullOfficialRulePass` 或 `fullOfficial=true`。

## 3. Guards Added

新增 focused verifier 覆盖以下约束：

- `PaymentEngineOfficialMatrixRepresentativeSeedRowsAllHaveUpstreamManifestAnchors`
- `PaymentEngineOfficialMatrixRepresentativeSeedRowsKeepPromptCommandAuditAndRollbackEvidenceDistinct`
- `PaymentEngineOfficialMatrixRepresentativeSeedCoverageDoesNotClaimP0005Closure`

## 4. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 67/67 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 625/625 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4504/4504 passed

## 5. Remaining Risk

4D-03BJ 只证明 representative seed rows 的 upstream audit routing 完整、可回归，且没有与 missing-row / policy-deferred row 混淆。它不生成 full official matrix，不修改 card matrix JSON，不证明每个官方卡牌支付分支和失败分支已覆盖，也不替代 frontend final validation 或 completion audit。

项目仍 **NOT READY**；P0-005 remains open.
