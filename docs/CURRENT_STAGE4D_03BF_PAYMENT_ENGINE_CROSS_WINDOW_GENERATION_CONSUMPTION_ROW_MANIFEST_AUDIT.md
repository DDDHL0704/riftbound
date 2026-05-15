# Stage 4D-03BF PaymentEngine Cross-Window Generation Consumption Row Manifest Audit

审计日期：2026-05-16
结论：**FOCUSED VERIFIER ACCEPTED / PROJECT NOT READY**

## 1. Scope

本批在 4D-03BE rollback failure row manifest 后，继续拆解 4D-03BC 仍标记为 missing 的 `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING`。目标是把 generated / temporary / payment-only resource 的 creation、consumption、expiry、cleanup、restriction 和 audit-correlation 维度固定成可执行 manifest，避免后续 full official PaymentEngine matrix 只证明单一窗口而跳过跨窗口生命周期。

本批只修改 conformance test 与 docs，不修改 runtime、frontend、card matrix JSON，不升级 `fullOfficial=true`，不关闭 P0-005 或 READY。

## 2. Cross-Window Families

`CrossWindowGenerationConsumptionRowManifest` 固定 7 个 representative families：

| Family | Purpose |
| --- | --- |
| `RESOURCE_SKILL_GENERATION_WINDOWS` | resource skill generated resource creation windows |
| `INLINE_PAYMENT_CONSUMPTION_WINDOWS` | generated resource consumption in inline payment windows |
| `PENDING_PAYMENT_REUSE_AND_CLOSE` | pending PAY_COST / TRIGGER_PAYMENT quote, spend, decline and close |
| `TYPED_GENERIC_CONVERSION_AND_MATCHING` | typed, generic and conversion resource matching |
| `EXPIRY_CLEANUP_AND_TURN_BOUNDARY` | spend cleanup, payment close cleanup and stale expiry |
| `PAYMENT_ONLY_RESTRICTIONS_AND_WRONG_WINDOW` | payment-only restriction, wrong window and ordinary-pool misuse |
| `DUPLICATE_SPEND_AND_AUDIT_CORRELATION` | creation / spend / cleanup id correlation and duplicate-spend rejection |

每个 family 必须保留：

- `OfficialMatrixRowId = ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING`
- generation scope
- consumption scope
- representative surface
- prompt anchor
- command anchor
- audit anchor
- lifetime / restriction / no-mutation anchor
- remaining official breadth
- NOT READY / P0-005 remains open closure status
- resolving docs anchors

## 3. Guards Added

新增 focused verifier 覆盖以下约束：

- required cross-window families exactly once
- every row links back to `ROW_CROSS_WINDOW_GENERATION_CONSUMPTION_MISSING`
- prompt / command / audit / no-mutation / doc anchors are present
- lifecycle dimensions remain explicit: generation, creation, consumption, payment-only, restriction, expiry, cleanup, typed, generic, pending, duplicate, cross-window, ledger
- no row claims `FullOfficialRulePass`, `fullOfficial=true`, P0-005 closure or READY

## 4. Validation

通过的验证：

- Focused PaymentEngine coverage guard: 56/56 passed
- Adjacent PaymentEngine / resource skill / prompt / hub regression: 614/614 passed
- Backend full `dotnet test Riftbound.slnx --no-restore`: 4493/4493 passed

## 5. Remaining Risk

4D-03BF proves generated-resource cross-window row routing is visible and executable as a representative manifest. It does not prove full official generation / consumption coverage, all generated resource pairings, every official card payment branch, card matrix alignment, frontend final validation or final completion audit.

项目仍 **NOT READY**；P0-005 remains open.
