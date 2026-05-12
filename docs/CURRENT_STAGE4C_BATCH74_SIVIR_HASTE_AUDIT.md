# Stage 4C-74 Sivir Haste Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-5bcc4063c2`
- 代表卡：希维尔 / Sivir `SFD·143/221` / cardId `33234`，以及 A 版 `SFD·143a/221` / cardId `33235`
- 代表 effects：`SIVIR_PLAY_UNIT_NO_OPTIONAL_HASTE`、`SIVIR_ALT_A_PLAY_UNIT_NO_OPTIONAL_HASTE`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand no-optional Haste play-unit route，以及代表性 `HASTE_READY` 额外费用 active-entry route。
- 本批不声明完整 PaymentEngine、完整 LayerEngine、万能符能计数、+2 战力、游走分支、完整 cleanup/control matrix 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `SFD·143/221` 与 `SFD·143a/221` 为 direct card behavior：费用 4、0 目标、源牌入基地成为 4 战力单位、标签 `急速`。
- 两个 Sivir 条目均登记代表 `HasteReadyManaCost: 1`、`HasteReadyPowerCost: 1`、`HasteReadyPowerTrait: RuneTrait.Purple`。
- no-optional fixtures 覆盖基础 4 费、0 目标入栈、双方让过后源牌进入控制者基地成为 4 战力 `CARD_TYPE:UNIT|急速` 单位。
- `p4-play-sivir-haste-ready` 与 `p4-play-sivir-alt-a-haste-ready` 覆盖代表 `HASTE_READY` 额外 1 mana + 1 purple power 支付，payload 记录 `hasteReadyOptionalCostPaid=true` 且 `isExhausted=false`。
- 既有共享 source-unit guard 覆盖 unexpected target rejection；Sivir 专项测试覆盖错色符能拒付 no-mutation。

## 验证

- focused Sivir / source-unit / HASTE_READY regression：78/78 passed。
- haste / payment / resource adjacent regression：103/103 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明完整 PaymentEngine 与所有 resource contribution 语义、万能符能支付计数、两枚万能符能后的 +2 战力与游走分支、完整 LayerEngine / continuous effects、duration cleanup、cleanup queue / replacement effects、完整 control-zone movement / ownership permutations、full FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
