# Stage 4D-02 Spell Duel And Battle State Machine Handoff

日期：2026-05-13
结论：**NOT READY**

本文是 Stage 4D-02 的服务端实现交接规格。它只定义 P0-004 的下一片可交付范围，不授权前端补洞、不升级卡牌矩阵、不宣称 P0-002/P0-003/P0-005 或 P1 已关闭。

前置状态：4D-01 board task queue foundation 已验收，见 `docs/CURRENT_STAGE4D_01_BOARD_TASK_QUEUE_FOUNDATION_AUDIT.md`。4D-02 的实现前基线见 `docs/CURRENT_STAGE4D_02_SPELL_DUEL_BATTLE_BASELINE_EVIDENCE.md`。

## 1. 目标

4D-02 要把争夺战场后的法术对决与战斗从代表性 direct/minimal resolver 推进为更稳定的 task-driven lifecycle：

- `START_SPELL_DUEL` / `START_BATTLE` task 由服务端 cleanup/task queue 创建、推进和关闭，前端只能渲染 snapshot/prompt。
- 法术对决 focus/pass、swift/reaction 入栈、栈清空后焦点恢复、双方让过后关闭都绑定当前 battlefield task。
- `START_BATTLE` active task 必须有稳定 battlefield / battle id / participant metadata，并且 `DECLARE_BATTLE` 只能匹配当前 active task。
- battle declaration、combat damage assignment、battle cleanup、battlefield control resolution 和 task clear 必须在同一服务端 lifecycle 中可观测。
- reconnect/snapshot/prompt 在 `SPELL_DUEL_TASKS`、`BATTLE_TASKS` 和 battle damage assignment window 中保留权威状态，同时继续隐藏对手 face-down standby、内部对象 id 和 raw task kind/reason。

## 2. 当前代码面

已存在的基础面：

- `src/Riftbound.Engine/MatchSession.cs`
  - `TurnWindowState`
  - `SpellDuelState`
  - `BattleState`
  - `BattleLifecycleIds`
  - `BattlefieldTaskState`
  - `PendingTaskQueueState`
  - `BuildSpellDuelState`
  - `BuildBattleState`
  - `BuildBattlefieldTaskSnapshotView`
  - `DeclareBattleSourceRequirements`
  - `AssignCombatDamageMetadataFor`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `ResolvePassPriority` 可以在法术对决栈清空后回到 `SPELL_DUEL_OPEN`。
  - `ResolvePassFocus` 可以关闭争夺战场法术对决并标记 `SpellDuelCompleted`。
  - `AdvancePendingBattlefieldTasksAfterStateChange` 可以启动代表性争夺战场法术对决。
  - `ResolveDeclareBattle` 仍主要是 `DECLARE_BATTLE` 命令驱动的 representative battle resolver。
  - `ResolveAssignCombatDamageRuntime` / `CommitCombatDamageAssignments` 已有代表性 battle damage assignment window。
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
  - `SpellDuelPassCloseEntersDamageAssignmentThenBattleCleanupUpdatesControl`
  - `AssignCombatDamage*` prompt/runtime/stale envelope tests
  - `PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
  - contest stack -> spell duel -> start battle representative paths
  - active start battle command guards
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
  - active `START_BATTLE` prompt filtering
  - invalid `DECLARE_BATTLE` no-mutation guard
  - representative valid `DECLARE_BATTLE` task clear path

Current limitation: these surfaces prove many representative paths, but still do not prove that all spell duel and battle lifecycle transitions are task-scoped, replayable across reconnect, and safe across multiple contested battlefields / stale prompts / wrong focus / cleanup-created follow-up tasks.

## 3. Write Lock

Exclusive implementation write scope:

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`

Allowed new files:

- `tests/Riftbound.ConformanceTests/SpellDuelBattleStateMachineTests.cs`
- Narrow fixtures under `tests/Riftbound.ConformanceTests/Fixtures/` if needed.

Blocked parallel writes:

- Do not modify PaymentEngine / payment behavior in this slice except where existing swift/reaction legality is already part of spell-duel prompt verification.
- Do not modify frontend UI in this slice.
- Do not modify card matrix status in this slice.
- Do not overlap with 4D-03 payment integration in `CoreRuleEngine.cs`.
- Do not use UI-side assumptions to start battle, choose participants, assign damage, resolve control, or clear tasks.

## 4. Required Implementation Shape

Minimum acceptable design:

- Keep `PendingTaskQueue` as the authoritative task surface and route spell duel / battle progression through a shared battlefield task lifecycle entrypoint.
- Persist enough state to bind `PASS_FOCUS`, swift/reaction stack items, `DECLARE_BATTLE`, `ASSIGN_COMBAT_DAMAGE` and battle cleanup to the same battlefield task / battle id.
- Reject non-focus `PASS_FOCUS`, wrong timing `PASS_FOCUS`, stale `DECLARE_BATTLE`, wrong battlefield, wrong participant and wrong player commands with no mutation.
- If spell duel cleanup removes a participant, skip only the matching start battle task and then advance the next pending battlefield task if one exists.
- If multiple battlefields are contested, process one active spell duel / start battle at a time in deterministic battlefield order without promoting the wrong battlefield.
- Preserve existing 4D-01 cleanup ordering: state-based cleanup blocks ordinary actions before spell duel / battle tasks.
- Preserve existing hidden-info redaction and prompt text hygiene; raw task ids may exist in machine-readable snapshot fields where already contracted, but user-visible prompt reasons must remain sanitized.

## 5. Focused Acceptance Tests

4D-02 is not accepted until all of the following are covered by automated tests:

- Multiple contested battlefields queue one active `START_SPELL_DUEL` at a time and do not promote `START_BATTLE` for the wrong battlefield.
- `PASS_FOCUS` by a non-focus player or outside `SPELL_DUEL_OPEN` is rejected with no mutation.
- Swift/reaction play during an active battlefield spell duel keeps timing context tied to the active spell-duel task; after stack resolution, focus returns and the task remains active until all players pass focus.
- Closing spell duel after cleanup removes a participant skips the matching `START_BATTLE` task and advances the next pending battlefield task if present.
- Reconnect during `SPELL_DUEL_TASKS` preserves phase, active task, focus player, participants and hidden-info redaction.
- Reconnect during `BATTLE_TASKS` preserves phase, active start battle task, battle id, participant metadata and hidden-info redaction.
- Active `START_BATTLE` prompt exposes only legal participant choices for the current battlefield; non-unit / face-down / standby / equipment / spell / rune / stale objects remain excluded.
- `DECLARE_BATTLE` is task-driven: wrong actor, wrong battlefield, wrong participant, stale prompt and hidden object ids are rejected with no mutation.
- A valid battle path either opens an explicit damage assignment window or completes the existing representative damage/cleanup path while clearing only the matching battle task and emitting stable battle/control events.
- `ASSIGN_COMBAT_DAMAGE` remains bound to the current battle id / battlefield id and rejects wrong player, wrong participant, incomplete assignment and illegal target order with no mutation.

Suggested focused filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~SpellDuelPassCloseEntersDamageAssignmentThenBattleCleanupUpdatesControl|FullyQualifiedName~AssignCombatDamageRuntime|FullyQualifiedName~AssignCombatDamagePrompt|FullyQualifiedName~PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses|FullyQualifiedName~CoreRuleEngineMarksContestSpellDuelCompletedWhenAllPlayersPassFocus|FullyQualifiedName~CoreRuleEngineSkipsStartBattleWhenSpellDuelCleanupRemovesParticipant|FullyQualifiedName~CoreRuleEngineAllowsDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineRejectsNonActivePlayerDeclareBattleForActiveStartBattleTask|FullyQualifiedName~CoreRuleEngineRejectsDeclareBattleThatDoesNotMatchActiveStartBattleTask|FullyQualifiedName~P6BattlefieldContestStackSeedAdvancesToSpellDuelAfterPriorityPass"
```

Suggested adjacent filter:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~AssignCombatDamage|FullyQualifiedName~BattleState"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 6. No-Go Criteria

- Do not mark P0-004 resolved unless focused, adjacent and backend full tests pass and `docs/CURRENT_SERVER_RULE_AUDIT.md` is updated with exact evidence.
- Do not claim full official battle lifecycle if the slice still depends on direct/minimal `DECLARE_BATTLE` representative resolver without task-scoped state.
- Do not claim P0-002/P0-003 resolved; full battlefield/control lifecycle remains separate unless explicitly proven by official fixtures.
- Do not claim P0-005 resolved; PaymentEngine remains 4D-03.
- Do not update `IMPLEMENTED_TESTED` / full-official matrix fields from this handoff alone.
- Do not call active goal complete.

## 7. Handoff Summary

Next implementing agent should start by adding failing tests around multi-contest ordering, wrong-focus no-mutation, reconnect redaction in spell-duel/battle phases and task-scoped battle id preservation. Then implement the narrowest service-side state/lifecycle changes, run focused / adjacent / backend full, and return diff plus evidence to A for audit.
