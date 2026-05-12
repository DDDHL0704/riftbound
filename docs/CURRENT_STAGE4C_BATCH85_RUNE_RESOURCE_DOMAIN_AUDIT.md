# Stage 4C-85 Rune Resource Domain Audit

审计日期：2026-05-13
结论：**代表性资源域证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-0ec69ae7e6`、`FU-39041f4562`
- 代表卡：炽烈符文 / Red basic rune `OGN·007/298` / cardId `31211`
- 代表卡：翠意符文 / Blue basic rune `OGN·042/298` / cardId `31252`
- 共享条目：上述两个 FU 覆盖 OGN / SFD / UNL 中的 16 个基础红/蓝符文 snapshot entries。
- 代表 effect：`RUNE_RESOURCE_DOMAIN`
- 本批是 evidence-only overlay，不修改功能代码；覆盖符文卡通过 non-play resource domain 被登记为 implemented representative rule pass、符文不进入 `PLAY_CARD` direct registry、可控基地符文通过服务端 `RECYCLE_RUNE` / `paymentResourcePowerByChoice` 作为支付资源贡献 1 点对应 trait 符能。
- 本批同步记录 typed `SPEND_POWER:red:2` 下 red 支付资源可补足、blue 支付资源因 trait 不匹配被拒绝，generic `SPEND_POWER:2` 下 red / blue 任一服务端候选可补足但不得过量回收，以及 Hub development seed 与前端 Chrome smoke 的候选驱动证据。
- 本批不声明完整 rune call / rune tapping lifecycle、全部颜色/trait taxonomy、完整 PaymentEngine、替代/额外费用统一模型、reaction payment windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E。

## 证据事实

- `src/Riftbound.CardCatalog/BehaviorSpecCatalog.cs` 的 `OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains` 会把所有 `CardCategoryName == "符文"` 的官方卡映射到 `RUNE_RESOURCE_DOMAIN`，并保留符文卡不进入 direct `PLAY_CARD` registry。
- `tests/Riftbound.ConformanceTests/CardCatalogBaselineTests.cs` 的 `P6RuneResourceDomainMapsAllRuneEntriesWithoutMakingRunesPlayableCards` 断言 48 张符文卡都映射为 `RUNE_RESOURCE_DOMAIN`，且 `CardBehaviorRegistry.TryGetByCardNo` 对这些符文卡为 false。
- `src/Riftbound.Engine/MatchSession.cs` 的 prompt metadata 为 `RECYCLE_RUNE` 暴露 `sourcePolicy: controlled-trait-base-rune`、`resourceGain: 1-matching-trait-power`、`destination: rune-deck-bottom`。
- `src/Riftbound.Engine/MatchSession.cs` 的 `PlayCardPaymentResourcePowerByChoiceForBehavior` 只从控制者基地中可回收且带 `COLOR:*` 的符文生成 `RECYCLE_RUNE:<objectId>` 支付资源，并为每个候选公开服务端计算的 `trait` 和 `power: 1`。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs` 覆盖 red 符文作为 typed payment resource、partial spend power、red/blue contribution metadata、wrong trait rejection、generic mixed trait payment、required multiple resource payment 和 over-recycle no-mutation guard。
- `tests/Riftbound.ConformanceTests/GameHubJoinTests.cs` 覆盖 `typed-power-payment-recycle`、`typed-power-payment-double-recycle`、`typed-power-payment-mixed-recycle`、`typed-power-payment-generic-mixed-recycle` 四个 development seed 的 ActionPrompt / Hub submit / authoritative snapshot 路径。

## 验证

- focused rune resource / typed payment / Hub regression：10/10 passed。
- rune resource / recycle / payment / ActionPrompt / Hub adjacent regression：240/240 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明完整 rune call / first-turn / turn-start / tap-rune / recycle timing lifecycle、全部颜色与特殊符文 taxonomy、完整 PaymentEngine、PayCost quote/authorize/commit、替代费用、减费/加费、额外费用、reaction payment window、跨命令支付资源统一模型、hidden-info / replay / redaction 全矩阵、1009/811 full-official 或 formal 18-step E2E 已完成。
