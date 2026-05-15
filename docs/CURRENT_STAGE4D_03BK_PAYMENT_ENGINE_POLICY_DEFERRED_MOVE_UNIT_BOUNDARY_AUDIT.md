# Stage 4D-03BK PaymentEngine Policy-Deferred MOVE_UNIT Boundary Audit

审计日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批在 4D-03BH 覆盖 `missing-official-row`、4D-03BJ 覆盖 `representative-seed` 后，补齐第三类 row status 的聚合 verifier：确保 4D-03BC 的唯一 `policy-deferred-row` 是 `ROW_MOVE_UNIT_POLICY_DEFERRED`，并保持 MOVE_UNIT 当前仍是 movement-permission / optional-cost policy boundary，而不是 PaymentEngine payment row。

本批只修改 conformance test 与 docs，不修改 runtime、frontend、card matrix JSON，不升级 `fullOfficial=true`，不关闭 P0-005 或 READY。

## 2. Coverage Contract

`PaymentEngineOfficialMatrixSeedRowManifest` 当前包含：

- 9 个 `representative-seed`
- 3 个 `missing-official-row`
- 1 个 `policy-deferred-row`

本批新增的聚合 verifier 要求：

- `policy-deferred-row` 精确等于 `ROW_MOVE_UNIT_POLICY_DEFERRED`。
- 该 row 必须保留 `ACTION_WINDOWS` axis 与 `MOVE_UNIT` action window。
- 该 row 必须明确 prompt 是 movement permission metadata，不是 PaymentEngine payment prompt。
- command anchor 必须说明不会打开 payment command row，除非未来官方规则给 MOVE_UNIT 增加资源费用。
- audit anchor 必须说明当前是 movement / permission audit，不是 `COST_PAID` audit。
- policy row 不得出现在 representative seed、missing official row、rollback failure、cross-window generation / consumption、card matrix alignment downstream manifests 中。
- `CoverageManifest` 里的 MOVE_UNIT 必须保持 `policy-non-resource`；`ResidualBlockerManifest` 里的 MOVE_UNIT policy family 必须保持 `policy-deferred`。
- 聚合文本仍保留 NOT READY / P0-005 remains open closure，并不得出现 `FullOfficialRulePass` 或 `fullOfficial=true`。

## 3. Guards Added

新增 focused verifier 覆盖以下约束：

- `PaymentEngineOfficialMatrixPolicyDeferredRowsStaySingleMoveUnitBoundary`
- `PaymentEngineOfficialMatrixPolicyDeferredMoveUnitStaysOutOfPaymentManifests`
- `PaymentEngineOfficialMatrixPolicyDeferredMoveUnitDoesNotClaimPaymentCoverageOrClosure`

## 4. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 70/70 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 628/628 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4507/4507 passed

## 5. Remaining Risk

4D-03BK 只证明 MOVE_UNIT 当前 policy-deferred row boundary 完整、可回归，且不会被误算为 PaymentEngine payment coverage。它不生成 full official matrix，不修改 MOVE_UNIT runtime，不修改 card matrix JSON，也不证明未来官方规则不会要求 movement payment reclassification。

项目仍 **NOT READY**；P0-005 remains open.
