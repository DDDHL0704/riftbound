# Stage 4D-03H PaymentEngine Trigger Resource Handoff

日期：2026-05-13
结论：**HANDOFF READY / PROJECT NOT READY**

本文定义 4D-03H 的 B 侧实现交接范围。A 主控只记录官方候选、当前代码边界、写入范围和验收门槛；本文件不代表实现已完成，不关闭 P0-005，不改变项目 **NOT READY** 结论。

## 1. 目标

把一个具体、官方、非 mana 的 `TRIGGER_PAYMENT` 代表窗口接入 shared PaymentEngine resource-action 口径。

首选候选为 SFD 菲奥娜：

- `SFD·180/221` / `SFD·180a/221`，卡名 `菲奥娜`，副标题 `无上统御`。
- 官方文本：当控制者的一名单位变为强力时，可以支付黄色符能，让该单位变为活跃状态。
- 现有 preflight fixture `p2-preflight-play-sfd-180-fiora-powerful-ready-static.fixture.json` 与 `p2-preflight-play-sfd-180a-fiora-powerful-ready-static.fixture.json` 已明确把“强力状态变化、可选黄色支付和活跃路径”标为暂缓。

本切片目标不是补完所有 trigger engine，也不是补完整 LayerEngine。目标是先让一个可靠的“变为强力 -> 可选黄色支付 -> 活跃”路径，使用和 4D-03F / 4D-03G 相同的 `PaymentPlan` / `TryCommitPayment` / `RECYCLE_RUNE:*` 资源动作语义。

## 2. 当前代码事实

- `CoreRuleEngine.ResolvePendingPayCost` 已支持普通 pending `PAY_COST` 的 `RECYCLE_RUNE:*` payment resource action：先在事务副本中回收符文，再通过 `BuildPendingPaymentPlan` 和 `PaymentCostRules.TryCommitPayment` 扣费。
- `CoreRuleEngine.ResolveTriggerPayCost` 当前仍是触发支付特化路径：只允许一个 choice，且非 `DECLINE` 时必须是 `SPEND_MANA:1`。它不会接受 `SPEND_POWER:yellow:1`，也不会处理 `RECYCLE_RUNE:*`。
- 现有 `TRIGGER_PAYMENT` 代表路径 Treasure Pile / Sunken Temple / Vayne / Icevale Archer / Jax 均为支付 1 mana，不应被本切片改坏。
- `ApplyBoon` / `ApplyPowerModifier` 已存在能让单位战力跨过强力阈值的局部路径；`OGN·232/298` 菲奥娜关键词路径说明当前代码已经有“战力达到 5”代表判断，但尚无通用“before < 5 / after >= 5”触发支付框架。

## 3. 建议写入范围

建议 owner：B / Maxwell。

允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`
- `tests/Riftbound.ConformanceTests/PaymentEngineUnificationTests.cs`
- 必要时补充 `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

不建议本切片写入：

- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`
- 前端运行时代码
- `PaymentCostRules.cs`，除非实现中证明现有 plan / commit 接口确实缺少必要表达
- 未跟踪文件 `riftbound-dotnet.sln`

## 4. 实现要求

1. 触发创建

- 当可验证的服务端效果让一个由同一玩家控制的单位从非强力变为强力时，若该玩家控制公开、正面、可用的 `SFD·180/221` 或 `SFD·180a/221` 菲奥娜，服务端打开 `TRIGGER_PAYMENT` / `PAY_COST` 窗口。
- pending payment 应包含 `powerCostByTrait["yellow"] = 1`、`LegalPaymentChoiceIds` 至少包含 `SPEND_POWER:yellow:1` 与 `DECLINE`。
- 若当前黄色符能不足但基地有合法可回收黄色基础符文，pending payment / prompt 应暴露对应 `RECYCLE_RUNE:<objectId>` payment resource action。
- reason/context 必须能在 `PAY_COST` 时重新校验 source Fiora、目标单位、控制者、对象位置和目标仍为强力，避免 stale window 直接结算。

2. 支付解析

- `ResolveTriggerPayCost` 不应继续把所有非 decline 触发支付硬编码为 `SPEND_MANA:1`。
- 对带 power cost 的 trigger payment，允许提交 spend choice 加必要 `RECYCLE_RUNE:*` resource action，并复用 `BuildPendingPaymentPlan`、`ApplyRecycleRunePaymentResourceActions`、`PaymentCostRules.TryCommitPayment`。
- invalid / duplicate / stale / wrong-owner / unnecessary recycle rune resource action 必须 rejected 且 no mutation。
- decline 必须继续关闭窗口且不支付、不 ready。

3. 效果结算

- 支付成功后，将成为强力的目标单位变为活跃状态，并发出可审计事件，例如 `TRIGGER_RESOLVED` 与 `UNIT_READIED`。
- `COST_PAID` payload 应保留现有兼容字段，并包含 `paymentId`、`paymentWindow`、`reason`、`totalPowerCost`、`powerByTrait`、`paymentChoiceIds`、`legalPaymentChoiceIds`、`paymentResourceActions`、`recycledRuneObjectIds` 与 remaining pool metadata。
- 既有 mana-only trigger payment 的事件 payload 和测试语义不能回退。

4. Prompt / snapshot

- `MatchSession` pending payment snapshot / action prompt 应复用 4D-03F 的 resource metadata 口径，向当前支付玩家暴露 resource action choice、per-choice contribution 与 available-power-with-resources。
- 对手 snapshot 不得泄漏隐藏牌信息，只能看到允许公开的 payment window 事实。

## 5. 必补测试

Focused tests 建议覆盖：

- SFD·180 菲奥娜在场，友方 exhausted 4-power 单位因 `Arena Rookie` / boon 路径成为 5-power，打开 `TRIGGER_PAYMENT`，费用为黄色 1，允许 decline。
- 支付已有黄色符能后，目标单位 ready，窗口关闭，`COST_PAID` payload 使用 `TRIGGER_PAYMENT` / yellow power cost。
- 当前黄色符能不足但基地有黄色基础符文时，prompt 暴露 `RECYCLE_RUNE:*`；提交 `RECYCLE_RUNE:<id>` + `SPEND_POWER:yellow:1` 后符文被回收、目标 ready。
- decline 路径不支付、不回收、不 ready。
- invalid / duplicate / wrong-owner / missing-cardNo / unnecessary recycle rune resource action rejected 且 tick、runePool、zones、cardObjects、pending payment 不变。
- source Fiora 消失、目标控制者变化、目标离场、目标不再强力时 stale payment rejected。
- 既有 `TriggerPaymentTests` 的 Treasure Pile / Sunken Temple / Vayne / Icevale Archer / Jax mana-only paths 继续通过。

## 6. 验收命令

实现后至少运行：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Fiora|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PAY_COST"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
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

- 如果无法可靠判断“before < 5 且 after >= 5”，不要用“当前已经强力”近似触发。
- 如果只有 OGN 菲奥娜关键词特化可用，而没有能绑定 SFD 菲奥娜源和目标的触发上下文，应先提交 NO-GO / design gate，不要硬凑事件。
- 不要把所有 `TRIGGER_PAYMENT` 改成只服务 Fiora 的分支；mana-only 触发支付必须保持兼容。
- 不要在本切片实现完整 LayerEngine、全 triggered payment framework、全 `[A]` / `[C]` resource skills 或卡牌矩阵升级。
- 不要修改前端让 UI 自行推断支付资源或强力触发；前端只能展示服务端 prompt。

## 8. A 侧结论

4D-03H 是 P0-005 full PaymentEngine breadth 的下一枚窄切片。它只以 SFD 菲奥娜的黄色触发支付代表路径验证 trigger payment resource action，不关闭完整 PaymentEngine、完整 trigger engine、LayerEngine、1009/811 full-official 证据或最终 READY。
