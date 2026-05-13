# Stage 4D-02 Spell Duel And Battle Baseline Evidence

日期：2026-05-13
结论：**BASELINE GREEN / PROJECT NOT READY**

本文记录 Stage 4D-02 实现前的 spell duel / battle 状态机基线。它只说明当前 HEAD 的既有代表路径测试为绿色，不关闭 P0-004，不升级 full official，不改变项目 **NOT READY** 结论。

## 1. Scope

4D-02 目标来自 `docs/CURRENT_STAGE4D_P0_P1_CLOSURE_PLAN.md`：把法术对决和战斗从 direct/minimal representative resolver 推进到由 task queue 创建、推进和关闭的官方状态机。

当前已存在基础能力：

- `SpellDuelState` / `BattleState` / `BattlefieldTaskState` snapshot。
- `START_SPELL_DUEL` / `START_BATTLE` pending task queue 代表路径。
- 法术对决栈清空后回到 `SPELL_DUEL_OPEN` 的焦点恢复。
- 双方让过焦点后把 matching spell-duel task 标记完成并进入 `BATTLE_TASKS`。
- active `START_BATTLE` prompt / command guard。
- 代表性 `DECLARE_BATTLE`、`ASSIGN_COMBAT_DAMAGE`、battle cleanup、battlefield control resolution。

当前仍未证明：

- 多个争夺战场的任务串联和 deterministic one-active-task ordering。
- 错误焦点 / 错误时机 `PASS_FOCUS` 的 no-mutation guard。
- swift/reaction 栈项目与 active spell-duel task 的稳定绑定。
- reconnect during `SPELL_DUEL_TASKS` / `BATTLE_TASKS` / damage assignment 的完整状态与 redaction。
- battle id / battlefield id / participant metadata across stale prompt、wrong actor 和 wrong participant 的全链路 no-mutation。
- 完整官方 initial stack、battle response window、无战斗结果、battle cleanup task lifecycle。

## 2. Baseline Commands

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~SpellDuelPassCloseEntersDamageAssignmentThenBattleCleanupUpdatesControl|FullyQualifiedName~AssignCombatDamageRuntime|FullyQualifiedName~AssignCombatDamagePrompt|FullyQualifiedName~PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses|FullyQualifiedName~CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus|FullyQualifiedName~CoreRuleEngineSkipsStartBattleWhenSpellDuelCleanupRemovesParticipant|FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineRejectsNonActivePlayerDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineRejectsDeclareBattleThatDoesNotMatchActiveStartBattleTask|FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass"
```

Result: passed, 29/29.

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~BattleState"
```

Result: passed, 121/121.

Backend full baseline inherited from accepted 4D-01 evidence:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result at 4D-01 acceptance: passed, 3780/3780.

## 3. A Interpretation

- The current representative paths are green and can be used as regression guardrails for 4D-02.
- The green baseline does not prove P0-004 complete because most risk is missing lifecycle breadth rather than currently failing representative tests.
- 4D-02 must add new tests that fail on today’s gaps before changing implementation, especially around multi-contest sequencing, wrong-focus no-mutation and reconnect redaction.
- Any implementation that only preserves current baseline without expanding lifecycle coverage is not acceptable.

## 4. Next Step

Use `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_HANDOFF.md` as Maxwell / B service-side implementation handoff. A will accept the slice only after reviewing diff, focused / adjacent / backend full output and updated audit docs.
