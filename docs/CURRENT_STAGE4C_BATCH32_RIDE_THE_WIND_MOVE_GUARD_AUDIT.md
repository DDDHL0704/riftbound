# 阶段 4C-32 Ride the Wind Move Guard 审计

更新时间：2026-05-10
结论：**NOT READY**

本文记录 Ride the Wind / 驭风而行 `OGN·173/298` / cardId `31403` / `FU-6f84196631` / `RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY` 的规则证据与 P0/P1 审计口径。本批只关闭一个极窄代表性服务端切片：visible face-up spell source 结算后，合法 friendly public battlefield unit target ready 并移动到 owner base，并强化目标 guard。它不代表 full-official，`fullOfficial=false`，不得标记 READY / READY-CANDIDATE，不启动最终 18-step E2E。

配套证据文档：`docs/CURRENT_STAGE4C_BATCH32_RIDE_THE_WIND_MOVE_GUARD_EVIDENCE.md`。

## 本批范围

- 4C-32 收 Ride the Wind / 驭风而行 `OGN·173/298`、cardId `31403`、`FU-6f84196631` / `RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY` 的 ready + move-to-owner-base + target guard hardening representative slice。
- 官方文本入口：2026-04-27 catalog 中 `OGN·173/298` 写明“移动一名友方单位，然后让其变为活跃状态”。
- 代表路径：P1 打出 Ride the Wind，选择合法 friendly public battlefield unit target，双方 priority pass 后结算，目标 ready 并移动到 owner base。
- guard：enemy battlefield unit、friendly base unit、stale unit、face-down standby object、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object 均 `INVALID_TARGET`，no tick / no events / no payment / no hand movement / no stack item / no ready / no move / no leak。
- hidden-info stance：face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续由 viewer-specific snapshot / redaction 保护。
- 本批不新增 protocol / frontend shape，不实现 full swift / reaction timing、spell-duel breadth、完整 target selection prompt、full movement / roam / precise battlefield / control-zone matrix、完整 PaymentEngine、完整 FEPR / targeting / stack timing、full FAQ regression、1009/811 full-official 或最终 18-step E2E。
- Ride the Wind / swift 相关 swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix 保持 P1/P2 后续项；本批不新增这些方向的 P0。
- Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 仍保持 deferred / design-gated，不由本批关闭。

## 规则证据入口

| 规则域 | 证据入口 | 当前 4C-32 状态 | 仍缺 |
|---|---|---|---|
| Ride the Wind move + ready | `CATALOG` `OGN·173/298`；cardId `31403`；FU `FU-6f84196631` | 已记录：合法 friendly public battlefield unit target 结算后 ready 并移动到 owner base | full Ride the Wind official、完整“移动一名友方单位”目的地与来源矩阵、swift / reaction timing |
| Spell play / stack resolution | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356；`JFAQ-251023` p4 | 已记录：作为普通手牌法术代表路径加入 stack，双方 pass 后结算 | 完整 spell-duel lifecycle、反应窗口、priority / FEPR 全矩阵 |
| Target zone / type / controller guard | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已记录：enemy battlefield unit、friendly base unit、stale unit、face-down standby object、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object 均拒绝且 no mutation | 完整 target prompt、Spellshield target tax、hidden / face-down 原始目标策略 |
| Hidden-info boundary | viewer-specific snapshot / redaction policy；`CORE-260330` p4-p8 rules 107-129 | 已记录：face-down standby target 被拒绝且不暴露真实身份 | 完整 hidden / face-down target matrix 与 replay redaction |
| Movement / ready / zones | `CORE-260330` p4-p8 rules 107-129；p31-p33 rules 318-324；p39-p42 rules 355-356 | 已记录：目标从 battlefield 移动到 owner base 并 ready，本批无 protocol / frontend shape | owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix 保持 P1/P2 后续项 |

## 验证记录

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RideTheWind|FullyQualifiedName~Ride|FullyQualifiedName~MoveGuard"` 通过 11/11。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 32/32。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3479/3479。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Tests added in `RideTheWindMoveGuardTests`：`RideTheWindReadiesAndMovesPublicFriendlyBattlefieldUnitToOwnerBase`、`RideTheWindRejectsInvalidTargetsWithoutMutation`。
- 上述验证不得替代最终正式 18-step E2E。
- D 本轮只更新 docs checkpoint / audit / evidence / index / TODO 文档；不修改 service、frontend、coverage matrix JSON、risk / baseline / freeze 文档或 `riftbound-dotnet.sln`。

## 关闭项

- `OGN·173/298` / cardId `31403` / `FU-6f84196631` 的 visible face-up Ride the Wind ready + move friendly public battlefield unit to owner base representative baseline 已记录。
- `RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY` 在 `PLAY_CARD` validation 中的服务端权威目标合法性代表 guard 已记录。
- enemy battlefield unit、friendly base unit、stale unit、face-down standby object、friendly battlefield equipment、friendly battlefield spell object、friendly battlefield rune object guard 已记录，并确认 rejected without mutation。
- face-down standby invalid target 不暴露真实身份 / opponent hidden info 保护口径已记录。

## 停止条件

- 不把 4C-32 标记为 full-official、READY 或 READY-CANDIDATE。
- 不因 Ride the Wind 代表路径外推 swift / reaction timing、spell-duel breadth、完整 movement / roam / precise battlefield / control-zone matrix、full target selection prompt、PaymentEngine 或 full FEPR。
- 不声明新的 protocol / frontend shape；本批只复用现有 play-card / stack / move / ready evidence。
- 不关闭 Hostile Takeover、Berserk Impulse、Edge of Night、Karthus、Aphelios 的 deferred / design-gated 口径。
- 不修改 E-owned coverage matrix / risk / baseline / freeze 文档；4C-32 只做 D-owned docs 收口。
- owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix 保持 P1/P2 后续项；不新增为本批 P0。

## 仍存在 P0/P1

- P0：完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵仍未完成。
- P0：Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates 仍未完成。
- P0：full FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E 与 completion audit。
- P1/P2：Ride the Wind / swift 相关 swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax、UI/DTO 解释字段、event label / replay redaction、targeting UX 与 movement / ready UX 仍需后续全矩阵证据。
