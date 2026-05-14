# Stage 4D-03AJ PaymentEngine Renata Typed Temporary Resource Audit

日期：2026-05-15

状态：**accepted / project NOT READY**

## Scope

本切片只收口 Renata Glasc 两个 typed-blue `ACTIVATE_ABILITY` payment branches 对 typed temporary payment resource 的 prompt / command / audit parity：

- `RENATA_GLASC_PAY_1_BLUE_DRAW_1`
- `RENATA_GLASC_PAY_4_BLUE4_EXHAUST_SCORE_1`

实现范围：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/RenataActivatedAbilityTests.cs`

## Runtime Changes

- Renata draw / score command branches 不再把合法 `TEMP_PAYMENT_RESOURCE:*` 直接判为非法。
- 两个 Renata branches 现在使用现有 inline temporary payment window：
  - 带入 typed `powerCostByTrait`。
  - 复用 `TryApplyTemporaryPaymentResourcesToPendingPayment`。
  - 提交后清理 `TemporaryPaymentResources` ledger。
  - 失败仍通过现有 payment helper 保持 no-mutation。
- `COST_PAID` audit payload 继续记录 `paymentResourceActions`，并补充：
  - `temporaryPaymentResourceIds`
  - `temporaryPaymentResourcePower`
  - `temporaryPaymentResourcePowerByTrait`
- 事件流在支付 temporary ledger 时包含 `TEMPORARY_PAYMENT_RESOURCE_SPENT` 与 `TEMPORARY_PAYMENT_RESOURCE_CLEARED`。

## Test Coverage

新增 / 更新 coverage：

- Renata draw prompt 可 quote matching blue typed temporary resource。
- Renata draw command 可消费 matching blue typed temporary resource。
- Renata score command 可在已有 3 蓝符能 + 1 蓝 typed temporary resource 时支付 4 蓝费用、横置并入栈。
- 既有 generic temporary resource 对 Renata typed-blue cost 仍不 quote / 不合法。

## No-Go

本切片不关闭 P0-005 full official：

- 不扩展完整 `[A]` / `[C]` resource skill family。
- 不新增 target-bearing activated ability family。
- 不处理 keyword payment branches、replacement / alternative / additional cost 全路径。
- 不修改 frontend、card matrix、formal E2E 或 READY 状态。

项目整体仍 **NOT READY**。
