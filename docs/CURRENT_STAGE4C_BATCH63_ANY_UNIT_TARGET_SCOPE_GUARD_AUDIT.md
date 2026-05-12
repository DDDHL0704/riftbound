# Stage 4C-63 AnyUnit Target Scope Guard Audit

审计日期：2026-05-13
结论：**代表性守卫已验证；项目整体仍 NOT READY。**

## 范围

- 代表 FU：`FU-abf504d74e`
- 代表卡：大副 / First Mate `OGN·132/298` / cardId `31356`
- 代表 effect：`FIRST_MATE_PLAY_UNIT_READY_ANOTHER_UNIT`
- 本批只覆盖 ordinary hand `PLAY_CARD`、支付 3 mana、选择公开场上单位、stack pass-pass 后目标变为活跃，以及 `AnyUnit` 目标域排除 non-unit / hidden / standby / dirty / stale / hand targets。

## 修复

- `CardTargetScopes.AnyUnit` 现在复用 public field-unit controller guard，只接受由所在区域玩家控制的公开场上单位。
- public field-unit guard 排除 face-down、standby、equipment、spell、rune 与控制权不匹配对象，但保留现有 trait-only unit object 模型兼容性。
- 新增 `AnyUnitTargetScopeGuardTests`，覆盖 First Mate valid target resolution、invalid target no-mutation，以及无 `TargetRequiredTag` 的 AnyUnit non-unit rejection regression。

## 验证

- focused：15/15 passed。
- target regression：16/16 passed。
- backend full：3742/3742 passed。
- frontend build：passed。
- Chrome smoke：passed。

## 非覆盖

不声明 all AnyUnit card texts / modes、formal multi-battlefield precision、standby / reaction、quick / spell-duel timing、full FEPR stack lifecycle、PaymentEngine、LayerEngine、hidden-info / redaction matrix、FAQ、1009/811 full-official 或 formal 18-step E2E 已完成。
