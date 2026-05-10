# 阶段 4C-31 Reprimand Return To Hand Guard 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Reprimand / 责退 `OGN·172/298`、`FU-d0383ed260` / `REPRIMAND_RETURN_BATTLEFIELD_UNIT_TO_HAND` 的代表性 return public battlefield unit to owner hand 与 target guard hardening 证据。4C-31 不宣称 full-official，`fullOfficial=false`，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-31 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`OGN·172/298` Reprimand / 责退 | 只取“让一名战场上的单位返回其所属的手牌”作为代表路径。 |
| Spell / stack | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356；`JFAQ-251023` p4 | 本批使用普通手牌法术打出 -> stack -> priority pass -> resolve 代表路径；不关闭完整 swift / reaction timing 或 spell-duel breadth。 |
| Target validity | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | base unit、stale object、face-down standby、battlefield equipment、battlefield spell object、battlefield rune object 均拒绝并 no mutation。 |
| Hidden info | viewer-specific snapshot / redaction policy；`CORE-260330` p4-p8 rules 107-129 | face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续受保护。 |
| Return-to-hand / zones | `CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 目标从 battlefield 返回 owner hand，并移除公开场上对象状态。 |

## 实现证据

- Representative route：P1 打出 Reprimand，目标为 P2 正面公共战场单位；双方 pass priority 后，服务端记录 `UNIT_RETURNED_TO_HAND`，目标进入 P2 owner hand。
- State boundary：目标回手后不再保留在 public `CardObjects` / battlefield state，证明本批走 return-to-hand 公开对象移除边界。
- Guard route：base unit、stale object、face-down standby object、battlefield equipment、battlefield spell object、battlefield rune object 均在 `PLAY_CARD` 阶段返回 `INVALID_TARGET`，不推进 tick，不写事件，不支付费用，不移动源手牌，不创建 stack item，不回手目标。
- Hidden-info route：face-down standby target 被拒绝，且本批不暴露其真实 card identity；opponent hidden info 继续通过既有 viewer-specific snapshot / redaction 保护。
- Protocol boundary：本批无新增 protocol / frontend shape，只复用现有 play-card / stack / return-to-hand evidence。
- Boundary：本批不覆盖 swift / reaction timing、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / control-zone matrix、full target selection prompt、PaymentEngine、Spellshield target tax、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates、1009/811 full-official 或正式 18-step E2E。

## 验证

- Focused backend：A 记录 focused 通过 58/58。
- Adjacent guard：A 记录 adjacent guard 通过 24/24。
- Backend full：A 记录 backend full 通过 3471/3471。
- Frontend build：A 记录 frontend build passed。
- Chrome smoke：A 记录 Chrome smoke passed。
- Tests added in `ReprimandReturnToHandGuardTests`：`ReprimandReturnsPublicBattlefieldUnitToOwnerHand`、`ReprimandRejectsInvalidTargetsWithoutMutation`。

## 仍未关闭

- 完整 swift / reaction timing、spell-duel breadth、priority window 与 FEPR 全矩阵。
- 完整 return-to-hand / movement / control-zone lifecycle、owner/controller split、attached-equipment replacement、replacement / prevention / cleanup 交织。
- 完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- UI/DTO 解释字段、event label / replay redaction、targeting UX 与 return-to-hand UX。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
