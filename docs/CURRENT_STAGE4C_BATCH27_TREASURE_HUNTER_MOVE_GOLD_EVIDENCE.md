# 阶段 4C-27 Treasure Hunter Move Gold 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Treasure Hunter / 寻宝猎人 `SFD·130/221`、`FU-6144ab0271` 的代表性 move -> dormant Gold equipment token 证据。4C-27 不宣称 full-official，`fullOfficial=false`，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-27 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`SFD·130/221` Treasure Hunter / 寻宝猎人 | 只取“每当我移动时，打出一个休眠的‘金币’装备指示物”作为代表路径。 |
| Movement | `CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356；`SOUL-JFAQ-260114` p21 | 本批使用现有 `MOVE_UNIT` base -> battlefield 与 precise `ROAM` battlefield A -> battlefield B 移动路径。 |
| Trigger resolution | `CORE-260330` p31-p35 rules 318-340；p52-p55 rules 383.3.d-383.3.e | 移动成功后记录 `TRIGGER_RESOLVED` / `EQUIPMENT_TOKEN_CREATED` representative route。 |
| Gold equipment token | Gold token catalog；`CORE-260330` p89 rules 718-719 | 创建 dormant / exhausted `CARD_TYPE:EQUIPMENT`、`金币`、`反应` token 到 controller base。 |
| Hidden information | `CORE-260330` p4-p8 rules 107-129 | hidden / face-down / standby / opponent-controlled source 不触发、不泄漏。 |

## 实现证据

- Representative route：visible face-up Treasure Hunter source 成功通过 existing `MOVE_UNIT` 从 base 移动到 battlefield 后，服务端记录 `TRIGGER_RESOLVED`，并创建一个 dormant Gold equipment token 到 controller base。
- Precise roam route：visible face-up Treasure Hunter 从 `BATTLEFIELD:P1-BATTLEFIELD-A` 精确 `ROAM` 到 `BATTLEFIELD:P1-BATTLEFIELD-B` 后，创建一个 dormant Gold equipment token，并在事件 payload 中保留 origin / destination。
- Guard route：non-Treasure Hunter move、hidden / face-down / standby / opponent-controlled source、failed move、precise roam no-op 均不产生 Treasure Hunter trigger event，不创建 Gold token，不泄漏 hidden source metadata。
- Protocol boundary：本批无新增 protocol / frontend shape，只复用现有 movement command / event 与 token creation evidence。
- Boundary：本批不覆盖 full move-trigger family、完整 control-zone movement matrix、完整 trigger engine / `ORDER_TRIGGERS`、Gold token resource / reaction ability、full equipment-token rules、hidden / face-down original trigger modeling、FAQ regression、1009/811 full-official 或正式 18-step E2E。
- Karthus / `FU-ee1dfb3ed3` 保持 design-gated，不由本批关闭。

## 验证

- Focused backend：A 记录通过 82/82。
- Small regression：A 记录通过 121/121。
- Tests added in `TreasureHunterMoveTriggerTests`：`TreasureHunterMoveCreatesDormantGoldToken`、`TreasureHunterHiddenStandbyOrOpponentControlledDoesNotTrigger`、`NonTreasureHunterMoveDoesNotTrigger`、`FailedTreasureHunterMoveDoesNotCreateGold`、`TreasureHunterPreciseRoamMoveCreatesDormantGoldToken`、`TreasureHunterPreciseRoamNoOpDoesNotCreateGold`。

## 仍未关闭

- 完整 battlefield / standby / control / held / conquer lifecycle、control-zone movement、movement legality、equipment movement / detach / attach 全矩阵。
- 完整 trigger engine、complete APNAP、trigger batch、optional trigger handling、`ORDER_TRIGGERS` 与完整 effect resolution。
- 完整 move-trigger family、同次多移动、多来源同时移动、movement replacement / prevention、移动触发与 cleanup 交织。
- Gold token resource / reaction ability、equipment token full rules、token ownership / controller / visibility 全矩阵。
- hidden / face-down 原始触发建模、viewer-specific metadata 全路径、显露窗口。
- UI/DTO 解释字段、event label / replay redaction、Roam / movement UX。
- Karthus `FU-ee1dfb3ed3` optional extra Last Breath design gate、FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
