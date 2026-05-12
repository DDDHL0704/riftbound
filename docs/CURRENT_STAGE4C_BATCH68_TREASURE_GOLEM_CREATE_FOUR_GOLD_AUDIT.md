# Stage 4C-68 Treasure Golem Create Four Gold Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-7472703e56`
- 代表卡：宝藏魔像 / Treasure Golem `SFD·174/221` / cardId `33270`
- 代表 effect：`TREASURE_GOLEM_PLAY_UNIT_CREATE_FOUR_GOLD`
- 本批是 evidence-only overlay，不修改功能代码；只覆盖 ordinary hand `PLAY_CARD`、支付 8 mana、0 目标入栈、stack pass-pass 后源牌进入基地成为 9 战力单位，并打出四个休眠的 Gold equipment token。

## 证据事实

- `CardBehaviorRegistry` 已登记 `SFD·174/221` 为 direct card behavior：`CreatedBaseEquipmentTokenCount: 4`、`CreatedBaseEquipmentTokenName: 金币`、`CreatedBaseEquipmentTokenTags: CARD_TYPE:EQUIPMENT`、`CreatedBaseEquipmentTokenIsExhausted: true`、`PlaysSourceToBaseAsUnit`、`SourceUnitPower: 9`。
- `p2-preflight-play-treasure-golem-create-four-gold.fixture.json` 已记录官方卡面、核心规则证据和完整 pass-pass 结算预期。
- `p4-play-treasure-golem-target-rejected.fixture.json` 已记录带目标打出时拒绝且不创建 Gold token 的 no-mutation 预期。

## 验证

- focused：3/3 passed。
- Gold-token regression：30/30 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 all Gold-token creation cards / alternate token counts、complete destination selection、Gold equipment spend / activation / extra-mana interactions、standby / reaction、quick / spell-duel timing、full FEPR lifecycle、PaymentEngine、equipment cleanup / replacement、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E 已完成。
