# 阶段 4C-21 Sunken Temple Trigger Payment 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 `SFD·218/221` Sunken Temple / 《沉没神庙》、`FU-05ce012700` 的代表性触发支付证据。4C-21 不宣称 full-official，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-21 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`SFD·218/221` | 征服此处且战场上留存至少一名强力单位时，可以支付 1 抽 1。 |
| 强力 / 征服时机 | `SOUL-OFAQ-260114` p15 | 作为 Sunken Temple powerful / conquest timing 的 FAQ 入口；完整时序矩阵仍未关闭。 |
| 支付 / 拒付 | `CORE-260330` p52-p55 rules 377, 403-405；`JFAQ-251023` p2-p4 q2.5 | 本批用 `TRIGGER_PAYMENT` / `PAY_COST` 表达可支付或拒付，不再自动扣费抽牌。 |

## 实现证据

- `src/Riftbound.Engine/CoreRuleEngine.cs`：征服 Sunken Temple 且 source unit 仍为 powerful 时打开 `PendingPaymentState`，reason 编码 battlefield / source context；`PAY_COST(SPEND_MANA:1)` 支付后抽 1；`PAY_COST(DECLINE)` 只关闭窗口。
- `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`：覆盖开窗、支付成功、拒付、wrong player / stale / unknown / duplicate / malformed / insufficient no-mutation、非 powerful 不开窗。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`：旧 immediate auto pay + draw 断言改为先开窗再支付。
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs`：开发 seed 通过 Hub 暴露 `PAY_COST` prompt，客户端提交服务端 prompt stamp 后才抽牌。

## 验证

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~P79BattlefieldConquerPowerfulUnitPaysOneToDraw|FullyQualifiedName~P79BattlefieldConquerPowerfulDrawSeedOffersBattlefieldDestinationAndDraws"` 通过 13/13。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3404/3404。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && node scripts/stage3-preflight.mjs --start-api` 通过。

## 仍未关闭

- 完整 PaymentEngine、Quote / Authorize / Commit、替代 / 额外费用、更多触发支付窗口。
- Sunken Temple full-official timing matrix：effective power / LayerEngine、temporary modifiers、征服后才变强力、多单位同时离场等。
- 完整 battlefield / conquer / battle cleanup / scoring matrix。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
