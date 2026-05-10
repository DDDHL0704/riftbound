# 阶段 4C-30 Hunt the Weak Destroy Guard 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Hunt the Weak / 狩魂 `UNL-159/219`、`FU-282b6e3149` 的代表性 destroy public battlefield unit with power <= 3 to owner graveyard 与 target guard hardening 证据。4C-30 不宣称 full-official，`fullOfficial=false`，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-30 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`UNL-159/219` Hunt the Weak / 狩魂 | 只取“摧毁战场上一名不高于 3 战力的单位”作为代表路径。 |
| Spell / stack | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356；`JFAQ-251023` p4 | 本批使用普通手牌法术打出 -> stack -> priority pass -> resolve 代表路径；不关闭完整反应时机。 |
| Target validity | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p20 rules 164-167；p39-p42 rules 355-356；p62-p63 rule 428 | power > 3、非公共战场单位、装备、stale object、face-down standby 均拒绝并 no mutation。 |
| Hidden info | viewer-specific snapshot / redaction policy；`CORE-260330` p4-p8 rules 107-129 | face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续受保护。 |
| Destroy / zones | `CORE-260330` p62-p63 rule 428 | 目标从 battlefield 摧毁到 owner graveyard，并移除公开场上对象状态。 |

## 实现证据

- Representative route：P1 打出 Hunt the Weak，目标为 P2 正面公共战场单位且 power = 3；双方 pass priority 后，服务端记录 `UNIT_DESTROYED`，目标进入 P2 owner graveyard。
- State boundary：目标摧毁后不再保留在 public `CardObjects` / battlefield state，证明本批走 destroy-to-graveyard 公开对象移除边界。
- Guard route：power > 3、base unit、stale object、face-down standby object、battlefield equipment 均在 `PLAY_CARD` 阶段返回 `INVALID_TARGET`，不推进 tick，不写事件，不支付费用，不移动源手牌，不创建 stack item，不摧毁目标。
- Hidden-info route：face-down standby target 被拒绝，且本批不暴露其真实 card identity；opponent hidden info 继续通过既有 viewer-specific snapshot / redaction 保护。
- Protocol boundary：本批无新增 protocol / frontend shape，只复用现有 play-card / stack / destroy evidence。
- Boundary：本批不覆盖 swift / reaction timing、完整 spell-duel lifecycle、完整 destroy / cleanup / Last Breath trigger interactions、full target selection prompt、PaymentEngine、Spellshield target tax、replacement / prevention / cleanup / full targeting matrix、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates、1009/811 full-official 或正式 18-step E2E。

## 验证

- Focused backend：A 记录 Hunt the Weak focused 通过 34/34。
- Adjacent regression：A 记录 adjacent 通过 19/19。
- Tests added in `HuntTheWeakDestroyGuardTests`：`HuntTheWeakDestroysPublicSmallBattlefieldUnit`、`HuntTheWeakRejectsInvalidTargetsWithoutMutation`。
- Backend full：A 记录 `dotnet test Riftbound.slnx --no-restore` 通过 3464/3464。
- Frontend build：A 记录 `npm run build` 通过。
- Chrome smoke：A 记录 `npm run smoke:chrome -- --start-api` 通过。

## 仍未关闭

- 完整 swift / reaction timing、spell duel / battle lifecycle、priority window 与 FEPR 全矩阵。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- 完整 destroy / cleanup / Last Breath trigger interactions、state-based cleanup 与 simultaneous destruction full-official matrix。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- Hunt the Weak 相关 replacement / prevention / cleanup / full targeting matrix、target prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- UI/DTO 解释字段、event label / replay redaction、targeting UX 与 destroy UX。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
