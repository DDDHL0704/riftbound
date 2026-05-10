# 阶段 4C-22 Muddy Dredger Warhawk Baseline 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 D 对 Muddy Dredger / 腐泥疏浚工 `UNL-153/219` / `FU-b829fb32b9` 的规则证据与 P0/P1 审计口径。本批只关闭一个代表性服务端切片：visible face-up Last Breath 经 state-based cleanup 入队、排序、入栈、让过优先权后创建 Warhawk token。它不代表 full-official，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH22_MUDDY_DREDGER_WARHAWK_EVIDENCE.md`。

## 本批范围

- A 已决定 4C-22 收 Muddy Dredger / 腐泥疏浚工 `UNL-153/219` / `FU-b829fb32b9`，而不是 Aphelios / `FU-67c6b0186e`。理由是 B/D 均判断 Muddy 是低耦合服务端 representative slice，且本批代码、focused backend 与 backend full 已通过。
- 4C-22 基线从 checkpoint `5241179` `checkpoint: complete stage 4C sunken temple trigger payment baseline` 之后继续推进；项目仍 **NOT READY**。
- 官方文本入口：2026-04-27 catalog 中 `UNL-153/219` 写明 Last Breath 后打出一名 1 战力 Warhawk / “战鹰”到你的基地，它拥有 Spellshield / 法盾。
- Token 入口：2026-04-27 catalog 中 `UNL·T02` Warhawk / “战鹰”为 1 战力 token unit，具备 Spellshield / 法盾文本。
- 规则路径：visible face-up Muddy Dredger 被 state-based cleanup 摧毁 -> `UNIT_DESTROYED` -> `TriggerQueue` -> `ORDER_TRIGGERS` -> `StackItems` -> priority pass -> `TRIGGER_RESOLVED` -> `UNIT_TOKEN_CREATED` Warhawk `UNL·T02` 到 controller base。
- 本批只以 token tag 表示 Warhawk 的 Spellshield。Spellshield target tax / additional cost 全矩阵不在本批关闭。
- hidden / face-down / standby / invalid Muddy source 应 no enqueue / no leak / no token。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-22 状态 | 仍缺 |
|---|---|---|---|
| Muddy Dredger Last Breath | `CATALOG` `UNL-153/219`；FU `FU-b829fb32b9` | 已记录：visible face-up Muddy Dredger 在 state-based cleanup destruction 后进入真实触发队列 | true stack route、完整 Last Breath family、simultaneous destruction multiplicity |
| Warhawk token identity | `CATALOG` `UNL·T02` | 已记录：priority pass 后创建 Warhawk token 到 controller base，并带 unit / Spellshield 代表 tag | token “play” vs create 的完整官方语义、所有 token family taxonomy |
| State-based cleanup lethal destroy | `CORE-260330` p31-p33 rules 318-324；`CORE-260330` p14-p15 rules 142-143；`CORE-260330` p77 rule 460；`SOUL-OFAQ-260114` p19-p20 | 已记录：state-based cleanup route 可作为 Muddy Last Breath 触发来源 | replacement / prevention、repeat-until-stable、更多 cleanup 来源 |
| Trigger queue / ordering / stack / priority | `CORE-260330` p33-p35 rules 333-340；`CORE-260330` p52-p55 rules 383.3.d-383.3.e；`JFAQ-251023` p2-p4 q2.2-q2.3 | 已记录：`TriggerQueue`、`ORDER_TRIGGERS`、`StackItems` 与 priority pass 后结算 | 完整 trigger engine、complete APNAP / trigger batch / optional trigger handling |
| Hidden / invalid source guard | `CORE-260330` p4-p8 rules 107-129 | 已记录：hidden / face-down / standby / invalid source no enqueue / no leak / no token | face-down 原始触发建模、显露窗口、viewer-specific trigger metadata 全路径 |
| Spellshield on Warhawk | `CATALOG` `UNL·T02`；`CORE-260330` p92-p105 keyword rules 800+；`SOUL-OFAQ-260114` p1-p4 | 本批只记录 token tag 代表 Spellshield identity | Spellshield target tax、强制额外费用、multi-target tax 与不足支付回归 |

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~MuddyDredger|FullyQualifiedName~RealTriggerQueue"` 通过 52/52。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3407/3407。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Stage 3 preflight 本批未运行；不得用本批 focused / backend full / smoke 替代正式 18-step E2E。
- D 本轮只更新 docs 审计 / 证据 / checkpoint 文档；不修改服务端、前端、coverage matrix JSON 或 `riftbound-dotnet.sln`。

## 关闭项

- Muddy Dredger `UNL-153/219` / `FU-b829fb32b9` 的 visible face-up state-based cleanup Last Breath representative baseline 已记录为通过。
- Warhawk `UNL·T02` token 创建到 controller base 的代表路径已记录。
- hidden / face-down / standby / invalid source no enqueue / no leak / no token 的代表 guard 已记录。
- Spellshield 只作为 Warhawk token identity / tag 记录，不关闭 target tax 回归。

## 停止条件

- 不把 4C-22 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Muddy Dredger 代表路径外推 Aphelios、Icevale Archer、Lux、Vayne 或其他 4C-22 候选。
- 如后续需要裁决 Warhawk 文本中“打出”与当前 `UNIT_TOKEN_CREATED` 的完整语义差异，先停下并交由 A / 用户 adjudication。
- 如后续需要 Spellshield target tax、additional cost 或 multi-target tax 回归，另开支付 / keyword 专项，不在本批补做。
- 如需要修改服务端、前端、coverage matrix JSON 或 `riftbound-dotnet.sln` 才能继续，本批 D 文档收口应停止。

## 仍存在 P0/P1

- P0：完整 trigger engine、complete APNAP / trigger batch、可选触发选择、完整 effect resolution 仍未关闭。
- P0：完整 Last Breath / destroyed / friendly-destroyed family、same source / same cleanup pass / simultaneous destruction multiplicity matrix 仍未关闭。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口仍未关闭。
- P0：Spellshield target tax / mandatory additional cost / multi-target tax / insufficient payment 回归不由本批关闭。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit 仍未完成。
- P1：Warhawk token “打出”语义、token identity / family taxonomy、token source / ownership / controller event fields 仍需后续全矩阵证据。
