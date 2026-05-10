# Stage 4C-34 Isolate Move Guard Evidence

日期：2026-05-10

证据对象：Isolate / 隔绝 `UNL-124/219` / cardId `34667` / `FU-175d573ae4` / `ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW`。

## 规则 / 数据依据

- `CATALOG` `UNL-124/219`：卡面要求将一名敌方单位从战场移动到其基地；随后若该战场上有落单的敌方单位则抽一张牌。
- `CORE-260330` p4-p8 rules 107-129：卡牌、对象、拥有者 / 控制者基础。
- `CORE-260330` p14-p15 rules 142-143：公开区域与对象状态。
- `CORE-260330` p31-p35 rules 318-340：出牌、目标与结算链代表路径。
- `CORE-260330` p39-p42 rules 355-356：结算与移动后状态同步代表路径。

## 自动化证据

新增测试：`tests/Riftbound.ConformanceTests/IsolateMoveToBaseGuardTests.cs`

- `IsolateMovesPublicEnemyBattlefieldUnitToOwnerBaseWithoutDrawing`
- `IsolateRejectsInvalidTargetsWithoutMutation`

验证结果：

- Focused backend：46/46 passed。
- Adjacent guard regression：48/48 passed。
- Backend full：3495/3495 passed。
- Frontend build：passed。
- Chrome smoke：passed。

## 代表路径

合法路径：

1. P1 支付 Isolate 基础费用并用 `PLAY_CARD` 指定 `P2-BATTLEFIELD-UNIT`。
2. 双方 priority pass。
3. 目标从 P2 battlefield 移到 P2 base。
4. 目标保留 damage = 2、power = 4、exhausted = true。
5. 事件包含 `UNIT_MOVED_TO_BASE`，`ownerPlayerId = P2`。
6. 事件不包含 `CARD_DRAWN`。

非法目标：

- `P1-FRIENDLY-BATTLEFIELD-UNIT`
- `P2-BASE-UNIT`
- `P2-STALE-UNIT`
- `P2-FACE-DOWN-STANDBY`
- `P2-BATTLEFIELD-EQUIPMENT`
- `P2-BATTLEFIELD-SPELL`
- `P2-BATTLEFIELD-RUNE`

非法路径共同断言：

- `INVALID_TARGET`
- no events
- tick 不推进
- no pending payment
- rune pool 不变
- hand 不变
- battlefield / base zones 不变
- stack 为空
- no `UNIT_MOVED_TO_BASE`
- no `EQUIPMENT_MOVED_TO_BASE`
- no `CARD_DRAWN`

## 隐藏信息边界

face-down standby object 被作为 invalid target 拒绝；测试只断言 no mutation / no event / no draw，不需要也不允许暴露对手面朝下待命真实信息。该路径继续依赖 viewer-specific snapshot / redaction 作为正式对手视角保护。

## 不可外推

本证据不关闭：

- Isolate 落单敌方单位抽牌分支。
- 完整移动 / 游走 / 精确战场目的地模型。
- 完整 FEPR target selection、target invalidation、Spellshield target tax。
- 完整 hidden / face-down / standby target matrix。
- replacement / prevention / cleanup 与移动交织。
- 1009 / 811 full-official 覆盖。
- 正式 18-step E2E。
