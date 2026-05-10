# 阶段 4C-28 Battle or Flight Move To Base 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Battle or Flight / 战或逃 `OGN·168/298` / `FU-813144e7d4` 的规则证据与 P0/P1 审计口径。本批只关闭一个极窄代表性服务端切片：visible face-up spell source 结算后，将一名正面战场单位移动到其 owner base，并强化目标 guard。它不代表 full-official，`fullOfficial=false`，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH28_BATTLE_OR_FLIGHT_MOVE_TO_BASE_EVIDENCE.md`。

## 本批范围

- 4C-28 收 Battle or Flight / 战或逃 `OGN·168/298`、`FU-813144e7d4` 的 move-to-owner-base + target guard hardening representative slice。
- 官方文本入口：2026-04-27 catalog 中 `OGN·168/298` 写明“将一名单位从战场上移动到其所属的基地”。
- 代表路径：P1 打出 Battle or Flight，选择正面战场单位目标，双方 priority pass 后结算，目标移动到 owner base，并保留 damage / power 等对象状态。
- guard：battlefield equipment、base unit、stale object、face-down standby object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no unit movement。
- 本批不新增 protocol / frontend shape，不实现 full swift spell-duel timing、face-down standby reaction play、完整 target selection prompt、full movement / control-zone matrix、完整 PaymentEngine、完整 FEPR / targeting / stack timing、FAQ regression、1009/811 full-official 或最终 18-step E2E。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-28 状态 | 仍缺 |
|---|---|---|---|
| Battle or Flight move to owner base | `CATALOG` `OGN·168/298`；FU `FU-813144e7d4` | 已记录：正面战场单位目标结算后移动到 owner base，并保留对象状态 | full Battle or Flight official、swift / standby reaction timing、full movement matrix |
| Spell play / stack resolution | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356；`JFAQ-251023` p4 | 已记录：作为普通手牌法术代表路径加入 stack，双方 pass 后结算 | 完整 spell-duel lifecycle、反应窗口、priority / FEPR 全矩阵 |
| Movement / zones | `CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356；`SOUL-JFAQ-260114` p12、p16 | 已记录：目标从 battlefield zone 移动到 owner base，状态保留 | 全部移动来源、owner/controller split、attached equipment、cleanup / replacement / prevention 交织 |
| Target guard | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 已记录：equipment、base unit、stale object、face-down standby object 均拒绝且 no mutation | 完整 target prompt、Spellshield target tax、hidden / face-down 原始目标策略 |

## 验证记录

- Focused backend：A 记录通过 61/61。
- Tests added in `BattleOrFlightMoveToBaseTests`：`BattleOrFlightMovesFaceUpBattlefieldUnitToOwnerBase`、`BattleOrFlightRejectsInvalidTargetsWithoutMutation`。
- 本批未记录 backend full、frontend build、Chrome smoke 或 Stage 3 preflight；不得用 focused 61/61 替代最终正式 18-step E2E。
- D 本轮只更新 docs checkpoint / audit / evidence / index / TODO 文档；不修改 service、frontend、coverage matrix JSON、risk / baseline / freeze 文档或 `riftbound-dotnet.sln`。

## 关闭项

- `OGN·168/298` / `FU-813144e7d4` 的 visible face-up Battle or Flight move battlefield unit -> owner base representative baseline 已记录。
- 正面战场单位目标移动后保留 damage / power / object identity 的代表路径已记录。
- battlefield equipment、base unit、stale object、face-down standby object guard 已记录，并确认 rejected without mutation。

## 停止条件

- 不把 4C-28 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Battle or Flight 代表路径外推 swift spell-duel timing、standby face-down reaction play、完整 movement / control-zone matrix、full target selection prompt、PaymentEngine 或 full FEPR。
- 不声明新的 protocol / frontend shape；本批只复用现有 play-card / stack / move-to-base evidence。
- 不修改 E-owned coverage matrix / risk / baseline / freeze 文档；4C-28 只做 D-owned docs 收口。

## 仍存在 P0/P1

- P0：完整 spell duel / battle lifecycle、swift / reaction timing、face-down standby play 与 priority window 全矩阵仍未完成。
- P0：完整 movement / control-zone / roam lifecycle、owner/controller split、attached equipment、movement replacement / prevention / cleanup 交织仍未完成。
- P0：完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax 仍未完成。
- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵仍未完成。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Battle or Flight move-to-base 的 UI/DTO 解释字段、event label / replay redaction、movement UX 仍需后续全矩阵证据。
