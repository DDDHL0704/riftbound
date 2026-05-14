# Stage 4D-02E Battle Task Advancement Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 侧在 4D-02D 后建立的下一轮 P0-004 交接规格。4D-02B/C/D 已证明 natural `START_BATTLE` 可以进入 battle-response priority、再进入 `ASSIGN_COMBAT_DAMAGE`，并在单一战场上完成 battle close / battlefield control。剩余风险之一是：`ASSIGN_COMBAT_DAMAGE` 关闭当前战斗后，还没有 focused 证据证明 pending battlefield task queue 会继续推进到下一个争夺战场。

## Owner

- 建议 owner：B / Raman，服务端规则、协议、测试实现。
- A 负责验收 diff、跑 focused / adjacent / backend full、更新审计与 checkpoint。

## Observed Gap

- `ResolveAssignCombatDamageRuntime` / `CommitCombatDamageAssignments` 已能提交伤害、清理、关闭 battle、结算 battlefield control。
- 当前 focused tests 只断言当前 matching `START_BATTLE` 被清理，不断言同一局面中后续争夺战场会自动进入下一 `START_SPELL_DUEL`。
- 代码阅读显示 `ResolvePassFocus` 在 spell duel close 后已有 next-task advance guard；`CommitCombatDamageAssignments` 目前没有同等的 post-battle assignment advancement evidence。
- 若多个战场同时争夺，当前 battle assignment 关闭一个战场后存在停在 `BATTLEFIELD_TASKS` / `WAIT` 的风险，这属于 P0-004 battle lifecycle task progression gap。

## Target Slice

实现并验证：natural battle damage assignment 关闭当前 active `START_BATTLE` 后，如果还有其他争夺战场且没有 state-based cleanup / stack / active battle / active spell duel 阻塞，服务端必须立即推进下一个 battlefield task，开启对应 `SPELL_DUEL_OPEN` / `START_SPELL_DUEL`。

推荐 focused representative：

- `BF-A` 与 `BF-B` 同时 contested；
- `BF-A` 已有 `BattlefieldTaskMarkers.SpellDuelCompleted("BF-A")`，因此当前 active task 是 `START_BATTLE:BF-A`；
- `BF-A` 走 minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT`，开启 `ASSIGN_COMBAT_DAMAGE`；
- legal assignment 关闭 `BF-A` battle，清理 matching `START_BATTLE:BF-A`；
- `BF-B` 仍 contested 且没有 spell-duel completed marker；
- assignment commit 后应产生 `BATTLEFIELD_CONTESTED` + `SPELL_DUEL_STARTED` for `BF-B`，`TimingState` 进入 `SPELL_DUEL_OPEN`，`FocusPlayerId` 与 `PendingTaskQueue.ActiveTaskId` 指向 `task:start-spell-duel:BF-B`。

## Expected Tests

至少新增/调整 focused test：

- `BattleDamageAssignmentLifecycleTests.NaturalAssignCombatDamageAdvancesNextContestedBattlefieldTask`

建议断言：

- legal `ASSIGN_COMBAT_DAMAGE` accepted；
- `BF-A` battle closed，`BattleState.IsActive == false`；
- `START_BATTLE:BF-A` 不再存在；
- result events 包含 `BATTLE_CLOSED` / `BATTLEFIELD_CONTROL_RESOLVED` for `BF-A`；
- result events 还包含 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` for `BF-B`；
- next state `TimingState == SPELL_DUEL_OPEN`；
- `FocusPlayerId` / `ActivePlayerId` 是新 spell duel focus player；
- `PendingTaskQueue.Phase == "SPELL_DUEL_TASKS"`；
- `PendingTaskQueue.ActiveTaskId == "task:start-spell-duel:BF-B"`；
- prompts expose `SPELL_DUEL_FOCUS` for the focus player, not `WAIT` / stale `ASSIGN_COMBAT_DAMAGE` / stale `DECLARE_BATTLE` for `BF-A`。

Optional guard：

- reconnect after the advancement preserves `BF-B` task metadata and hides opponent hidden standby details if the fixture includes hidden standby.

## Write Scope

允许修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs` only if prompt/snapshot metadata genuinely needs adjustment
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- narrow adjacent tests if needed
- 4D-02E audit / evidence docs after implementation

不要修改：

- front-end files
- PaymentEngine / card matrix / LayerEngine
- unrelated conformance fixtures
- `riftbound-dotnet.sln`

## Validation Gate

实现后必须通过：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

```sh
git diff --check
```

## No-Go

- Do not rewrite all combat.
- Do not start LayerEngine.
- Do not expand PaymentEngine.
- Do not change frontend.
- Do not update card coverage matrix.
- Do not close P0-004, P0-005, P1, READY, or active goal.

## Verdict

This is a focused task-queue progression slice. It should make one natural assignment battle hand off to the next contested battlefield task, narrowing P0-004 without claiming full official battle lifecycle closure.
