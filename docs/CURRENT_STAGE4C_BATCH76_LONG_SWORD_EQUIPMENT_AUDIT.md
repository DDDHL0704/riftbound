# Stage 4C-76 Long Sword Equipment Audit

审计日期：2026-05-13
结论：**代表性证据已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-5accdd09f9`
- 代表卡：长剑 / Long Sword `SFD·022/221` / cardId `33095`
- 代表 effect：`LONG_SWORD_AGILE_PLAY_EQUIPMENT`
- 本批是 evidence-only overlay，不修改功能代码；覆盖 ordinary hand `PLAY_CARD`、支付 2 mana、0 目标入栈、stack / pass-pass 后源牌进入控制者基地成为 `CARD_TYPE:EQUIPMENT` / `武装` / `灵便` 装备对象，并记录显式目标拒绝、最小 `ASSEMBLE_RED` 贴附与 owner/controller 贴附身份代表证据。
- 本批不声明灵便反应贴附、完整装备装配系统、完整装备移动 / 离场目的地矩阵、LayerEngine 装备加成 / 未激活文本、完整 PaymentEngine、完整 FEPR 或 full-official 覆盖。

## 证据事实

- `CardBehaviorRegistry` 已登记 `SFD·022/221` 为 direct card behavior：费用 2、0 目标、`PlaysSourceToBaseAsEquipment: true`、`SourceEquipmentTags: 武装|灵便`。
- `p2-preflight-play-long-sword-agile-equipment.fixture.json` 覆盖普通手牌打出路径：支付 2、0 目标入栈、双方让过后《长剑》进入 P1 基地成为带装备 / 武装 / 灵便标签的对象。
- `p4-play-long-sword-target-rejected.fixture.json` 覆盖给 0 目标装备打出路径提供显式单位目标时拒绝，且不推进 tick、不支付费用、不移动手牌、不入场装备、不创建结算链。
- `p4-assemble-equipment-long-sword-attach.fixture.json` 与 `p5-equipment-state-assemble-long-sword-owner-controller.fixture.json` 覆盖最小 `ASSEMBLE_RED` 贴附和 owner/controller 身份保持。
- `p5-move-unit-attached-equipment-follows-host.fixture.json` 与 `p5-equipment-detaches-when-host-destroyed.fixture.json` 作为邻接回归保持代表性随动 / 脱离行为全绿；本批不把它们扩展为完整装备生命周期声明。

## 验证

- focused Long Sword regression：11/11 passed。
- equipment / attach / move adjacent regression：336/336 passed。
- backend full：3754/3754 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 Long Sword 的 Agile reaction attach timing、完整 `ASSEMBLE_EQUIPMENT` profile / attachment lifecycle、全部装备移动 / 脱离 / 目的地排列、LayerEngine 装备战力修正与未激活文本、PaymentEngine beyond represented base pay / `ASSEMBLE_RED`、replacement / prevention / cleanup interactions、complete FEPR target / stack / timing windows、hidden-info / redaction matrix、1009/811 full-official 或 formal 18-step E2E 已完成。
