# 阶段 4C-29 Gust Return To Hand Guard 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Gust / 罡风 `OGN·169/298`、`FU-48662b7661` 的代表性 return public battlefield unit with power <= 3 to owner hand 与 target guard hardening 证据。4C-29 不宣称 full-official，`fullOfficial=false`，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-29 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`OGN·169/298` Gust / 罡风 | 只取“让战场上一名不高于 3 战力的单位返回其所属的手牌”作为代表路径。 |
| Spell / stack | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356；`JFAQ-251023` p4 | 本批使用普通手牌法术打出 -> stack -> priority pass -> resolve 代表路径；不关闭完整反应时机。 |
| Target validity | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p20 rules 164-167；p39-p42 rules 355-356 | power > 3、非公共战场单位、装备、stale object、face-down standby 均拒绝并 no mutation。 |
| Return-to-hand / zones | `CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356 | 目标从 battlefield 返回 owner hand，并移除公开场上对象状态。 |

## 实现证据

- Representative route：P1 打出 Gust，目标为 P2 正面公共战场单位且 power = 3；双方 pass priority 后，服务端记录 `UNIT_RETURNED_TO_HAND`，目标进入 P2 owner hand。
- State boundary：目标回手后不再保留在 public `CardObjects` / battlefield state，证明本批走 return-to-hand 公开对象移除边界。
- Guard route：power > 3、base unit、stale object、face-down standby object、battlefield equipment 均在 `PLAY_CARD` 阶段返回 `INVALID_TARGET`，不推进 tick，不写事件，不支付费用，不移动源手牌，不创建 stack item，不回手目标。
- Protocol boundary：本批无新增 protocol / frontend shape，只复用现有 play-card / stack / return-to-hand evidence。
- Boundary：本批不覆盖 swift / reaction timing、完整 spell-duel lifecycle、完整 return-to-hand / movement / control-zone matrix、full target selection prompt、PaymentEngine、Spellshield target tax、FAQ regression、Hostile Takeover / Berserk Impulse / Edge of Night / Karthus / Aphelios design gates、1009/811 full-official 或正式 18-step E2E。

## 验证

- Focused backend：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Gust|FullyQualifiedName~ReturnToHand|FullyQualifiedName~Return|FullyQualifiedName~Hand"` 通过 112/112。
- Small combined regression：GustReturnToHandTests + BattleOrFlight + existing Gust rejection 通过 13/13。
- Tests added in `GustReturnToHandTests`：`GustReturnsPublicSmallBattlefieldUnitToOwnerHand`、`GustRejectsInvalidTargetsWithoutMutation`。
- Backend full：`source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore` 通过 3458/3458。
- Frontend build：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run build` 通过；仅保留既有 SignalR / Rollup PURE annotation warning。
- Chrome smoke：`cd src/Riftbound.DevUi && source ../../scripts/dev-env.sh && npm run smoke:chrome -- --start-api` 通过；覆盖 `/`、`/lobby`、`/decks`、`/cards`、`/rooms/stage3-smoke`、`/matches/stage3-smoke`、`/matches/stage3-smoke/result`。

## 仍未关闭

- 完整 swift / reaction timing、spell duel / battle lifecycle、priority window 与 FEPR 全矩阵。
- 完整 return-to-hand / movement / control-zone lifecycle、owner/controller split、attached equipment、replacement / prevention / cleanup 交织。
- 完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- Hostile Takeover control lifecycle、Berserk Impulse hidden-zone reveal / choose / recycle、Edge of Night face-down standby attach、Karthus extra Last Breath、Aphelios weapon-attachment three-mode design gates。
- UI/DTO 解释字段、event label / replay redaction、targeting UX 与 return-to-hand UX。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
