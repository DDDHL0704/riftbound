# Stage 4D-02 Spell Duel And Battle Evidence

日期：2026-05-13
结论：**FOCUSED SLICE ACCEPTED / PROJECT NOT READY**

本文记录 Stage 4D-02 第一片服务端实现证据。该证据接受 handoff focused checklist，不关闭 P0-004 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Code Changes

- `src/Riftbound.Engine/MatchSession.cs`
  - 新增 active spell duel battlefield 推导。
  - 多争夺战场时只把 deterministic 第一个未完成战场标为 active `START_SPELL_DUEL`。
  - `BattlefieldTaskState` 只给 active spell duel task 暴露 focus / stack item metadata。
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ResolvePassFocus` 在 spell duel close 后跑 cleanup。
  - 若 cleanup 让 matching `START_BATTLE` 不再存在，则继续推进下一 pending battlefield task。
- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
  - 新增 6 个 focused tests。

## 2. Covered Acceptance Points

- 多个争夺战场只允许一个 active `START_SPELL_DUEL`，并按 battlefield object id 稳定排序。
- 非 focus player 和 wrong timing `PASS_FOCUS` 拒绝且 no mutation。
- active spell-duel stack item 结算后回到同一 `START_SPELL_DUEL` task，不提前标记 spell duel completed。
- spell duel close 后 cleanup 移除当前战场参与者时，只跳过 matching `START_BATTLE`，并推进下一 contested battlefield 的 spell duel。
- reconnect during `SPELL_DUEL_TASKS` 保留 phase、active task、focus、spellDuelId、participant metadata，并保持 opponent hidden standby redaction。
- reconnect during `BATTLE_TASKS` 保留 phase、active start battle task、battleId、participant metadata，并保持 opponent hidden standby redaction。
- 既有 active start-battle guard、declare battle、assign combat damage、battle cleanup 和 control resolution regression 继续通过。

## 3. Verification

Focused new tests:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpellDuelBattleStateMachineTests"
```

Result: passed, 6/6.

Focused handoff regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~SpellDuelPassCloseEntersDamageAssignmentThenBattleCleanupUpdatesControl|FullyQualifiedName~AssignCombatDamageRuntime|FullyQualifiedName~AssignCombatDamagePrompt|FullyQualifiedName~PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses|FullyQualifiedName~CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus|FullyQualifiedName~CoreRuleEngineSkipsStartBattleWhenSpellDuelCleanupRemovesParticipant|FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineRejectsNonActivePlayerDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineRejectsDeclareBattleThatDoesNotMatchActiveStartBattleTask|FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass"
```

Result: passed, 35/35.

Adjacent regression:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~BattleState"
```

Result: passed, 127/127.

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result: passed, 3786/3786.

Whitespace:

```sh
git diff --check
```

Result: no output after implementation review.

## 4. Remaining Scope

This evidence is intentionally narrow. The following remain open:

- full official battle response window and initial-stack lifecycle;
- full swift/reaction chain breadth beyond current task binding;
- no-result battle matrix and replacement / prevention interactions;
- complete damage assignment official breadth;
- PaymentEngine unification and reaction payment windows;
- LayerEngine, keyword full-pass and full-card official matrix.
