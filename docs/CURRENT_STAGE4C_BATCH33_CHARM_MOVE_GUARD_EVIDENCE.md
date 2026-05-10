# 阶段 4C-33 Charm Move Guard 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Charm / 魅惑妖术 `OGN·043/298`、cardId `31255`、`FU-1586b6cdd9` / `CHARM_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE` 的代表性 move enemy public battlefield unit to owner base 与 target guard hardening 证据。4C-33 不宣称 full-official，`fullOfficial=false`，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-33 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`OGN·043/298` Charm / 魅惑妖术，cardId `31255` | 只取“移动一名敌方单位”作为代表路径；本批把目标限定为 enemy public battlefield unit。 |
| Spell / stack | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356 | 本批使用普通手牌法术打出 -> stack -> priority pass -> resolve 代表路径；不关闭完整 reaction timing 或 spell-duel breadth。 |
| Target validity | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p31-p33 rules 318-324；p39-p42 rules 355-356 | friendly battlefield unit、enemy base unit、stale unit、face-down standby、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均拒绝并 no mutation。 |
| Hidden info | viewer-specific snapshot / redaction policy；`CORE-260330` p4-p8 rules 107-129 | face-down standby target 被拒绝且不暴露真实身份；opponent hidden info 继续受保护。 |
| Movement / zones | `CORE-260330` p4-p8 rules 107-129；p31-p33 rules 318-324；p39-p42 rules 355-356 | 目标从 battlefield 移动到 owner base，并保留 damage / power / exhausted / object identity。 |

## 实现证据

- Representative route：P1 打出 Charm，目标为 P2 正面公共战场单位；双方 pass priority 后，服务端将该目标移动到 owner base。
- State boundary：目标移动后不再保留在 battlefield state，进入 owner base；damage / power / exhausted state / object identity 均按代表切片保留。
- Guard route：friendly battlefield unit、enemy base unit、stale unit、face-down standby object、enemy battlefield equipment、enemy battlefield spell object、enemy battlefield rune object 均在 `PLAY_CARD` 阶段返回 `INVALID_TARGET`，不推进 tick，不写事件，不支付费用，不移动源手牌，不创建 stack item，不移动目标，不泄漏 hidden info。
- Hidden-info route：face-down standby target 被拒绝，且本批不暴露其真实 card identity；opponent hidden info 继续通过既有 viewer-specific snapshot / redaction 保护。
- Protocol boundary：本批无新增 protocol / frontend shape，只复用现有 play-card / stack / move evidence。
- Boundary：本批不覆盖完整目的地选择、spell-duel breadth、owner/controller split、attached-equipment replacement、full movement / roam / precise battlefield / control-zone matrix、full target selection prompt、PaymentEngine、Spellshield target tax、full FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates、1009/811 full-official 或正式 18-step E2E。

## 验证

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Charm|FullyQualifiedName~MoveToBase|FullyQualifiedName~MoveGuard"` 通过 35/35。
- Adjacent guard regression：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~CharmMoveToBaseGuardTests|FullyQualifiedName~BattleOrFlightMoveToBaseTests|FullyQualifiedName~RideTheWindMoveGuardTests|FullyQualifiedName~ReprimandReturnToHandGuardTests|FullyQualifiedName~GustReturnToHandTests|FullyQualifiedName~HuntTheWeakDestroyGuardTests"` 通过 40/40。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3487/3487。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过。
- Tests added in `CharmMoveToBaseGuardTests`：`CharmMovesPublicEnemyBattlefieldUnitToOwnerBase`、`CharmRejectsInvalidTargetsWithoutMutation`。

## 仍未关闭

- 完整 reaction timing、spell-duel breadth、priority window 与 FEPR 全矩阵。
- 完整 movement / roam / precise battlefield / control-zone lifecycle、owner/controller split、attached-equipment replacement、replacement / prevention / cleanup 交织。
- 完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- UI/DTO 解释字段、event label / replay redaction、targeting UX 与 movement UX。
- full FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
