# Stage 4D-03F Payment Engine PAY_COST Resource Handoff

日期：2026-05-13
状态：**HANDOFF READY / PROJECT NOT READY**

本文是 Stage 4D-03F 的服务端实现交接。目标是在 4D-03E 之后继续收窄 P0-005：让 pending `PAY_COST` 窗口具备 `RECYCLE_RUNE:*` payment resource action 的 shared plan / commit / audit 基础，供后续 trigger payment power-cost 代表路径复用。

## 1. Owner And Write Lock

- Owner：B / Maxwell 服务端实现。
- A 主控职责：派单、验收、复跑测试、文档收口；不默认亲自写功能代码。
- 写入范围：
  - `src/Riftbound.Engine/MatchSession.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/PaymentCostRules.cs`，仅当 pending plan 需要暴露已有字段或 helper 小修时
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`，仅当不破坏现有 mana-only trigger payment 代表路径时补 regression
- 不改前端 UI、不改卡牌矩阵、不碰未跟踪的 `riftbound-dotnet.sln`。

## 2. Scope

4D-03F 聚焦 pending `PAY_COST` resource action foundation，不实现新的具体卡牌触发效果。

必须覆盖：

- `PendingPaymentState` 能记录并规范化 payment resource action ids，例如 `RECYCLE_RUNE:<objectId>`，并在 snapshot / prompt metadata 中保持可审计。
- 普通 pending `PAY_COST` command 可以提交合法的 `RECYCLE_RUNE:*` resource action 与 `SPEND_POWER:*` / typed `SPEND_POWER:<trait>:<amount>` payment choice。
- command commit 在扣费用前应用合法 `RECYCLE_RUNE:*` resource action，生成既有 `RUNE_RECYCLED` / `POWER_GAINED` events，再通过 `PaymentCostRules.PaymentPlan` / `TryCommitPayment` 扣除最终费用。
- `COST_PAID` payload 保留兼容键：`mana`、`power`、`powerByTrait`、`paymentChoiceIds`；同时包含 plan-driven `paymentResourceActions`、`legalPaymentChoiceIds`、`paymentId`、`paymentWindow`、remaining pool metadata。
- 无效、不属于当前玩家、非基础符文、已经没有必要的 resource action 必须 rejected 且 no-mutation。
- 现有 `TRIGGER_PAYMENT` mana-only 代表路径保持兼容：`SPEND_MANA:1` 与 `DECLINE` 语义、单选校验、支付/拒付/资源不足 no-mutation 不能变。

## 3. Implementation Notes

- 当前 `PendingPaymentState` 只包含 mana / power / typed power / legal choices / reason；`NormalizePendingPayment`、snapshot 和 `PayCostMetadataFor` 都不会单独暴露 resource action ids。
- 当前普通 pending `PAY_COST` path 会直接用 `PaymentCostRules.TryCommitPayment(paymentPlan, state.RunePools)`，没有在 pending window 内应用 `RECYCLE_RUNE:*`。
- 可以复用现有 helper：
  - `TryExtractRecycleRunePaymentResourceActions`
  - `AreRecycleRunePaymentResourceActionsRequired`
  - `ApplyRecycleRunePaymentResourceActions`
  - `BuildPendingPaymentPlan`
- 若 pending state 的 legal choices 继续同时包含 `SPEND_POWER:*` 和 `RECYCLE_RUNE:*`，实现需要把 payment resource actions 与 spend choices 清晰分组，避免把资源动作误当成要扣除的费用。
- 普通 pending `PAY_COST` 当前要求提交的 choice 集合与 legal choices 完全一致；如果实现改为区分 required spend choices 和 optional resource choices，必须新增 no-mutation regression，证明老的 minimal pending window 仍可用。

## 4. No-Go

- 不把现有 battlefield conquer / Vayne / Icevale / Jax `TRIGGER_PAYMENT` 改成 power-cost trigger。
- 不引入新的 concrete card runtime、trigger queue 模式选择或前端交互。
- 不关闭完整 P0-005。
- 不实现完整 `[A]` / `[C]` resource skills、Legend / battlefield held score resource action、Haste / Echo / Spellshield 全窗口资源动作。
- 不改完整 PAY_COST DTO 长期契约、卡牌矩阵、LayerEngine 或 READY 结论。

## 5. Acceptance Gates

Focused filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST"
```

Adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Final gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Baseline Evidence

实现前基线见 `docs/CURRENT_STAGE4D_03F_PAYMENT_ENGINE_PAY_COST_RESOURCE_BASELINE_EVIDENCE.md`。
