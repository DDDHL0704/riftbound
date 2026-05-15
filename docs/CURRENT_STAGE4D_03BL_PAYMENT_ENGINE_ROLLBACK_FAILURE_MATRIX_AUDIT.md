# Stage 4D-03BL-B PaymentEngine Rollback Failure Matrix Audit

审计日期：2026-05-16
结论：**B-SIDE VERIFIER COMPLETE / A ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批接续 4D-03BE 的 rollback failure representative family manifest，把 `ROW_ROLLBACK_FAILURES_OFFICIAL_MATRIX_MISSING` 扩展为 executable all-window rollback failure matrix。

本批只修改 conformance test 与 4D-03BL audit / evidence docs；未修改 runtime、frontend、browser smoke scripts、card matrix JSON、`fullOfficial`、READY 或 `riftbound-dotnet.sln`。

## 2. Matrix Contract

`PaymentEngineCoverageAuditTests` 现在生成 42 行 rollback failure all-window matrix：

- 6 个当前 PaymentEngine payment surfaces：`PLAY_CARD`、`PAY_COST`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT`、`TRIGGER_PAYMENT`、`BATTLEFIELD_HELD_SCORE_PAYMENT`
- 7 个 4D-03BE rollback failure representative families：`STALE_PROMPT_PENDING_PAYMENT`、`INVALID_PAYMENT_SOURCE_OR_TRAIT`、`INSUFFICIENT_COST_OR_TARGET_TAX`、`STALE_SOURCE_TARGET_OR_OPTION`、`OPTIONAL_EXTRA_ALTERNATIVE_BRANCH`、`REPLACEMENT_PREVENTION_NO_EFFECT`、`GENERATED_RESOURCE_LIFETIME_REUSE`

每行必须绑定：

- action window
- failure dimension
- payment source kind
- prompt quote
- command rejection
- no-mutation assertion
- audit expectation
- doc anchors

## 3. Guards Added

新增 focused verifier 覆盖以下约束：

- `PaymentEngineRollbackFailureAllWindowMatrixCoversEveryRequiredSurfaceAndFamily`
- `PaymentEngineRollbackFailureAllWindowMatrixRequiresBoundPromptCommandNoMutationAuditAndDocFields`
- `PaymentEngineRollbackFailureAllWindowMatrixLinksBackTo03BEFamiliesAndSurfaceDocs`
- `PaymentEngineRollbackFailureAllWindowMatrixKeepsFailureDimensionsExecutable`
- `PaymentEngineRollbackFailureAllWindowMatrixDoesNotClaimP0005Closure`

这些 verifier 证明每个当前 payment surface 都与 7 个 rollback failure families 成笛卡尔矩阵，并保留 prompt quote / command rejection / no-mutation / audit / doc evidence contract。

## 4. Boundary Assertions

本批保持以下边界：

- `MOVE_UNIT` 不进入 PaymentEngine payment rollback matrix。
- `HIDE_CARD` 与 `LEGEND_ACT` 不被本批提升进 4D-03BL-B 指定 surfaces。
- 03BE representative family manifest 仍保留为 family-level anchor；03BL 只增加 all-window verifier。
- 新 verifier 不声明 full official rollback coverage。
- P0-005 remains open.
- 项目仍 **NOT READY**。

## 5. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 75/75 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 633/633 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4512/4512 passed
- `git diff --check`: passed

A review accepted the B-side diff because it stayed within the 4D-03BL-B test/docs write lock and did not touch runtime, frontend, card matrix JSON, fullOfficial / READY or `riftbound-dotnet.sln`.

## 6. Remaining Risk

4D-03BL-B 证明 rollback failure matrix 的 all-window audit contract 已可执行，但仍不是 full official PaymentEngine closure。它没有枚举每张官方卡、每种资源混合、每个 modifier / optional / replacement ordering、每个 generated-resource lifetime 组合，也没有更新 card matrix JSON、frontend final validation、P0/P1 closure 或 completion audit READY。

项目仍 **NOT READY**；P0-005 remains open.
