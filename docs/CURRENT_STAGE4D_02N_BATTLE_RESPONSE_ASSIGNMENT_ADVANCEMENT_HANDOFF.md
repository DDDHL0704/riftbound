# Stage 4D-02N Battle Response Assignment Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02M 后继续收窄 P0-004 battle lifecycle 的下一批服务端实现交接。它只锁定 natural battle response priority -> `ASSIGN_COMBAT_DAMAGE` -> next contested battlefield task advancement 的组合护栏，不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

现有 focused evidence 已经分别覆盖了三个相邻能力：

- 4D-02D：同一 natural active `START_BATTLE` 中，合法 Shadow battle response 会先打开 response priority，双方 pass 后再进入 `ASSIGN_COMBAT_DAMAGE` prompt。
- 4D-02E：直接进入 `ASSIGN_COMBAT_DAMAGE` 的 natural battle 在关闭当前战斗后，会推进下一处 contested battlefield 到 `SPELL_DUEL_TASKS`。
- 4D-02M：post-battle trigger payment 打开期间阻止 next contested battlefield advancement，并在 payment window 接受或拒绝关闭后恢复 advancement。

仍缺一条 cross-product guard：当当前 battle 先经历 battle response priority，再进入 assignment window，最终提交 `ASSIGN_COMBAT_DAMAGE` 后，服务端是否仍会推进下一处 contested battlefield。该路径穿过 `ResolveBattleResponsePriorityPassed(...)`、保留的 declaration context、`ResolveDeclareBattle(..., openBattleResponsePriority: false)` 与 `CommitCombatDamageAssignments(...)`，是 P0-004 battle lifecycle task progression 的自然组合风险点。

## 2. Target Behavior

最小代表流程：

1. `BF-A` 与 `BF-B` 同时 contested；`BF-A` 已完成 spell duel，当前 active task 是 `task:start-battle:BF-A`。
2. P1 对 `BF-A` 提交 minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT`，防守侧存在合法 Shadow battle-response source。
3. 服务端先打开 `BATTLE_RESPONSE_PRIORITY_OPENED`：
   - 不得同时打开 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`；
   - 不得提前推进 `BF-B` 的 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`。
4. P2 / P1 依次 `PASS_PRIORITY` 后：
   - 输出 `BATTLE_RESPONSE_PRIORITY_CLOSED`；
   - 打开 `ASSIGN_COMBAT_DAMAGE` prompt；
   - 仍保持当前 active task 指向 `task:start-battle:BF-A`；
   - 仍不得提前推进 `BF-B`。
5. P1 提交合法 `ASSIGN_COMBAT_DAMAGE` 后：
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

- Prefer a failing guard first; this is likely test-only if the shared `CommitCombatDamageAssignments(...)` advancement hook already handles the resumed response path.
- Reuse `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId])` or a similarly narrow fixture.
- Keep the existing `NaturalBattleResponsePassThenOpensAssignCombatDamageForAssignmentOrderingBattle` readable. Add a dedicated test if extending it would make the assertions too dense.
- Assert no early advancement during both the response window and the assignment window.
- Assert event ordering within each resolution result:
  - response close result: `BATTLE_RESPONSE_PRIORITY_CLOSED` before `BATTLE_DAMAGE_ASSIGNMENT_OPENED`;
  - assignment result: `BATTLE_CLOSED` / `BATTLEFIELD_CONTROL_RESOLVED` before `BF-B` `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`.
- Do not duplicate task selection logic; if runtime is needed, route through the existing shared `AdvancePendingBattlefieldTasksAfterStateChange(...)` blocker semantics.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests"
```

Focused acceptance should include:

- response priority open blocks `BF-B` advancement while `BF-A` response is unresolved;
- response pass opens `ASSIGN_COMBAT_DAMAGE` for `BF-A` without advancing `BF-B`;
- legal assignment closes `BF-A` and advances `BF-B` to `SPELL_DUEL_TASKS`;
- resulting prompt is `SpellDuelFocus` for `BF-B`, not stale `AssignCombatDamage` / `BattleDeclaration` for `BF-A`.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST"
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
