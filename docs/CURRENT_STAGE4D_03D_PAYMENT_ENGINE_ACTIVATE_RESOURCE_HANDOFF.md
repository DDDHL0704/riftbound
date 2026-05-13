# Stage 4D-03D Payment Engine Activate Resource Handoff

日期：2026-05-13
状态：**HANDOFF READY / PROJECT NOT READY**

本文是 Stage 4D-03D 的服务端实现交接。目标是在 4D-03 / 4D-03B / 4D-03C 的 shared `PaymentPlan` foundation 之后，继续收窄 P0-005：把代表性 `ACTIVATE_ABILITY` 支付窗口接入支付步骤中的 `RECYCLE_RUNE:*` 资源动作 quote / authorize / commit / audit 口径。

## 1. Owner And Write Lock

- Owner：B / Maxwell 服务端实现。
- A 主控职责：派单、验收、复跑测试、文档收口；不默认亲自写功能代码。
- 写入范围：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`，仅当 prompt metadata 断言需要补强
  - `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`，仅当 Hub seed / prompt 需要补强
- 只有确需抽出 shared helper 时才修改 `src/Riftbound.Engine/PaymentCostRules.cs`。
- 不改前端 UI、不改卡牌矩阵、不碰未跟踪的 `riftbound-dotnet.sln`。

## 2. Scope

4D-03D 聚焦代表性 `ACTIVATE_ABILITY` non-play window，不试图一次关闭完整 `[A]` / `[C]` resource skill model。

必须覆盖：

- Vi `ACTIVATE_ABILITY` 在法力已足、符能不足但己方基地有可回收基础符文时，prompt 公开 `RECYCLE_RUNE:<objectId>` payment resource choice，command 可在同一 `ACTIVATE_ABILITY` optional costs 中回收符文并支付 1 generic power。
- Xerath `ACTIVATE_ABILITY` 在符能不足但有可回收基础符文时，prompt / command 同样支持 payment resource action；若目标带 Spellshield，加税 mana 仍必须由当前法力支付。
- `COST_PAID` 保留 4D-03B 的 shared plan metadata，并新增/保持 `paymentResourceActions`、`recycledRuneObjectIds`、remaining pool metadata。
- `RUNE_RECYCLED` / `POWER_GAINED` 事件必须带 `paymentWindow = ACTIVATE_ABILITY` 与同一个 `paymentId`，事件顺序应保持 resource events -> `ABILITY_ACTIVATED` / `COST_PAID` / stack or immediate resolution。
- 错误来源、重复回收、过量回收、非己方/面朝下/无 `cardNo` 符文、资源动作不必要、Spellshield tax mana 不足均 no-mutation。

## 3. Implementation Notes

- 优先复用现有 `TryExtractRecycleRunePaymentResourceActions`、`AreRecycleRunePaymentResourceActionsRequired`、`ApplyRecycleRunePaymentResourceActions` 和 `PaymentCostRules.PaymentPlan`。
- `MatchSession.ActivateAbilitySourceRequirements` 当前只按 `RunePool.TotalPower` 过滤，4D-03D 需要让它像 `PLAY_CARD` / `ASSEMBLE_EQUIPMENT` 一样暴露 payment resource choices 与 per-choice contribution metadata。
- `ActivateAbilityCommand.OptionalCosts` 已存在；本切片应只接受服务端 prompt 可给出的 `RECYCLE_RUNE:*` 资源动作，不引入新 command shape。
- 只把 Vi / Xerath 代表路径纳入本切片；不要扩展到所有技能、所有触发支付窗口或完整 reaction timing。

## 4. No-Go

- 不关闭完整 P0-005。
- 不实现完整 `[A]` / `[C]` resource skills。
- 不改 `LEGEND_ACT`、`PAY_COST` pending trigger payment、battlefield held score 的资源动作，除非只是为共享 helper 保持编译。
- 不进入 4D-04 LayerEngine、关键词 full-pass 或卡牌矩阵升级。
- 不用前端本地推断弥补服务端 quote / commit 不一致。

## 5. Acceptance Gates

Focused filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActivateAbility|FullyQualifiedName~P7PlayCardRecyclesRuneAsPaymentResourceAction|FullyQualifiedName~P7TypedPowerPaymentAssemblesLongSwordWithRecycleRunePaymentResource|FullyQualifiedName~CoreRuleEngineRejectsTapRuneSourceWithoutCardNo|FullyQualifiedName~CoreRuleEngineTapsBasicRuneAndReconcilesObjectLocation|FullyQualifiedName~CoreRuleEnginePromptsAndTapsLegacyOwnedBasicRune|FullyQualifiedName~CoreRuleEngineRejectsRecycleRuneSourceWithoutCardNo|FullyQualifiedName~CoreRuleEngineRecyclesBasicRuneForMatchingTraitPower|FullyQualifiedName~CoreRuleEngineRecyclesLegacyOwnedBasicRune|FullyQualifiedName~CoreRuleEngineRecyclesBasicRuneAndReconcilesObjectLocations"
```

Adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentResource|FullyQualifiedName~RecycleRune|FullyQualifiedName~TapRune|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Final gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Baseline Evidence

实现前基线见 `docs/CURRENT_STAGE4D_03D_PAYMENT_ENGINE_ACTIVATE_RESOURCE_BASELINE_EVIDENCE.md`。
