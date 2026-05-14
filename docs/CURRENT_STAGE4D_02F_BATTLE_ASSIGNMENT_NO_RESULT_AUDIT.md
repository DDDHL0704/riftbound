# Stage 4D-02F Battle Assignment No-Result Audit

日期：2026-05-14
结论：**IMPLEMENTED / PROJECT NOT READY**

## Scope

本切片只补 natural battle damage assignment 分支中的 no-result lifecycle representative。

覆盖路径：

- active contested `START_BATTLE`
- minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT`
- `ASSIGN_COMBAT_DAMAGE` prompt
- legal damage assignment
- all battle participants destroyed
- `BATTLE_NO_RESULT`
- `BATTLE_CLOSED`
- matching `START_BATTLE` cleanup
- `BattleResolutionState.Kind = NO_RESULT`

## Runtime Note

Focused test 暴露了一个真实 audit gap：当所有参战者在 state-based cleanup 中被移出 `CardObjects` 后，`CloseResolvedBattle` 之前只会在清除 `IsAttacking` / `IsDefending` 标记时 emit `BATTLE_CLOSED`，全灭场景会缺少 close audit。

修复方式：

- `CloseResolvedBattle` 现在会把已从 `CardObjects` 移除的 participant 计入 `removedObjectIds`。
- 当 `clearedObjectIds` 为空但 `removedObjectIds` 非空时，仍 emit `BATTLE_CLOSED`。
- 保留原 `clearedObjectIds` payload，并追加 `removedObjectIds` 以便审计全灭关闭。

## Test

新增：

- `BattleDamageAssignmentLifecycleTests.NaturalAssignCombatDamageEmitsNoResultWhenAllParticipantsDestroyed`

断言：

- declare opens `ASSIGN_COMBAT_DAMAGE`;
- legal assignment accepted;
- `BATTLE_NO_RESULT.reason == ALL_PARTICIPANTS_DESTROYED`;
- `BATTLE_CLOSED` emitted;
- no `BATTLEFIELD_HELD` / `BATTLEFIELD_CONQUERED`;
- battle inactive;
- matching `START_BATTLE` cleared;
- no stale assignment/declaration prompt;
- all participants in graveyards and removed from authoritative battlefield/card object state;
- persisted battle resolution is `NO_RESULT` with null winner and empty surviving attacker/defender lists.

## Non-Goals

- 未重写 combat。
- 未启动 LayerEngine。
- 未扩 PaymentEngine。
- 未修改前端、fixtures mass update 或 coverage matrix。
- 未关闭 P0-004 / P0-005 / P1 / READY。
