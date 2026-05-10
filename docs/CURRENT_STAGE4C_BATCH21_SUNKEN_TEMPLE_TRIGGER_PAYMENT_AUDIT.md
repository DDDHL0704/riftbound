# 阶段 4C-21 Sunken Temple Trigger Payment 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对 Sunken Temple / 沉没神庙 `SFD·218/221` / `FU-05ce012700` 的规则证据与 P0/P1 审计口径。本批只把旧 immediate auto pay + draw 路径改为征服强力单位后的服务端权威 `TRIGGER_PAYMENT` / `PAY_COST` 窗口，不代表 full-official，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH21_SUNKEN_TEMPLE_TRIGGER_PAYMENT_EVIDENCE.md`。

## 本批范围

- 代表卡牌：Sunken Temple / 沉没神庙 `SFD·218/221` / `FU-05ce012700`。
- 官方文本入口：2026-04-27 card catalog 记录“当你征服此处时，如果此战场上留存至少一名强力单位，则你可以选择支付 1 来抽一张牌”。
- 规则路径：conquer with a powerful unit -> `TRIGGER_PAYMENT` prompt -> `PAY_COST(SPEND_MANA:1)` 或 `PAY_COST(DECLINE)` -> 支付成功后 `CARD_DRAWN` 1，拒付或非法提交不抽牌。
- 本批替代旧 immediate auto pay + draw：前端 / 测试不得把沉没神庙解释为自动支付、自动抽牌。
- B 实现改动文件记录：`src/Riftbound.Engine/CoreRuleEngine.cs`、`tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`、`tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`、`tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`。D 本批只更新文档。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-21 状态 | 仍缺 |
|---|---|---|---|
| Sunken Temple conquer trigger | `CATALOG` `SFD·218/221`；FU `FU-05ce012700`；`SOUL-OFAQ-260114` p15 | 已记录：征服此处且战场上留存强力单位时打开服务端权威 `TRIGGER_PAYMENT` / `PAY_COST` 窗口 | 完整 battlefield / conquer lifecycle、control freeze、battle cleanup 与 FAQ regression |
| Trigger payment / decline | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | 已记录：可支付 1 抽 1；可拒付并关闭窗口；非法 / stale / insufficient 等仍应维持 no-mutation 语义 | 完整 PaymentEngine、Quote / Authorize / Commit、替代费用、额外费用、更多非出牌支付窗口 |
| Powerful timing | `SOUL-OFAQ-260114` p15；`CATALOG` `SFD·218/221` | 已记录：本批只覆盖征服时已有 / 留存强力单位的代表路径 | effective power / LayerEngine、temporary modifier、征服后才变强力等完整时序矩阵 |
| 前端权威边界 | 服务端权威 prompt / snapshot 契约 | 前端只应展示服务端候选并提交 `PAY_COST`，不得本地判断是否强力、是否征服或是否抽牌 | 产品级支付窗口 UX、正式 18-step E2E |

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~P79BattlefieldConquerPowerfulUnitPaysOneToDraw|FullyQualifiedName~P79BattlefieldConquerPowerfulDrawSeedOffersBattlefieldDestinationAndDraws"` 通过 13/13。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3404/3404。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api` 通过，player-a / player-b 双上下文 OK；尾部 499/143、allocator 与一次 `OperationCanceledException` 记录来自脚本关停 / 本地连接取消噪声，不作为阻断。
- 正式 18-step E2E 未运行；不得用本批 focused / smoke 结果替代最终验收。

## 关闭项

- `SFD·218/221`《沉没神庙》旧 immediate auto pay + draw 已被 4C-21 口径 superseded。
- Sunken Temple 征服强力单位后的 `TRIGGER_PAYMENT` / `PAY_COST` 服务端权威窗口代表路径已关闭。
- `PAY_COST(SPEND_MANA:1)` 支付成功抽 1、`PAY_COST(DECLINE)` 拒付关闭窗口的代表性语义已记录。

## 仍存在 P0/P1

- P0：完整 PaymentEngine、triggered-cost 通用模型、Quote / Authorize / Commit、替代 / 额外费用、更多非出牌支付窗口仍未完成。
- P0：完整 battlefield / conquer lifecycle、战场控制冻结、battle cleanup 与 conquest scoring 全规则矩阵仍未完成。
- P0：完整 trigger engine、完整 effect resolution、FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 仍未完成。
- P1：Sunken Temple full-official timing matrix，包括 effective power / LayerEngine、temporary modifier、征服后变强力、战场上多单位同时离场等组合仍需补证据和测试。
