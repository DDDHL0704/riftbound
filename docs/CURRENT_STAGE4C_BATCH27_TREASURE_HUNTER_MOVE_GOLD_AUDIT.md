# 阶段 4C-27 Treasure Hunter Move Gold 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Treasure Hunter / 寻宝猎人 `SFD·130/221` / `FU-6144ab0271` 的规则证据与 P0/P1 审计口径。本批只关闭一个极窄代表性服务端切片：visible face-up Treasure Hunter 通过现有权威移动路径移动后，结算 `TREASURE_HUNTER_MOVE_CREATE_GOLD`，为其控制者打出一个休眠 Gold / 金币 equipment token。它不代表 full-official，`fullOfficial=false`，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH27_TREASURE_HUNTER_MOVE_GOLD_EVIDENCE.md`。

## 本批范围

- 4C-27 收 Treasure Hunter / 寻宝猎人 `SFD·130/221`、`FU-6144ab0271` 的 move -> dormant Gold representative slice。
- 官方文本入口：2026-04-27 catalog 中 `SFD·130/221` 写明“每当我移动时，打出一个休眠的‘金币’装备指示物”。
- 代表路径：visible face-up Treasure Hunter source -> existing authoritative `MOVE_UNIT` / precise `ROAM` move route -> `TREASURE_HUNTER_MOVE_CREATE_GOLD` -> `EQUIPMENT_TOKEN_CREATED` dormant Gold token in controller base。
- guard：non-Treasure Hunter move no prompt / no token；hidden / face-down / standby / opponent-controlled source no trigger / no leak；failed move / no-op precise roam no token。
- 本批不新增 protocol / frontend shape，不实现 full move-trigger family、完整 control-zone movement matrix、完整 trigger engine / `ORDER_TRIGGERS`、Gold token resource / reaction ability、full equipment-token rules、hidden / face-down original trigger modeling、FAQ regression、1009/811 full-official 或最终 18-step E2E。
- Karthus / `FU-ee1dfb3ed3` 继续 design-gated，不进入 4C-27 implementation。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-27 状态 | 仍缺 |
|---|---|---|---|
| Treasure Hunter move Gold | `CATALOG` `SFD·130/221`；FU `FU-6144ab0271` | 已记录：visible face-up Treasure Hunter 成功移动后创建一个 dormant Gold equipment token | full Treasure Hunter official、完整 move-trigger family、同次多移动 / 多来源矩阵 |
| Movement / control-zone | `CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356；`SOUL-JFAQ-260114` p21 | 已记录：复用现有 `MOVE_UNIT` base -> battlefield 与 precise `ROAM` battlefield A -> battlefield B 代表路径 | 完整 control-zone movement、战场控制权、装备随行、非法待命清理、移动触发顺序 |
| Trigger / effect resolution | `CORE-260330` p31-p35 rules 318-340；p52-p55 rules 383.3.d-383.3.e | 已记录：移动成功后以 `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED` 代表结算 | complete trigger engine、APNAP / trigger batch、optional trigger handling、`ORDER_TRIGGERS` 全矩阵 |
| Gold equipment token | `CORE-260330` p89 rules 718-719；Gold token catalog | 已记录：创建 dormant / exhausted `CARD_TYPE:EQUIPMENT`、`金币`、`反应` token 到 controller base | Gold token resource ability、reaction timing、equipment token full official、Renata bonus 等全矩阵 |
| Hidden / invalid source guard | `CORE-260330` p4-p8 rules 107-129 | 已记录：hidden / face-down / standby / opponent-controlled source no trigger / no leak；failed move / no-op move no token | face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口 |

## 验证记录

- Focused backend：A 记录通过 82/82。
- Small regression：A 记录通过 121/121。
- Tests added in `TreasureHunterMoveTriggerTests`：`TreasureHunterMoveCreatesDormantGoldToken`、`TreasureHunterHiddenStandbyOrOpponentControlledDoesNotTrigger`、`NonTreasureHunterMoveDoesNotTrigger`、`FailedTreasureHunterMoveDoesNotCreateGold`、`TreasureHunterPreciseRoamMoveCreatesDormantGoldToken`、`TreasureHunterPreciseRoamNoOpDoesNotCreateGold`。
- 本批未记录 backend full、frontend build、Chrome smoke 或 Stage 3 preflight；不得用 focused / small regression 替代最终正式 18-step E2E。
- D 本轮只更新 docs checkpoint / audit / evidence / index / TODO 文档；不修改 service、frontend、coverage matrix JSON、risk / baseline / freeze 文档或 `riftbound-dotnet.sln`。

## 关闭项

- `SFD·130/221` / `FU-6144ab0271` 的 visible face-up Treasure Hunter move -> dormant Gold representative baseline 已记录。
- existing `MOVE_UNIT` base -> battlefield 成功移动后创建 dormant Gold 的代表路径已记录。
- precise `ROAM` battlefield A -> battlefield B 成功移动后创建 dormant Gold 的代表路径已记录。
- failed move、precise roam no-op、non-Treasure Hunter、hidden / face-down / standby / opponent-controlled source guard 已记录。

## 停止条件

- 不把 4C-27 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Treasure Hunter 代表路径外推 full move-trigger family、完整 movement/control-zone matrix、完整 trigger engine / `ORDER_TRIGGERS`、Gold token resource ability 或 full equipment-token rules。
- 不声明新的 protocol / frontend shape；本批只复用现有 movement events 与 token creation evidence。
- Karthus / `FU-ee1dfb3ed3` 仍需 optional extra Last Breath / multiplicity / visibility / trigger-generation design gate；不得被 4C-27 暗示关闭。

## 仍存在 P0/P1

- P0：完整 battlefield / standby / control / held / conquer lifecycle、control-zone movement、movement legality、equipment movement / detach / attach 全矩阵仍未完成。
- P0：完整 trigger engine、complete APNAP / trigger batch、optional trigger handling、`ORDER_TRIGGERS` 与完整 effect resolution。
- P0：完整 move-trigger family、同次多移动、多来源同时移动、movement replacement / prevention、移动触发与 cleanup 交织。
- P0：Gold token resource / reaction ability、equipment token full rules、token ownership / controller / visibility 全矩阵。
- P0：hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- P0：Karthus `FU-ee1dfb3ed3` optional extra Last Breath design gate、FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Treasure Hunter move Gold 的 UI/DTO 解释字段、event label / replay redaction、Roam / movement UX 仍需后续全矩阵证据。
