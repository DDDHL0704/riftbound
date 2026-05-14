# Stage 4D-02O Battle Response Activation Assignment Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02N 后继续收窄 P0-004 battle lifecycle 的下一批服务端实现交接。它只锁定 natural battle response 中真实 Shadow activation / stack resolution / return-to-response -> `ASSIGN_COMBAT_DAMAGE` -> next contested battlefield task advancement 的组合护栏，不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-02N 已证明 pass-only battle response path 可以进入 assignment window，并在 legal assignment 后推进下一处 contested battlefield。

仍缺一条更深的组合 guard：当前 battle response priority 中如果防守方真的激活 Shadow swift ability，服务端会创建 stack item；双方 pass 后 stack resolution 会回到 battle response priority。该回到 response 的状态再经过双方 pass、进入 `ASSIGN_COMBAT_DAMAGE`、最终关闭 battle 时，仍应保持 next battlefield advancement 语义。

现有证据分散覆盖：

- `ShadowActivatedAbilityTests`：Shadow 可在 natural battle response window 激活、创建 stack、pass-pass 后回到 battle response 并最终关闭 battle。
- 4D-02K：Shadow activation / stack resolution 可保留 Brush replacement context，并在 final response pass 后进入 held-score path。
- 4D-02N：pass-only response -> assignment -> next contested advancement。

尚未有 focused guard 覆盖 actual activation / stack-return 后再进入 assignment 并推进下一处 contested battlefield。

## 2. Target Behavior

最小代表流程：

1. `BF-A` 与 `BF-B` 同时 contested；`BF-A` 已完成 spell duel，当前 active task 是 `task:start-battle:BF-A`。
2. P1 对 `BF-A` 提交 minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT`，防守侧存在合法 Shadow battle-response source。
3. P2 在 battle response priority 中激活 Shadow：
   - 输出 `ABILITY_ACTIVATED` / `UNIT_EXHAUSTED` / `COST_PAID` / `STACK_ITEM_ADDED`；
   - stack 未结算期间不得推进 `BF-B`。
4. P2 / P1 依次 pass stack priority 后：
   - 输出 `STACK_ITEM_RESOLVED` / `ABILITY_RESOLVED` / `STATUS_EFFECT_APPLIED`；
   - 回到 battle response priority；
   - 仍不得推进 `BF-B`。
5. P2 / P1 再次 pass battle response priority 后：
   - 输出 `BATTLE_RESPONSE_PRIORITY_CLOSED`；
   - 打开 `ASSIGN_COMBAT_DAMAGE` prompt；
   - 仍保持当前 active task 指向 `task:start-battle:BF-A`；
   - 仍不得推进 `BF-B`。
6. P1 提交合法 `ASSIGN_COMBAT_DAMAGE` 后：
   - 当前 battle damage / cleanup / battle close / battlefield control 正常审计；
   - matching `START_BATTLE:BF-A` 被清理；
   - 若无 terminal status、cleanup task、stack、active battle、active spell duel 或 payment blocker，服务端推进 `BF-B`；
   - `BF-B` 进入 `SPELL_DUEL_TASKS`，active task 是 `task:start-spell-duel:BF-B`，focus prompt 为 `SpellDuelFocus`。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

仅 prompt / snapshot contract gap 时允许写入：

- `src/Riftbound.Engine/MatchSession.cs`

禁止写入：

- frontend。
- PaymentEngine broad rewrite、LayerEngine、card matrix。
- unrelated conformance fixture rewrites。
- `riftbound-dotnet.sln`。

## 4. Implementation Notes

- Prefer a failing guard first; this is likely test-only if stack-return state already preserves the declaration context and `CommitCombatDamageAssignments(...)` advancement hook handles the final assignment close.
- Reuse `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId])`.
- Reuse existing local helpers where possible:
  - `ShadowResponseLegalAssignments()`
  - `AssertNextContestedBattlefieldNotAdvanced(...)`
  - `EventIndex(...)`
- Keep the existing 4D-02N pass-only test separate; add a dedicated test name such as `NaturalBattleResponseActivationAssignmentAdvancesNextContestedBattlefieldTask`.
- Assert no `BF-B` advancement while stack is open, after stack resolution returns to response, and after assignment window opens.
- Assert final event ordering:
  - stack resolution before response close;
  - response close before assignment open;
  - current battle close / control resolution before `BF-B` `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`.
- Do not duplicate task selection logic; if runtime is needed, route through the existing shared `AdvancePendingBattlefieldTasksAfterStateChange(...)` blocker semantics.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Focused acceptance should include:

- activation / stack-open state blocks `BF-B` advancement;
- stack resolution returns to battle response and still blocks `BF-B` advancement;
- final response pass opens `ASSIGN_COMBAT_DAMAGE` for `BF-A` without advancing `BF-B`;
- legal assignment closes `BF-A` and advances `BF-B` to `SPELL_DUEL_TASKS`;
- resulting prompt is `SpellDuelFocus` for `BF-B`, not stale `AssignCombatDamage` / `BattleDeclaration` for `BF-A`.

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
