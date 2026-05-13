# Stage 4D-01 Board Task Queue Foundation Handoff

日期：2026-05-13
结论：**NOT READY**

本文是 Stage 4D-01 的服务端实现交接规格。它只定义 P0-002 / P0-003 的第一片可交付范围，不授权前端补洞、不升级卡牌矩阵、不宣称 battle / spell-duel full official。

## 1. 目标

4D-01 要把当前派生式 battlefield / cleanup task view 推进为可稳定验收的 board task queue foundation：

- 任意会改变场面对象位置、战力、伤害、战场控制或待命合法性的状态变化后，进入同一个 post-state-change task runner。
- state-based cleanup 优先于 battlefield contest / spell-duel / battle task。
- task queue 阻塞期间，服务端 prompt 只开放当前任务允许动作或 `WAIT` / `SURRENDER`，手写普通命令必须 rejected 且 no mutation。
- snapshot / prompt 对非授权玩家继续隐藏 face-down standby、task object id、raw task kind / reason 和内部对象 id。

## 2. 当前代码面

已存在的基础面：

- `src/Riftbound.Engine/MatchSession.cs`
  - `PendingTaskQueueState`
  - `SpellDuelState`
  - `BattleState`
  - `ContinuousEffectState`
  - `MatchState.PendingTaskQueue`
  - `BuildPendingTaskQueue`
  - `SelectActivePendingTask`
  - `PendingTaskQueuePhase`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
  - `RunStateBasedCleanupLoop`
  - `ApplyIllegalStandbyCleanup`
  - `ApplyUnattachedEquipmentCleanup`
  - `AdvancePendingBattlefieldTasksAfterStateChange`
  - `ResolvePassFocus`
  - move-unit paths already call cleanup and then battlefield task advance in representative routes.
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`
  - active `START_BATTLE` prompt candidate guard
  - invalid `DECLARE_BATTLE` no-mutation guard
  - representative valid `DECLARE_BATTLE` task clear path
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
  - pending cleanup task queue shape
  - illegal standby cleanup redaction
  - unattached equipment cleanup task
  - spell-duel task active phase
  - start-battle task after spell duel closes

Current limitation: these surfaces are still a mix of derived views, marker effects and selected resolver calls. They do not yet prove that every relevant state change enters one durable task lifecycle, nor that battlefield control / held / conquer / standby transitions are officialized.

## 3. Write Lock

Exclusive implementation write scope:

- `src/Riftbound.Engine/MatchSession.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureShapeTests.cs`
- `tests/Riftbound.ConformanceTests/ConformanceFixtureRunnerTests.cs`
- `tests/Riftbound.ConformanceTests/BattlefieldContestBattleTaskGuardTests.cs`

Allowed new files:

- `tests/Riftbound.ConformanceTests/BoardTaskQueueFoundationTests.cs`
- Narrow fixtures under `tests/Riftbound.ConformanceTests/Fixtures/` if needed.

Blocked parallel writes:

- Do not modify PaymentEngine / payment behavior in this slice.
- Do not modify frontend task UI in this slice.
- Do not modify card matrix status in this slice.
- Do not overlap with 4D-02 battle state-machine implementation in `CoreRuleEngine.cs`.

## 4. Required Implementation Shape

Minimum acceptable design:

- Introduce a single post-state-change runner or equivalent shared entrypoint for cleanup + battlefield task advancement.
- Route MOVE_UNIT, stack resolution, trigger resolution, turn-start damage, turn-end cleanup, battlefield control changes, standby reveal/removal and equipment detach/recall through that entrypoint.
- Preserve existing FAQ fix: zero or negative power with no damage is not an automatic cleanup task; zero or negative power with damage can be lethal cleanup.
- Ensure cleanup loop repeats until stable before battlefield contest tasks become active.
- Ensure `START_SPELL_DUEL` and `START_BATTLE` task ordering is stable and battlefield scoped.
- Ensure task identity is stable across snapshot / reconnect enough for UI rendering, without exposing hidden object ids to unauthorized viewers.

## 5. Focused Acceptance Tests

4D-01 is not accepted until all of the following are covered by automated tests:

- Base-to-battlefield move into empty battlefield updates `ObjectLocations`, `BattlefieldStates` and does not create contest tasks.
- Base-to-battlefield move into opponent-controlled occupied battlefield creates cleanup-first queue, then `START_SPELL_DUEL`, then `START_BATTLE`.
- Battlefield-to-base move can remove contest and returns queue to `IDLE` when no cleanup remains.
- Battlefield-to-battlefield roam preserves exact `BATTLEFIELD:<objectId>` casing and queues contest tasks for the destination battlefield only.
- Lethal cleanup blocks ordinary actions before battlefield tasks and leaves failed commands as no-mutation.
- Illegal standby cleanup hides standby object ids from the opponent snapshot and prompt.
- Unattached battlefield equipment cleanup runs through the same queue and cannot leak raw cleanup kind/reason in prompt text.
- Cleanup repeats until stable when one cleanup action creates another cleanup candidate.
- `PASS_FOCUS` closes a queued battlefield spell duel and promotes the corresponding `START_BATTLE` task without losing participant controller/object data.
- Reconnect/snapshot rebuild preserves pending task phase, active task and hidden-info redaction.

Suggested focused filters:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BoardTaskQueueFoundation|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~PendingTaskQueue"
```

Suggested adjacent filters:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContest|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~MoveUnit|FullyQualifiedName~StateBasedCleanup|FullyQualifiedName~SpellDuel|FullyQualifiedName~StartBattle"
```

Final per-slice gate:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

## 6. No-Go Criteria

- Do not mark P0-002 / P0-003 resolved unless all focused and adjacent tests above pass and `docs/CURRENT_SERVER_RULE_AUDIT.md` is updated with exact evidence.
- Do not claim P0-004 resolved; battle and spell-duel lifecycle remain 4D-02 unless fully implemented in a separate accepted slice.
- Do not claim P0-005 resolved; PaymentEngine remains 4D-03.
- Do not update `IMPLEMENTED_TESTED` / full-official matrix fields from this handoff alone.
- Do not call active goal complete.

## 7. Handoff Summary

Next implementing agent should start by designing the shared post-state-change runner and adding failing tests for cleanup-first ordering, contest task promotion and hidden-info redaction. Once the focused tests are red for the intended gaps, implement the narrowest queue foundation changes, run focused / adjacent / backend full, then return diff and evidence to A for audit.
