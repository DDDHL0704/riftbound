# Stage 4D-03C Payment Engine Play Optional/Extra Handoff

日期：2026-05-13
状态：**HANDOFF READY / PROJECT NOT READY**

本文是 Stage 4D-03C 的服务端实现交接。目标是在 4D-03 / 4D-03B 的 shared `PaymentPlan` foundation 之后，继续收窄 P0-005：把 `PLAY_CARD` 的代表性 optional / extra / payment-resource 费用路径进一步纳入 shared quote / authorize / commit / audit 口径。

## 1. Owner And Write Lock

- Owner：B / Maxwell 服务端实现。
- A 主控职责：派单、验收、复跑测试、文档收口；不默认亲自写功能代码。
- 写入范围：
  - `src/Riftbound.Engine/PaymentCostRules.cs`
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`，仅当 prompt quote metadata 需要与 command commit 对齐时修改
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`，仅当 Hub seed 需要断言新 metadata 时修改
- 不改前端 UI、不改卡牌矩阵、不碰未跟踪的 `riftbound-dotnet.sln`。

## 2. Scope

4D-03C 聚焦 `PLAY_CARD` 代表路径，不试图一次关闭完整 PaymentEngine。

必须覆盖：

- `PLAY_CARD` base mana、cost reductions、additional mana、Spellshield tax、optional mana reductions、experience cost、generic / typed power cost 和 payment resource actions 进入同一个 `PaymentPlan` audit envelope。
- Haste `HASTE_READY`、Echo `ECHO`、Spellshield target tax、`SPEND_EXPERIENCE:*` 和 `RECYCLE_RUNE:*` payment resource action 的 prompt quote 与 command commit 继续保持一致。
- `COST_PAID` payload 保留旧字段，同时补全 `paymentId`、`paymentWindow`、`baseManaCost`、`totalManaCost`、`genericPower`、`powerByTrait`、`totalPowerCost`、`experienceCost`、`optionalCosts`、`paymentResourceActions`、source object 和 remaining pool / experience metadata。
- 支付失败、错误 trait、过量回收、非法 Echo、经验不足、Spellshield tax mana 不足都保持 no-mutation。

## 3. Implementation Notes

- 优先复用 `PaymentCostRules.PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` / `BuildCostPaidPayload`。
- 如果需要新增 helper，放在 `PaymentCostRules.cs` 或 `CoreRuleEngine` 局部私有 helper，避免引入大规模抽象。
- `RECYCLE_RUNE:*` 这类 payment resource action 已有事务回滚要求：先在临时工作状态中计算资源贡献，再用 shared plan commit 验证，只有全部接受后才落入最终状态。
- `MatchSession` prompt 只能展示服务端同一规则口径下可提交的 choices；不要让 prompt 重新发明一套费用判断。

## 4. No-Go

- 不关闭完整 P0-005。
- 不实现完整 `[A]` / `[C]` resource skills。
- 不扩展到所有 non-play payment windows。
- 不进入 4D-04 LayerEngine、关键词 full-pass 或卡牌矩阵升级。
- 不用前端本地推断弥补服务端 quote / commit 不一致。

## 5. Acceptance Gates

- Focused 4D-03C baseline filter 通过，并新增/强化 metadata 断言后仍通过。
- Adjacent Haste / Echo / Spellshield / Experience / PaymentResource / ActionPrompt / GameHub filter 通过。
- Backend full `dotnet test Riftbound.slnx --no-restore` 通过。
- `git diff --check` 无输出。
- A review 确认没有修改前端、卡牌矩阵或未跟踪 `riftbound-dotnet.sln`。

## 6. Baseline Evidence

实现前基线见 `docs/CURRENT_STAGE4D_03C_PAYMENT_ENGINE_PLAY_OPTIONAL_BASELINE_EVIDENCE.md`。
