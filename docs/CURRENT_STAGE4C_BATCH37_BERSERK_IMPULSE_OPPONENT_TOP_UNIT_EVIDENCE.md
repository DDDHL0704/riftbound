# Stage 4C-37 Berserk Impulse Opponent Top Unit Guard Evidence

日期：2026-05-10

本文件记录阶段 4C-37 的规则证据索引。证据只支持 Berserk Impulse / 暴怒冲动 representative opponent top main-deck unit target guard baseline，不支持 full-official 断言。

## Identity

| 项 | 值 |
|---|---|
| card | Berserk Impulse / 暴怒冲动 |
| collector | `OGN·025/298` |
| cardId | `31231` |
| functional unit | `FU-b05eda44ce` |
| effect kind | `BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT` |
| official text scope | 迅捷；每名对手展示其主牌堆顶部一张牌，选择一张当作自己的牌打出并无视费用，然后回收其余卡牌 |
| stage | 4C-37 |
| fullOfficial | false |

## Rule Evidence

| 规则域 | Evidence | 4C-37 使用方式 | 仍缺 |
|---|---|---|---|
| Object / visibility | `CORE-260330` p4-p8 rules 107-129 | representative revealed opponent top main-deck target and face-down/private invalid target guard | 完整 hidden-zone reveal / choose / redaction matrix |
| Controller / owner / zone identity | `CORE-260330` p14-p15 rules 142-143 | owner/source/play-by event semantics and source zone movement from opponent main deck to P1 base | 完整 “play as your own card” owner/controller/payment matrix |
| Spell / stack / priority | `CORE-260330` p31-p35 rules 318-340 | Berserk Impulse 普通出牌进入 stack，双方 pass 后结算 | 完整 FEPR、reaction timing、spell duel breadth |
| Play / resolution | `CORE-260330` p39-p42 rules 355-356 | target top unit is played to P1 base after legal target validation | full target invalidation and free-play branch interaction matrix |
| FAQ / hidden-zone branch | `SOUL-OFAQ-260114` p4 | evidence anchor for Berserk Impulse hidden-zone / top-card behavior | full FAQ regression、multi-opponent reveal / choose / recycle |

## Automated Evidence

| 测试 / Fixture | 覆盖 |
|---|---|
| `BerserkImpulsePlaysOpponentTopMainDeckUnitToControllerBaseAndResetsState` | legal opponent top main-deck unit is played to P1 base; owner/source/play-by event semantics; damage / UET / exhausted reset |
| `BerserkImpulseRejectsInvalidTargetsWithoutMutation` | friendly top, opponent second, top spell/equipment/rune, face-down top unit, private hand/base/battlefield targets reject with no mutation or leak |
| `BerserkImpulseDirtyResolutionDoesNotMoveInvalidTopDeckTarget` | top changed, non-unit, face-down, or wrong-controller dirty stack targets do not move and do not emit `UNIT_PLAYED_TO_BASE` |
| `p2-preflight-play-berserk-impulse-play-opponent-top-unit` | legacy representative stack fixture for opponent top unit played to P1 base |

Validation results：

- Focused backend：17/17 passed。
- D did not rerun tests。
- Backend full：not recorded for this D docs pass。
- Frontend build：not recorded for this D docs pass。
- Chrome smoke：not recorded for this D docs pass。

## Coverage Boundary

本批只给以下项补证据：

- Berserk Impulse representative opponent top main-deck unit target guard。
- Invalid target no-mutation/no-leak guard。
- Dirty resolution no-move guard。
- Owner/source/play-by event semantics。
- Damage / until-end-of-turn / exhausted reset representative stance。

本批不关闭：

- full Berserk Impulse official。
- multi-opponent reveal / choose / recycle。
- full hidden-zone prompt / redaction。
- non-unit branch, if any。
- full spell duel / reaction timing。
- full PaymentEngine。
- full LayerEngine。
- full FAQ regression。
- 1009/811 full-official。
- formal 18-step E2E。

最终口径：**NOT READY；`fullOfficial=false`；不宣称 READY-CANDIDATE。**
