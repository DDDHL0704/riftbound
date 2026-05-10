# 阶段 4C-30 Hunt the Weak Destroy Guard 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Hunt the Weak / 狩魂 `UNL-159/219` / `FU-282b6e3149` 的规则证据与 P0/P1 审计口径。本批只关闭一个极窄代表性服务端切片：visible face-up spell source 结算后，将一名正面公共战场单位且 power <= 3 的目标摧毁到 owner graveyard，并强化目标 guard。它不代表 full-official，`fullOfficial=false`，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH30_HUNT_THE_WEAK_DESTROY_GUARD_EVIDENCE.md`。

## 本批范围

- 4C-30 收 Hunt the Weak / 狩魂 `UNL-159/219`、`FU-282b6e3149` 的 destroy-to-owner-graveyard + target guard hardening representative slice。
- 官方文本入口：2026-04-27 catalog 中 `UNL-159/219` 写明“摧毁战场上一名不高于 3 战力的单位”。
- 代表路径：P1 打出 Hunt the Weak，选择正面公共战场单位且 power <= 3 的目标，双方 priority pass 后结算，目标被摧毁并进入 owner graveyard。
- guard：power > 3、base unit、stale object、face-down standby object、battlefield equipment 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no destroy mutation。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape，不实现 full swift / reaction timing、完整 spell-duel lifecycle、完整 target selection prompt、full destroy / cleanup / Last Breath trigger interactions、完整 PaymentEngine、完整 FEPR / targeting / stack timing、FAQ regression、1009/811 full-official 或最终 18-step E2E。
- Hunt the Weak 相关 replacement / prevention / cleanup / full targeting matrix 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-30 状态 | 仍缺 |
|---|---|---|---|
| Hunt the Weak destroy to owner graveyard | `CATALOG` `UNL-159/219`；FU `FU-282b6e3149` | 已记录：正面公共战场单位且 power <= 3 的目标结算后被摧毁并进入 owner graveyard | full Hunt the Weak official、reaction timing、full destroy / cleanup matrix |
| Spell play / stack resolution | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356；`JFAQ-251023` p4 | 已记录：作为普通手牌法术代表路径加入 stack，双方 pass 后结算 | 完整 spell-duel lifecycle、反应窗口、priority / FEPR 全矩阵 |
| Target power / zone guard | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p20 rules 164-167；p39-p42 rules 355-356；p62-p63 rule 428 | 已记录：power > 3、base unit、stale object、face-down standby object、battlefield equipment 均拒绝且 no mutation | 完整 target prompt、Spellshield target tax、hidden / face-down 原始目标策略 |
| Hidden-info boundary | viewer-specific snapshot / redaction policy；`CORE-260330` p4-p8 rules 107-129 | 已记录：face-down standby target 被拒绝且不暴露真实身份 | 完整 hidden / face-down target matrix 与 replay redaction |
| Destroy / zones | `CORE-260330` p62-p63 rule 428 | 已记录：目标从 battlefield 摧毁到 owner graveyard，公开对象状态移除 | replacement / prevention / cleanup / Last Breath trigger interaction 保持 P1/P2 后续项 |

## 验证记录

- Focused backend：A 记录 Hunt the Weak focused 通过 34/34。
- Adjacent regression：A 记录 adjacent 通过 19/19。
- Tests added in `HuntTheWeakDestroyGuardTests`：`HuntTheWeakDestroysPublicSmallBattlefieldUnit`、`HuntTheWeakRejectsInvalidTargetsWithoutMutation`。
- Backend full：A 记录 `dotnet test Riftbound.slnx --no-restore` 通过 3464/3464。
- Frontend build：A 记录 `npm run build` 通过。
- Chrome smoke：A 记录 `npm run smoke:chrome -- --start-api` 通过。
- 本批未运行 Stage 3 preflight；不得用 focused 34/34、adjacent 19/19、backend full、build 或 smoke 替代最终正式 18-step E2E。
- D 本轮只更新 docs checkpoint / audit / evidence / index / TODO 文档；不修改 service、frontend、coverage matrix JSON、risk / baseline / freeze 文档或 `riftbound-dotnet.sln`。

## 关闭项

- `UNL-159/219` / `FU-282b6e3149` 的 visible face-up Hunt the Weak destroy public battlefield unit with power <= 3 to owner graveyard representative baseline 已记录。
- `HUNT_THE_WEAK_DESTROY_BATTLEFIELD_UNIT_POWER_3_OR_LESS` 在 `PLAY_CARD` validation 中的服务端权威目标合法性代表 guard 已记录。
- power > 3、base unit、stale object、face-down standby object、battlefield equipment guard 已记录，并确认 rejected without mutation。
- face-down standby invalid target 不暴露真实身份 / opponent hidden info 保护口径已记录。

## 停止条件

- 不把 4C-30 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Hunt the Weak 代表路径外推 swift / reaction timing、完整 spell-duel lifecycle、完整 destroy / cleanup / Last Breath trigger interactions、full target selection prompt、PaymentEngine 或 full FEPR。
- 不声明新的 protocol / frontend shape；本批只复用现有 play-card / stack / destroy evidence。
- 不关闭 Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 的 deferred / design-gated 口径。
- 不修改 E-owned coverage matrix / risk / baseline / freeze 文档；4C-30 只做 D-owned docs 收口。
- replacement / prevention / cleanup / full targeting matrix 保持 P1/P2 后续项；不新增为本批 P0。

## 仍存在 P0/P1

- P0：完整 swift / reaction timing、spell duel / battle lifecycle、priority window 与 FEPR 全矩阵仍未完成。
- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵仍未完成。
- P0：完整 destroy / cleanup / Last Breath trigger interactions、state-based cleanup 与 simultaneous destruction full-official matrix 仍未完成。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates 仍未完成。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Hunt the Weak 相关 replacement / prevention / cleanup / full targeting matrix、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、UI/DTO 解释字段、event label / replay redaction、targeting UX 与 destroy UX 仍需后续全矩阵证据。
