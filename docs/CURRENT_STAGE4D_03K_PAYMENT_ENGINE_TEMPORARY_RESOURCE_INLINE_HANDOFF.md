# Stage 4D-03K PaymentEngine Temporary Resource Inline Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03K 的 B 侧服务端实现交接范围。A 主控只记录当前代码事实、实现边界、写入范围、测试过滤器和 no-go；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

4D-03J 已把 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill 的 spell-duel focus timing、no ordinary stack item representative 与 temporary payment-only ledger 接入服务端。当前 temporary ledger 只在 pending `PAY_COST` 中可被 `TEMP_PAYMENT_RESOURCE:*` 消费。

4D-03K 的目标是把这类 temporary payment-only resource 扩展到 inline payment commit 路径，使服务端 prompt quote、command guard、payment plan / commit 与 audit 在以下 representative 窗口中一致：

- `PLAY_CARD`
- `ACTIVATE_ABILITY`
- `ASSEMBLE_EQUIPMENT`

本切片仍只处理 Malzahar 产生的 temporary payment-only generic rune resource，不扩展到完整 `[A]` / `[C]` resource skill family，不修改前端运行时，不更新卡牌覆盖矩阵。

## 2. 当前代码事实

- `PaymentCostRules` 已有 `TemporaryPaymentResourceActionPrefix`、`TemporaryPaymentResourceActionId`、`TryParseTemporaryPaymentResourceActionId` 与 `RuneCostPaymentKind`。
- `MatchState.TemporaryPaymentResources` 与 snapshot view 已存在，并保留 owner、source、ability、paymentWindow、generatedPower、remainingPower、allowedPaymentKinds、paymentOnly 与 restriction metadata。
- `MatchSession` 的 pending payment prompt 已通过 `TemporaryPaymentResourceActionIds(state, payment)` 暴露 `TEMP_PAYMENT_RESOURCE:*`，并在 `PendingPaymentResourcePowerByChoice` 中给出 per-choice power。
- `CoreRuleEngine.TryApplyTemporaryPaymentResourcesToPendingPayment` 当前只服务 pending `PAY_COST`，并明确限制 temporary resource 只能补通用符能，不支付 mana、experience 或 typed shortfall。
- `PLAY_CARD`、`ACTIVATE_ABILITY` 与 `ASSEMBLE_EQUIPMENT` inline 路径仍通过 `TryExtractRecycleRunePaymentResourceActions` 只识别 `RECYCLE_RUNE:*` payment resource action。
- `MatchSession` 的 `PlayCardPaymentResourceChoicesForBehavior`、`ActivateAbilityPaymentResourceChoices`、`AssembleEquipmentPaymentResourceChoices` 当前只从基地可回收符文生成 `RECYCLE_RUNE:*` 候选；不会把 Malzahar temporary ledger 加入 inline `sourceRequirements` / `paymentResourceChoices`。
- 4D-03J accepted state 保证 pending `PAY_COST` 的 temporary resource representative 绿色，但仍显式保留 “inline payment-window temporary resource consumption” 残余。

## 3. 建议写入范围

建议 owner：B 服务端规则 / 协议 / 测试实现。

允许写入：

- `src/Riftbound.Engine/PaymentCostRules.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 必要时新增窄域 temporary resource inline payment tests

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- 与 Malzahar temporary resource inline payment 无关的 card behavior registry / fixture mass update
- 未跟踪文件 `riftbound-dotnet.sln`

不可并行文件：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/PaymentCostRules.cs`
- `tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`

## 4. 实现要求

### 4.1 Prompt Quote

- `PLAY_CARD`、`ACTIVATE_ABILITY`、`ASSEMBLE_EQUIPMENT` 的 `sourceRequirements` / payment metadata 必须在当前玩家拥有可用 temporary payment resource 且该资源可合法补足 generic rune power shortfall 时暴露 `TEMP_PAYMENT_RESOURCE:<resourceId>`。
- per-choice metadata 至少应表达 power contribution、payment-only restriction 与来源 resource id；不要让前端根据 snapshot 自行推断可提交 action。
- 不得在 mana-only、experience-only、typed-power-only shortfall 或已可直接支付的窗口暴露 temporary resource action。
- temporary resource 与 `RECYCLE_RUNE:*` 可以同时出现在候选中，但必须由服务端 quote 精确说明每个 choice 的贡献；前端仍只展示/提交服务端候选。

### 4.2 Command Guard

- `TryExtractRecycleRunePaymentResourceActions` 或其后继 helper 应扩展为可同时解析 `RECYCLE_RUNE:*` 与 `TEMP_PAYMENT_RESOURCE:*`，并把 behavior optional costs 与 payment resource action 分离。
- 对 inline windows，temporary resource 只能用于 generic rune power cost；不得支付 mana、experience、typed shortfall、unrelated score cost 或非 rune-cost window。
- 需要拒绝并保持 no-mutation：
  - stale / unknown temporary resource id。
  - 非当前玩家 owner 的 resource。
  - remainingPower 为 0 或缺少 `RUNE_COST` allowed kind。
  - 重复提交同一个 temporary resource action。
  - 当前 rune pool 已足够支付却提交 temporary resource。
  - typed power 不足时试图用 temporary resource 补 typed shortfall。
  - selected temporary resource 总 power 不足以补 generic shortfall。

### 4.3 Payment Commit / Lifecycle

- inline payment commit 应先在本地副本中应用合法 `RECYCLE_RUNE:*` 与 temporary resource contribution，再通过 shared `PaymentPlan` / authorize / commit 扣费。
- temporary resource consumption 不得永久泄漏为 unrestricted `RunePool.Power`；若短暂调整 rune pool 以复用 `TryCommitPayment`，必须在同一 transaction 内扣除并更新 / 清理 ledger。
- 成功消费后事件应记录 `TEMPORARY_PAYMENT_RESOURCE_SPENT` 或同等 audit payload，包含 paymentId、paymentWindow、resourceId、consumedPower、remainingPowerBeforeCleanup。
- 若 resource 未完全消费，B 可选择保留 remainingPower 或按当前 representative 直接清理；但必须在 handoff 实现审计中明确生命周期口径并由测试固定。
- end-turn / turn-start resource reset 清理必须继续覆盖未使用 temporary resource。

### 4.4 Representative Windows

- `PLAY_CARD`：覆盖至少一个 generic power shortfall 的 play-card representative，证明 prompt 暴露 temporary choice、命令可提交、`COST_PAID` 包含 paymentResourceActions，且 hand / stack / rune pool / temporary ledger 正确变化。
- `ACTIVATE_ABILITY`：覆盖 Vi 或同类 generic power `ACTIVATE_ABILITY` representative，不破坏 Xerath Spellshield tax mana guard，不允许 temporary resource 支付 mana tax。
- `ASSEMBLE_EQUIPMENT`：覆盖 Long Sword / assemble representative 的 generic power shortfall；保留 existing additional cost / target guard。
- 本切片不要求 `HIDE_CARD`、`LEGEND_ACT`、battlefield held score 或 trigger payment 同步迁移，除非 B 发现共享 helper 会自然覆盖且能补测试。

## 5. 必补测试

Focused tests 至少覆盖：

- Malzahar 创建 temporary payment-only resource 后，`PLAY_CARD` generic power shortfall prompt 暴露 `TEMP_PAYMENT_RESOURCE:*`。
- `PLAY_CARD` inline command 使用 temporary resource 成功支付并清理 / 更新 ledger，且不生成 unrestricted lingering power。
- `ACTIVATE_ABILITY` generic power shortfall 可使用 temporary resource；mana tax / typed shortfall 不可使用。
- `ASSEMBLE_EQUIPMENT` generic power shortfall 可使用 temporary resource。
- stale / wrong-owner / duplicate / unnecessary / insufficient temporary resource action 均 rejected no-mutation。
- pending `PAY_COST` 既有 temporary resource 消费测试仍通过。
- `RECYCLE_RUNE:*` 与 `TEMP_PAYMENT_RESOURCE:*` 混合候选的 prompt metadata 与 command audit 不冲突。

相邻回归必须覆盖：

- Malzahar resource skill lifecycle。
- `PaymentEngineUnificationTests`。
- `PLAY_CARD` optional / extra / payment resource actions。
- Vi / Xerath `ACTIVATE_ABILITY` payment resource actions。
- Long Sword / assemble equipment payment resource actions。
- `HIDE_CARD`、`LEGEND_ACT` 与 GameHub / ActionPrompt seed prompt surfaces。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~TemporaryPaymentResource|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PaymentResource|FullyQualifiedName~PlayCard|FullyQualifiedName~ActivateAbility|FullyQualifiedName~AssembleEquipment"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PlayCard|FullyQualifiedName~ActivateAbility|FullyQualifiedName~AssembleEquipment|FullyQualifiedName~HideCard|FullyQualifiedName~LegendAct|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

实现通过后由 A 决定是否再跑 backend full：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

提交前必须运行：

```sh
git diff --check
```

## 7. No-Go Criteria

- 不要把 temporary payment resource 写成普通永久 `RunePool.Power` 后宣称完成。
- 不要让前端根据 `temporaryPaymentResources` 自行推断 inline 可提交 action；必须由 server prompt quote 驱动。
- 不要允许 temporary resource 支付 mana、experience、typed shortfall、score cost 或非 rune-cost window。
- 不要修改 coverage matrix 或升级 `FU-0f7cbe26ce` 到 full-official。
- 不要把本切片扩展成完整 `[A]` / `[C]` resource skill family。
- 不要改前端运行时代码补服务端候选缺口。
- 不要因为 4D-03K 通过而关闭 P0-005、P1 LayerEngine、1009/811 full-official 或 active goal。

## 8. A 侧结论

4D-03K 是 4D-03J 的后续 PaymentEngine breadth 切片，只收窄 temporary payment-only resource 从 pending `PAY_COST` 走向 inline payment commit 的代表路径。它可以推进 P0-005，但不能替代 full official PaymentEngine、完整 resource skill family、reaction/counter full target-filter model、LayerEngine 或最终 READY audit。
