# Stage 4C-36 Hostile Takeover Control Ready Guard Evidence

日期：2026-05-10

本文件记录阶段 4C-36 的规则证据索引。证据只支持 Hostile Takeover / 恶意收购代表性 control-ready target guard baseline，不支持 full-official 断言。

## Identity

| 项 | 值 |
|---|---|
| card | Hostile Takeover / 恶意收购 |
| collector | `SFD·202/221` |
| cardId | `33301` |
| functional unit | `FU-00ee09c2cc` |
| effect kind | `HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT` |
| official text scope | 获得战场上一名敌方单位的控制权；让其变为活跃状态；回合结束时失去控制权并召回 |
| stage | 4C-36 |
| fullOfficial | false |

## Rule Evidence

| 规则域 | Evidence | 4C-36 使用方式 | 仍缺 |
|---|---|---|---|
| Object / visibility | `CORE-260330` p4-p8 rules 107-129 | enemy public battlefield unit target 与 face-down/private target guard | 完整 hidden / face-down / standby visibility matrix |
| Controller / owner / battlefield identity | `CORE-260330` p14-p15 rules 142-143；p22-p26 rules 179, 187-189 | owner remains P2，controller becomes P1，object remains battlefield | 完整 owner/controller split and control-zone lifecycle matrix |
| Spell / stack / priority | `CORE-260330` p31-p35 rules 318-340 | Hostile Takeover 普通出牌进入 stack，双方 pass 后结算 | 完整 FEPR、reaction timing、spell duel breadth |
| Play / resolution | `CORE-260330` p39-p42 rules 355-356 | gain control + ready resolves from stack after legal target validation | full target invalidation and spell interaction matrix |
| End-turn control return | `CORE-260330` p29-p31 rules 316.1-317.3；`SOUL-OFAQ-260114` p21 | representative P5 fixture covers return control and recall to owner base | full cleanup task model and control replacement breadth |
| FAQ control exception | `SOUL-OFAQ-260114` p21；`SOUL-JFAQ-260114` p22 | evidence anchor for Hostile Takeover control-specific behavior | full FAQ regression and non-combat spell duel breadth |

## Automated Evidence

| 测试 / Fixture | 覆盖 |
|---|---|
| `HostileTakeoverGainsControlReadiesEnemyBattlefieldUnitAndSchedulesReturn` | enemy public battlefield unit is controlled by P1, readied, owner remains P2, and return marker is scheduled |
| `HostileTakeoverRejectsInvalidTargetsWithoutMutation` | friendly battlefield, enemy base, stale, face-down standby, equipment, spell, rune, and hand/private targets return `INVALID_TARGET` without mutation or leak |
| `p2-preflight-play-hostile-takeover-gain-control-ready-battlefield-unit` | representative stack resolution for gain control + ready and return marker |
| `p5-hostile-takeover-end-turn-return-recall` | representative end-turn return control + recall to owner base; not full official |

Validation results：

- Focused backend：265/265。
- Adjacent guard regression：157/157。
- Backend full：3515/3515。
- Frontend build：passed。
- Chrome smoke：passed。

## Coverage Boundary

本批只给以下项补证据：

- Hostile Takeover representative enemy public battlefield unit gain-control + ready target guard。
- Invalid target no-mutation/no-leak guard。
- Owner/controller split and return-marker scheduling representative stance。
- Hidden face-down/private invalid target rejection stance。

本批不关闭：

- full Hostile Takeover official。
- standby / reaction timing。
- battle-start / conquer branch。
- full battlefield / control-zone lifecycle。
- full owner/controller matrix。
- full end-turn cleanup task model。
- Spellshield target tax / target invalidation。
- PaymentEngine / FEPR full matrix。
- full FAQ regression。
- 1009/811 full-official。
- formal 18-step E2E。

最终口径：**NOT READY；`fullOfficial=false`；不宣称 READY-CANDIDATE。**
