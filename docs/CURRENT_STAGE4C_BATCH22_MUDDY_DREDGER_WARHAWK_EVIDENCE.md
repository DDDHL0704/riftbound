# 阶段 4C-22 Muddy Dredger Warhawk 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Muddy Dredger / 腐泥疏浚工 `UNL-153/219`、`FU-b829fb32b9` 的代表性 Last Breath -> Warhawk token 证据。4C-22 不宣称 full-official，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-22 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`UNL-153/219` Muddy Dredger / 腐泥疏浚工 | Last Breath 后打出一名 1 战力 Warhawk / “战鹰”到 controller base。 |
| Token identity | 2026-04-27 固定 catalog：`UNL·T02` Warhawk / 战鹰 | 作为创建 token 的 official identity；本批只代表其 1 power unit 与 Spellshield tag。 |
| State-based cleanup | `CORE-260330` p31-p33 rules 318-324；p14-p15 rules 142-143；p77 rule 460；`SOUL-OFAQ-260114` p19-p20 | Lethal damage cleanup 产生 `UNIT_DESTROYED`，作为 Last Breath 入队来源。 |
| Trigger ordering / stack | `CORE-260330` p33-p35 rules 333-340；p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | 本批沿用 `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` 代表路径。 |
| Hidden information | `CORE-260330` p4-p8 rules 107-129 | hidden / face-down / standby / invalid source 不入队、不泄漏、不创建 token。 |

## 实现证据

- Muddy Dredger representative route：visible face-up Muddy Dredger source 经 state-based cleanup 被摧毁后入队，排序后进入 stack，双方 priority pass 后结算。
- Result event：`TRIGGER_RESOLVED` 后 `UNIT_TOKEN_CREATED`，Warhawk `UNL·T02` 出现在 controller base。
- Guard route：hidden / face-down / standby / invalid source 不产生 `TRIGGER_QUEUED`，不显示 hidden source metadata，不创建 Warhawk token。
- Spellshield boundary：Warhawk 的 Spellshield 只以 token tag / identity 代表；本批不覆盖 Spellshield target tax、额外费用支付或不足支付。

## 验证

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MuddyDredger|FullyQualifiedName~RealTriggerQueue"` 通过 52/52。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3407/3407。

## 仍未关闭

- 完整 trigger engine、complete APNAP、trigger batch、可选触发选择与完整 effect resolution。
- true stack route、其他 Last Breath / destroyed / friendly-destroyed FUs、simultaneous destruction multiplicity matrix。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- Spellshield target tax / mandatory additional cost / multi-target tax / insufficient payment regression。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
