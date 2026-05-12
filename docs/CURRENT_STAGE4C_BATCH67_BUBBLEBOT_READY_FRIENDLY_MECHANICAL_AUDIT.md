# Stage 4C-67 Bubblebot Ready Friendly Mechanical Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-3f5a9ef0e0`
- 代表卡：泡泡机 / Bubblebot `SFD·062/221` / cardId `33142`
- 代表 effect：`BUBBLEBOT_PLAY_UNIT_READY_FRIENDLY_MECHANICAL`
- 本批是 evidence-only overlay，不修改功能代码；只覆盖 ordinary hand `PLAY_CARD`、支付 3 mana、选择另一名友方公开机械单位、stack pass-pass 后源牌进入基地成为 3 战力单位，并让该机械目标变为活跃状态。

## 证据事实

- `CardBehaviorRegistry` 已登记 `SFD·062/221` 为 direct card behavior：`TargetScope: FriendlyUnit`、`TargetRequiredTag: CARD_TYPE:UNIT|机械`、`ReadiesTarget: true`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 3`。
- `p2-preflight-play-bubblebot-ready-friendly-mechanical.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。
- `ConformanceFixtureRunnerTests` 既有 fixture 与非机械目标拒绝测试覆盖该路线。

## 验证

- focused：2/2 passed。
- mechanical / ready regression：32/32 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 all friendly Mechanical ready cards / alternate modes、full FriendlyUnit target-scope matrix、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR lifecycle、PaymentEngine、readiness replacement / prevention、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E 已完成。
