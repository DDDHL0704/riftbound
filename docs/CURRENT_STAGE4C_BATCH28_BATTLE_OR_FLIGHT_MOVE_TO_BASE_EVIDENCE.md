# 阶段 4C-28 Battle or Flight Move To Base 证据

更新时间：2026-05-10
结论：**NOT READY**

本文只记录 Battle or Flight / 战或逃 `OGN·168/298`、`FU-813144e7d4` 的代表性 move battlefield unit -> owner base 与 target guard hardening 证据。4C-28 不宣称 full-official，`fullOfficial=false`，不进入 1009 张卡全量实现，不启动最终正式 18-step E2E。

## 证据锚点

| 领域 | 证据 | 4C-28 使用方式 |
|---|---|---|
| 卡牌文本 | 2026-04-27 固定 catalog：`OGN·168/298` Battle or Flight / 战或逃 | 只取“将一名单位从战场上移动到其所属的基地”作为代表路径。 |
| Spell / stack | `CORE-260330` p31-p35 rules 318-340；p39-p42 rules 355-356；`JFAQ-251023` p4 | 本批使用普通手牌法术打出 -> stack -> priority pass -> resolve 代表路径。 |
| Movement / zones | `CORE-260330` p4-p8 rules 107-129；p39-p42 rules 355-356；`SOUL-JFAQ-260114` p12、p16 | 目标从 battlefield 移动到 owner base，并保留 object state。 |
| Target validity | `CORE-260330` p4-p8 rules 107-129；p14-p15 rules 142-143；p39-p42 rules 355-356 | 非战场单位、装备、stale object、face-down standby 均拒绝并 no mutation。 |

## 实现证据

- Representative route：P1 打出 Battle or Flight，目标为 P2 正面战场单位；双方 pass priority 后，服务端记录 `UNIT_MOVED_TO_BASE`，目标进入 P2 owner base。
- State preservation：移动后目标保留 `damage=1`、`power=5` 等对象状态，证明本批不是重新创建对象或错误重置状态。
- Guard route：battlefield equipment、base unit、stale object、face-down standby object 均在 `PLAY_CARD` 阶段返回 `INVALID_TARGET`，不推进 tick，不写事件，不支付费用，不移动源手牌，不创建 stack item，不移动目标。
- Protocol boundary：本批无新增 protocol / frontend shape，只复用现有 play-card / stack / move-to-base evidence。
- Boundary：本批不覆盖 swift spell-duel timing、standby face-down reaction play、完整 movement / control-zone matrix、full target selection prompt、PaymentEngine、Spellshield target tax、FAQ regression、1009/811 full-official 或正式 18-step E2E。

## 验证

- Focused backend：A 记录通过 61/61。
- Tests added in `BattleOrFlightMoveToBaseTests`：`BattleOrFlightMovesFaceUpBattlefieldUnitToOwnerBase`、`BattleOrFlightRejectsInvalidTargetsWithoutMutation`。

## 仍未关闭

- 完整 spell duel / battle lifecycle、swift / reaction timing、face-down standby play 与 priority window 全矩阵。
- 完整 movement / control-zone / roam lifecycle、owner/controller split、attached equipment、movement replacement / prevention / cleanup 交织。
- 完整 targeting prompt、target invalidation、hidden / face-down target policy、Spellshield target tax。
- 完整 PaymentEngine、play-card cost Quote / Authorize / Commit、替代 / 额外费用与支付资源矩阵。
- UI/DTO 解释字段、event label / replay redaction、movement UX。
- FAQ regression、1009 entries / 811 functional units full-official、正式 18-step E2E。
