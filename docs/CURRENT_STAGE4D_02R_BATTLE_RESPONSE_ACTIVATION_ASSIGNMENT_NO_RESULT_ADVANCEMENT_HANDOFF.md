# Stage 4D-02R Battle Response Activation Assignment No-Result Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02Q 后继续收窄 P0-004 battle lifecycle 的下一批服务端实现交接。它只锁定 natural battle response 中真实 Shadow activation / stack resolution / return-to-response 后打开 damage assignment，合法 assignment 导致双方 battle participants 全部摧毁并产生 `BATTLE_NO_RESULT`；当前 battle close / no-result resolution / task cleanup 后，若无 blocker，应推进下一处 contested battlefield 到 `SPELL_DUEL_TASKS`。不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-02F 已证明普通 natural `ASSIGN_COMBAT_DAMAGE` 在双方全部 participants 被摧毁时会产生 `BATTLE_NO_RESULT` / `BattleResolutionState.NO_RESULT`，并清理当前 battle task。

4D-02O 已证明真实 Shadow activation / stack resolution / return-to-response 后可以进入 assignment window，且普通合法 assignment 关闭当前 battle 后会推进下一处 contested battlefield。

仍缺组合护栏：actual battle-response activation 返回 assignment 后，如果 assignment 走 no-result 分支，服务端不得停在 stale `BATTLE_TASKS` / `WAIT`，也不得在 stack-open、returned-response 或 assignment window 期间提前推进下一处 battlefield。该链路覆盖 activation-returned assignment path 与 no-result close path 的交叉点。

## 2. Target Behavior

最小代表流程：

1. `BF-A` 与 `BF-B` 同时 contested；`BF-A` 已完成 spell duel，当前 active task 是 `task:start-battle:BF-A`。
2. P1 对 `BF-A` 提交 multi-attacker / multi-defender `DECLARE_BATTLE + COMBAT_ASSIGNMENT`，防守侧同战场存在合法 Shadow battle-response source。
3. P2 在 battle response priority 中激活 Shadow：
   - 输出 `ABILITY_ACTIVATED` / `UNIT_EXHAUSTED` / `COST_PAID` / `STACK_ITEM_ADDED`；
   - stack 未结算期间不得推进 `BF-B`。
4. P2 / P1 pass stack priority 后：
   - stack resolved 并回到 battle response priority；
   - 仍不得推进 `BF-B`。
5. P2 / P1 final response pass 后：
   - 关闭 battle response；
   - 打开 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`；
   - assignment window 打开期间不得推进 `BF-B`。
6. P1 提交合法 no-result `ASSIGN_COMBAT_DAMAGE`：
   - 输出 `BATTLE_NO_RESULT`，reason 为 `ALL_PARTICIPANTS_DESTROYED`；
   - `BattleResolutions` 记录 `Kind == "NO_RESULT"`；
   - 当前 battle close，当前 `START_BATTLE` task 被清理；
   - 无 blocker 时推进 `BF-B` 到 `SPELL_DUEL_TASKS` / `SpellDuelFocus`。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

禁止写入：

- `src/Riftbound.Engine/MatchSession.cs`，除非发现 prompt / snapshot contract gap。
- PaymentEngine broad rewrite、LayerEngine。
- frontend。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- `riftbound-dotnet.sln`。

## 4. Implementation Notes

- Prefer a failing guard first. This is likely test-only if the no-result close branch already reuses pending battlefield task advancement.
- Suggested fixture:
  - start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, includeSecondAttacker: true, defenderObjectIds: [BulwarkDefenderObjectId, BackRowDefenderObjectId])`;
  - apply the same power mutations as `BuildNoResultNaturalStartBattleState`;
  - keep Shadow on the same battlefield as a legal response source but not a declared defender, so exhausting Shadow does not become a battle participant;
  - use `NoResultAssignments()` for the final assignment.
- Reuse helpers where possible:
  - `AssertNextContestedBattlefieldNotAdvanced(...)`
  - `EventIndex(...)`
  - `NoResultAssignments()`
  - existing activation assignment path from `NaturalBattleResponseActivationAssignmentAdvancesNextContestedBattlefieldTask`.
- Suggested test name:
  - `NaturalBattleResponseActivationAssignmentNoResultAdvancesNextContestedBattlefieldTask`
- Assert event order:
  - response close before assignment opened;
  - assignment accepted emits `BATTLE_NO_RESULT`;
  - `BATTLE_NO_RESULT` before `BATTLE_CLOSED`;
  - `BATTLE_CLOSED` before `BF-B` `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Focused acceptance should include:

- activation / stack-open state blocks `BF-B` advancement;
- stack resolution returns to battle response and still blocks `BF-B` advancement;
- final response pass opens assignment and still blocks `BF-B`;
- no-result assignment closes the current battle, records `NO_RESULT`, removes current battle participants, clears the current `START_BATTLE` task, and advances `BF-B` to `SPELL_DUEL_TASKS`.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

## 7. No-Go

- Do not rewrite combat damage assignment.
- Do not broaden Shadow, swift, reaction or PaymentEngine behavior beyond what this guard reveals.
- Do not modify frontend task UI.
- Do not update card coverage matrix.
- Do not close P0-002, P0-003, P0-004, P0-005, P1 or READY.
- Do not mark active goal complete.
