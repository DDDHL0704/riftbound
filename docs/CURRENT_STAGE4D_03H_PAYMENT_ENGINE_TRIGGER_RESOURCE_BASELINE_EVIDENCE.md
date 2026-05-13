# Stage 4D-03H PaymentEngine Trigger Resource Baseline Evidence

日期：2026-05-13
结论：**BASELINE GREEN / PROJECT NOT READY**

本文记录 4D-03H 实现前的 A 侧基线。当前仓库尚未实现 SFD 菲奥娜 `TRIGGER_PAYMENT` 黄色资源支付；本基线只证明既有 trigger / PAY_COST / prompt / GameHub 路径在实现前绿色，可作为后续回归护栏。

## 1. Worktree

实现前 `git status --short`：

```text
?? riftbound-dotnet.sln
```

该文件为既有未跟踪文件，4D-03H handoff / baseline 不读取、不修改、不暂存。

## 2. 官方候选确认

候选：`SFD·180/221` 与 `SFD·180a/221` 菲奥娜 / 无上统御。

官方卡面文本来自 `data/official/card-catalog.zh-CN.json`：

- 当控制者的一名单位变为强力时，可以支付黄色符能，让该单位变为活跃状态。
- 基础费用 3，战力 3，颜色 yellow。

现有 fixtures：

- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-180-fiora-powerful-ready-static.fixture.json`
- `tests/Riftbound.ConformanceTests/Fixtures/p2-preflight-play-sfd-180a-fiora-powerful-ready-static.fixture.json`

两份 fixture 当前只覆盖普通手牌打出后成为 3-power `CARD_TYPE:UNIT`，并明确记录“强力状态变化、可选黄色支付和活跃状态变化路径暂缓”。

## 3. 当前代码边界

- `ResolvePendingPayCost` 已支持普通 pending `PAY_COST` 的 `RECYCLE_RUNE:*` resource action。
- `ResolveTriggerPayCost` 当前仍要求 exactly one choice，并只接受 `DECLINE` 或 `SPEND_MANA:1`。
- 当前 `TriggerPaymentTests` 覆盖的 Treasure Pile、Sunken Temple、Vayne、Icevale Archer、Jax 均为 mana-only triggered payment；这批测试是 4D-03H 的兼容护栏。
- `ApplyBoon` 与 `ApplyPowerModifier` 能改变对象战力；`OGN·232/298` 菲奥娜已有“战力达到 5 后授予关键词”的代表路径，但这不是 SFD 菲奥娜的支付触发实现。

## 4. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~PAY_COST"
```

结果：

```text
Passed!  - Failed:     0, Passed:    55, Skipped:     0, Total:    55
```

覆盖含义：

- 既有 mana-only trigger payment 代表路径绿色。
- 4D-03F ordinary pending `PAY_COST` resource action 代表路径绿色。
- 该命令不证明 SFD 菲奥娜黄色 trigger payment 已实现。

## 5. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPayment|FullyQualifiedName~PAY_COST|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed!  - Failed:     0, Passed:   233, Skipped:     0, Total:   233
```

覆盖含义：

- trigger payment、PAY_COST、PaymentEngine foundation、ActionPrompt 与 GameHub 相邻路径在实现前绿色。
- 该命令是后续 4D-03H 实现后必须保持通过的回归护栏。

## 6. A 侧结论

4D-03H 可以进入 B 侧实现交接。实现前风险集中在 `TRIGGER_PAYMENT` 当前 mana-only 特化，以及“变为强力”的 before/after 触发上下文是否足够可靠。若 B 无法可靠建立该上下文，应按 handoff 的 no-go criteria 返回 design gate，而不是用当前已经强力的静态状态代替触发。
