# Stage 4D-02M Post-Payment Battle Task Advancement Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02L 后继续收窄 P0-004 battle lifecycle 的下一批服务端实现交接。它只锁定 post-battle `PendingPayment` 关闭后恢复 pending battlefield task advancement，不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-02L 已让 immediate `DECLARE_BATTLE` 直接结算分支在 battle close / control resolution 后调用 shared `AdvancePendingBattlefieldTasksAfterStateChange(...)`，并新增 `PendingPayment` blocker，避免征服 / 据守 / 触发支付窗口仍打开时提前推进下一处 contested battlefield。

只读检查发现多个支付关闭路径会把 `PendingPayment` 清空后直接返回，例如：

- `ResolveBattlefieldConquerGoldTriggerPayment(...)`
- `ResolveBattlefieldConquerPowerfulDrawTriggerPayment(...)`
- `ResolveOgnVayneConquerRecallTriggerPayment(...)`
- `ResolveIcevaleArcherAttackTriggerPayment(...)`
- `ResolveJaxWeaponAttachTriggerPayment(...)`
- `ResolveSfdFioraPowerfulReadyTriggerPayment(...)`
- `ResolveTriggerPaymentDecline(...)`
- 普通 `ResolvePayCost(...)`

这些路径现在没有统一的 post-payment battlefield task advancement hook。结果是：战斗结算先正确被 `PendingPayment` blocker 挡住，但支付窗口接受或拒绝后，已存在的 next contested battlefield 可能仍停在 `BATTLEFIELD_TASKS` / `WAIT`，无法自然进入 `SPELL_DUEL_TASKS`。

## 2. Target Behavior

最小代表流程：

1. `BF-A` 已完成 spell duel，当前 pending queue active task 是 `task:start-battle:BF-A`。
2. P1 在 `BF-A` 提交 immediate `DECLARE_BATTLE`，战斗结束并打开 post-battle trigger payment，例如 `BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD`。
3. 支付窗口打开期间：
   - `PendingPayment` 仍是服务端唯一权威；
   - prompt 只允许当前支付玩家 `PAY_COST`；
   - 不得提前输出 next `BF-B` 的 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`。
4. P1 接受支付后：
   - 支付、触发效果和 `PAYMENT_WINDOW_CLOSED` 正常审计；
   - 若无 terminal status、cleanup task、stack、active battle、active spell duel 或新 payment blocker，服务端继续推进 `BF-B`；
   - `BF-B` 进入 `SPELL_DUEL_TASKS`，active task 是 `task:start-spell-duel:BF-B`，prompt 为 `SpellDuelFocus`。
5. P1 拒绝支付后也应同样关闭 blocker 并推进 `BF-B`，但不得产生成本支付或可选触发效果。
6. 支付失败、非法 choice、错误玩家、stale payment id 等 rejected path 必须 no-mutation，并且不得推进任何 battlefield task。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- 或 `tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

禁止写入：

- `src/Riftbound.Engine/MatchSession.cs`，除非发现 prompt/snapshot contract gap。
- PaymentEngine broad rewrite 或 LayerEngine。
- frontend。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。

## 4. Implementation Notes

- Prefer a failing guard first. The strongest representative is a `START_BATTLE:BF-A` immediate conquest payment plus an already-contested `BF-B`.
- Reuse the shared `AdvancePendingBattlefieldTasksAfterStateChange(...)`; do not duplicate task selection or prompt construction logic.
- Add the post-payment hook only after an accepted payment or accepted decline has produced the correct post-payment `nextState` and events.
- Keep blocker semantics centralized in `AdvancePendingBattlefieldTasksAfterStateChange(...)`: status, phase, timing, stack, spell duel, active battle, pending payment and cleanup tasks must continue to gate advancement.
- Keep event order stable: payment / trigger / `PAYMENT_WINDOW_CLOSED` should remain before the subsequent `BF-B` `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` events.
- Do not advance on rejected payment attempts; the rejected state hash should match the pre-submit payment state.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests"
```

Focused acceptance should include:

- Post-battle trigger payment open blocks next contested battlefield advancement while `PendingPayment` is non-null.
- Accepted trigger payment closes the window and then advances the next contested battlefield.
- Declined trigger payment closes the window and then advances the next contested battlefield without cost/effect events.
- Rejected payment attempt keeps the payment window open, preserves the state hash and emits no next battlefield events.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 7. No-Go

- Do not rewrite combat damage assignment.
- Do not broaden PaymentEngine support beyond the post-payment advancement hook needed by this slice.
- Do not modify frontend task UI.
- Do not update card coverage matrix.
- Do not close P0-002, P0-003, P0-004, P0-005, P1 or READY.
- Do not mark active goal complete.
