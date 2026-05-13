# Stage 4D-03E Payment Engine Hide Card Handoff

日期：2026-05-13
状态：**HANDOFF READY / PROJECT NOT READY**

本文是 Stage 4D-03E 的服务端实现交接。目标是在 4D-03 / 4D-03B / 4D-03C / 4D-03D 的 shared `PaymentPlan` foundation 之后，继续收窄 P0-005：把 `HIDE_CARD` 待命暗置支付窗口从手写法力扣费与 legacy `COST_PAID` payload 迁移到 shared quote / authorize / commit / audit 口径。

## 1. Owner And Write Lock

- Owner：B / Maxwell 服务端实现。
- A 主控职责：派单、验收、复跑测试、文档收口；不默认亲自写功能代码。
- 写入范围：
  - `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `src/Riftbound.Engine/MatchSession.cs`
  - `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`，仅当 prompt metadata / command shape 断言需要补强
  - `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`，仅当 Hub seed / prompt 需要补强
- 只有确需抽出 shared helper 时才修改 `src/Riftbound.Engine/PaymentCostRules.cs`。
- 不改前端 UI、不改卡牌矩阵、不碰未跟踪的 `riftbound-dotnet.sln`。

## 2. Scope

4D-03E 聚焦 `HIDE_CARD` standby hide command payment window，不引入新的 payment resource action。

必须覆盖：

- `HIDE_CARD optionalCosts=["STANDBY_A"]` 的标准 1 法力待命暗置路径使用 `PaymentCostRules.PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`，不再直接 `currentPool.Mana < cost` 后 `PayRuneCosts`。
- `HIDE_CARD optionalCosts=["STANDBY_TEEMO_MANA"]` 的 Teemo 替代待命费用保持 1 法力支付、Teemo 权限校验和 `teemoStandbyHideReplacement` audit metadata。
- `HIDE_CARD optionalCosts=["STANDBY_FREE"]` 的 Guerrilla Warfare 免费待命保持 0 费用、权限校验和 `standbyHideCostWaived` audit metadata。
- `COST_PAID` 保留兼容键：`mana`、`power`、`optionalCosts`、`standbyHideCostWaived`、`teemoStandbyHideReplacement`；同时新增/保持 plan-driven `paymentId`、`paymentWindow = HIDE_CARD`、total / remaining pool metadata。
- Bandle Tree `BATTLEFIELD:<objectId>` 额外待命目的地、`CARD_HIDDEN` hidden-info payload、object location reconciliation 与 Ember Monk 待命触发顺序保持兼容。
- prompt quote 与 command commit 口径一致：`HideCardSourceRequirements` / optional cost choices 应只暴露当前 shared plan 可授权的费用选项，不让 prompt 和 Core 各自重复判断出不同结果。

## 3. Implementation Notes

- `ResolveHideCard` 当前 open-codes `currentPool.Mana` 检查、`PayRuneCosts` 和 legacy `PaymentCostRules.BuildCostPaidPayload(paymentId, paymentWindow, ...)`。本切片应先构造 standby hide payment plan，再在任何 state mutation 前完成 authorize / commit。
- `STANDBY_FREE` 应表达为 0-cost plan；如果 shared helper 当前不方便表达 0-cost，也必须保持 no-op commit 与 plan audit payload 一致。
- 保持现有 command shape：`HideCardCommand.SourceObjectId`、`CardNo`、`Destination`、`OptionalCosts` 不新增字段。
- 保持现有错误语义和 no-mutation：费用不足、错误 optional cost、非法 destination、来源不在手牌、未知/不匹配 `cardNo`、非控制来源、无待命关键词、缺少授权均不得推进 tick 或移动对象。
- 若给 `HideCardMetadataFor` 新增 plan metadata，必须确保前端仍能只按已有 `sourceRequirements` / `optionalCostChoices` 工作。

## 4. No-Go

- 不关闭完整 P0-005。
- 不实现完整待命 / 伏击 / reaction lifecycle，不改 `REVEAL_CARD` 反应窗口。
- 不给 `HIDE_CARD` 增加 `RECYCLE_RUNE:*` payment resource action。
- 不改完整 `[A]` / `[C]` resource skills、`PAY_COST` pending trigger payment、`LEGEND_ACT` 或 battlefield held score。
- 不进入 4D-04 LayerEngine、关键词 full-pass 或卡牌矩阵升级。
- 不用前端本地推断弥补服务端 quote / commit 不一致。

## 5. Acceptance Gates

Focused filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HideCard|FullyQualifiedName~Standby|FullyQualifiedName~PaymentEngineUnificationTests"
```

Adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~HideCard|FullyQualifiedName~Standby|FullyQualifiedName~RevealCard|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Final gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
git diff --check
```

## 6. Baseline Evidence

实现前基线见 `docs/CURRENT_STAGE4D_03E_PAYMENT_ENGINE_HIDE_CARD_BASELINE_EVIDENCE.md`。
