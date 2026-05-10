# Stage 4C-35 Vengeance Destroy Target Guard Evidence

日期：2026-05-10

本文件记录阶段 4C-35 的规则证据索引。证据只支持 Vengeance / 复仇代表性 destroy target guard baseline，不支持 full-official 断言。

## Identity

| 项 | 值 |
|---|---|
| card | Vengeance / 复仇 |
| collector | `OGN·229/298` |
| cardId | `31467` |
| functional unit | `FU-07104fa58a` |
| effect kind | `VENGEANCE_DESTROY_UNIT` |
| official text scope | 摧毁一名单位 |
| stage | 4C-35 |
| fullOfficial | false |

## Rule Evidence

| 规则域 | Evidence | 4C-35 使用方式 | 仍缺 |
|---|---|---|---|
| Object / visibility | `CORE-260330` p4-p8 rules 107-129 | public unit target 与 face-down/private target guard | 完整 hidden / face-down / standby visibility matrix |
| Controller / owner / zone identity | `CORE-260330` p14-p15 rules 142-143 | 目标进入 owner graveyard，不按 controller 选择 graveyard | 完整 owner/controller split matrix |
| Spell / stack / priority | `CORE-260330` p31-p35 rules 318-340 | Vengeance 普通出牌进入 stack，双方 pass 后结算 | 完整 FEPR、reaction timing、spell duel breadth |
| Destroy / move to graveyard | `CORE-260330` p39-p42 rules 355-356 | public unit target destroyed to owner graveyard | replacement / prevention / Last Breath / attached equipment breadth |
| Event / cleanup boundary | `CORE-260330` p62-p63 rule 428 | `UNIT_DESTROYED` event and object removal are tracked | full cleanup queue and trigger timing matrix |

## Automated Evidence

| 测试 | 覆盖 |
|---|---|
| `VengeanceDestroysPublicUnitTargets` | friendly / enemy public unit targets in base / battlefield can be destroyed to owner graveyard |
| `VengeanceRejectsInvalidTargetsWithoutMutation` | stale, face-down standby, equipment, spell, rune, and private hand unit targets return `INVALID_TARGET` without mutation or leak |

Validation results：

- Focused backend：107/107。
- Adjacent guard regression：23/23。
- Backend full：3506/3506。
- Frontend build：passed。
- Chrome smoke：passed。

## Coverage Boundary

本批只给以下项补证据：

- Vengeance representative public unit destroy target guard。
- Invalid target no-mutation/no-leak guard。
- Hidden face-down/private invalid target rejection stance。

本批不关闭：

- full Vengeance official。
- all destroy-target cards。
- full replacement / prevention / cleanup / Last Breath interaction。
- attached-equipment detach / replacement breadth。
- destroyed-this-turn memory breadth。
- Spellshield target tax / target invalidation。
- 1009/811 full-official。
- formal 18-step E2E。

最终口径：**NOT READY；不宣称 READY-CANDIDATE。**
