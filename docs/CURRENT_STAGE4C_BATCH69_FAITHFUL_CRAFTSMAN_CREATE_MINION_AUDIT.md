# Stage 4C-69 Faithful Craftsman Create Minion Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-2e2a00f575`
- 代表卡：忠实的工坊主 / Faithful Craftsman `OGN·211/298` / cardId `31447`
- 代表 effect：`FAITHFUL_CRAFTSMAN_PLAY_UNIT_CREATE_MINION`
- 本批是 evidence-only overlay，不修改功能代码；只覆盖 ordinary hand `PLAY_CARD`、支付 3 mana、0 目标入栈、stack pass-pass 后源牌进入基地成为 2 战力单位，并打出一名未横置的 1 战力 Minion unit token。

## 证据事实

- `CardBehaviorRegistry` 已登记 `OGN·211/298` 为 direct card behavior：`CreatedBaseUnitTokenCount: 1`、`CreatedBaseUnitTokenPower: 1`、`CreatedBaseUnitTokenName: 随从`、`CreatedBaseUnitTokenTags: CARD_TYPE:UNIT`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 2`。
- `CoreRuleEngine` 的 created unit token 结算路径会对 `tokenName == 随从` 自动追加 `TOKEN_FAMILY:MINION` marker。
- `p2-preflight-play-faithful-craftsman-create-minion.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。
- `CoreRuleEngineRejectsFaithfulCraftsmanWhenTargetsAreProvided` 已记录带目标打出时拒绝且不创建 token 的 no-mutation 预期。

## 验证

- focused：2/2 passed。
- Minion-token regression：18/18 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 all Minion-token creation cards / alternate token counts、complete destination selection、full token subtype/family taxonomy、standby / reaction、quick / spell-duel timing、full FEPR lifecycle、PaymentEngine、token replacement / prevention / cleanup、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E 已完成。
