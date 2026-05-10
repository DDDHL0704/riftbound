# 阶段 4C-29 Gust Return To Hand Guard 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Gust / 罡风 `OGN·169/298` / `FU-48662b7661` 的规则证据与 P0/P1 审计口径。本批只关闭一个极窄代表性服务端切片：visible face-up spell source 结算后，将一名正面公共战场单位且 power <= 3 的目标返回 owner hand，并强化目标 guard。它不代表 full-official，`fullOfficial=false`，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH29_GUST_RETURN_TO_HAND_GUARD_EVIDENCE.md`。

## 本批范围

- 4C-29 收 Gust / 罡风 `OGN·169/298`、`FU-48662b7661` 的 return-to-owner-hand + target guard hardening representative slice。
- 官方文本入口：2026-04-27 catalog 中 `OGN·169/298` 写明“让战场上一名不高于 3 战力的单位返回其所属的手牌”。
- 代表路径：P1 打出 Gust，选择正面公共战场单位且 power <= 3 的目标，双方 priority pass 后结算，目标返回 owner hand。
- guard：power > 3、base unit、stale object、face-down standby object、battlefield equipment 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no return-to-hand mutation。
- 本批不新增 protocol / frontend shape，不实现 full swift / reaction timing、完整 spell-duel lifecycle、完整 target selection prompt、full return-to-hand / movement / control-zone matrix、完整 PaymentEngine、完整 FEPR / targeting / stack timing、FAQ regression、1009/811 full-official 或最终 18-step E2E。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-29 状态 | 仍缺 |
|---|---|---|---|
| Gust return to owner hand | `CATALOG` `OGN·169/298`；FU `FU-48662b7661` | 已记录：正面公共战场单位且 power <= 3 的目标结算后返回 owner hand | full Gust official、reaction timing、full return-to-hand / movement matrix |
| Spell play / stack resolution | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356；`JFAQ-251023` p4 | 已记录：作为普通手牌法术代表路径加入 stack，双方 pass 后结算 | 完整 spell-duel lifecycle、反应窗口、priority / FEPR 全矩阵 |
| Target power / zone guard | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p20 rules 164-167；p39-p42 rules 355-356 | 已记录：power > 3、base unit、stale object、face-down standby object、battlefield equipment 均拒绝且 no mutation | 完整 target prompt、Spellshield target tax、hidden / face-down 原始目标策略 |
| Return-to-hand / zones | `CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 已记录：目标从 battlefield 返回 owner hand，公开对象状态移除 | owner/controller split、attached equipment、replacement / prevention / cleanup 交织 |

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gust|FullyQualifiedName~ReturnToHand|FullyQualifiedName~Return|FullyQualifiedName~Hand"` 通过 112/112。
- Small combined regression：GustReturnToHandTests + BattleOrFlight + existing Gust rejection 通过 13/13。
- Tests added in `GustReturnToHandTests`：`GustReturnsPublicSmallBattlefieldUnitToOwnerHand`、`GustRejectsInvalidTargetsWithoutMutation`。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3458/3458。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR / Rollup PURE annotation warning。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。
- 以上不替代最终正式 18-step E2E。
- D 本轮只更新 docs checkpoint / audit / evidence / index / TODO 文档；不修改 service、frontend、coverage matrix JSON、risk / baseline / freeze 文档或 `riftbound-dotnet.sln`。

## 关闭项

- `OGN·169/298` / `FU-48662b7661` 的 visible face-up Gust return public battlefield unit with power <= 3 to owner hand representative baseline 已记录。
- `GUST_RETURN_BATTLEFIELD_UNIT_POWER_3_OR_LESS_TO_HAND` 在 `PLAY_CARD` validation 中的服务端权威目标合法性代表 guard 已记录。
- power > 3、base unit、stale object、face-down standby object、battlefield equipment guard 已记录，并确认 rejected without mutation。

## 停止条件

- 不把 4C-29 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Gust 代表路径外推 swift / reaction timing、完整 spell-duel lifecycle、完整 return-to-hand / movement / control-zone matrix、full target selection prompt、PaymentEngine 或 full FEPR。
- 不声明新的 protocol / frontend shape；本批只复用现有 play-card / stack / return-to-hand evidence。
- 不关闭 Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 的 deferred / design-gated 口径。
- 不修改 E-owned coverage matrix / risk / baseline / freeze 文档；4C-29 只做 D-owned docs 收口。

## 仍存在 P0/P1

- P0：完整 swift / reaction timing、spell duel / battle lifecycle、priority window 与 FEPR 全矩阵仍未完成。
- P0：完整 return-to-hand / movement / control-zone lifecycle、owner/controller split、attached equipment、replacement / prevention / cleanup 交织仍未完成。
- P0：完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax 仍未完成。
- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵仍未完成。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates 仍未完成。
- P0：FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1：Gust return-to-hand 的 UI/DTO 解释字段、event label / replay redaction、targeting UX 与 return-to-hand UX 仍需后续全矩阵证据。
