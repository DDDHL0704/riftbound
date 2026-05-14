# Stage 4D-02F Battle Assignment No-Result Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 侧在 4D-02E 后建立的下一轮 P0-004 交接规格。4D-02E 已证明 natural `ASSIGN_COMBAT_DAMAGE` 关闭当前 battle 后可以推进下一个 contested battlefield task。下一条最窄缺口是：direct/minimal `DECLARE_BATTLE` 已有 `BATTLE_NO_RESULT` 代表证据，但 natural battle damage assignment runtime 尚缺 focused no-result lifecycle guard。

## Owner

- 建议 owner：B / Raman，服务端规则、协议、测试实现。
- A 负责验收 diff、跑 focused / adjacent / backend full、更新审计与 checkpoint。

## Observed Gap

- `CommitCombatDamageAssignments` 复用 `BuildBattleNoResultEvent` 和 `AppendBattleResolutionEvents`，理论上可以在 assignment 分支产生 `BATTLE_NO_RESULT` 与 `BattleResolutionState.Kind = NO_RESULT`。
- 当前 direct/minimal `DECLARE_BATTLE` 路径已有 `P4DeclareBattleCommandEmitsNoResultWhenAllParticipantsAreDestroyed`。
- `BattleDamageAssignmentLifecycleTests` 尚未覆盖 natural `START_BATTLE` -> `ASSIGN_COMBAT_DAMAGE` 中所有参战者同归于尽时的 no-result、battle close、battle resolution persistence、task cleanup 与 prompt cleanup。

## Target Slice

实现并验证：natural `ASSIGN_COMBAT_DAMAGE` legal assignment 如果导致攻防双方全部被摧毁，服务端必须：

- emit `BATTLE_NO_RESULT` with `reason = ALL_PARTICIPANTS_DESTROYED`;
- emit `BATTLE_CLOSED`;
- persist `BattleResolutionState.Kind = NO_RESULT`;
- leave `WinnerPlayerId` null for the battle resolution;
- clear matching `START_BATTLE` task and open no stale `ASSIGN_COMBAT_DAMAGE` / `DECLARE_BATTLE` prompt;
- keep destroyed participants in the correct graveyards and remove them from battlefield zones / authoritative object maps according to existing cleanup policy;
- preserve existing 4D-02E next-task advancement semantics when no state-based cleanup / stack / active battle / spell duel blocker remains.

## Expected Tests

至少新增/调整 focused test：

- `BattleDamageAssignmentLifecycleTests.NaturalAssignCombatDamageEmitsNoResultWhenAllParticipantsDestroyed`

建议 fixture shape：

- active contested battlefield `BF-DAMAGE`;
- `BattlefieldTaskMarkers.SpellDuelCompleted("BF-DAMAGE")`;
- minimal `DECLARE_BATTLE + COMBAT_ASSIGNMENT`;
- one attacker and one defender, each with equal lethal power, or assignment-ordering variant if needed to force `ASSIGN_COMBAT_DAMAGE`;
- legal assignment causes both sides to receive lethal combat damage in the assignment runtime.

建议断言：

- declare opens `ASSIGN_COMBAT_DAMAGE` prompt;
- legal assignment accepted;
- `BATTLE_NO_RESULT.reason == ALL_PARTICIPANTS_DESTROYED`;
- `BATTLE_CLOSED` emitted;
- no `BATTLEFIELD_HELD` / `BATTLEFIELD_CONQUERED` for this battle;
- `BattleState.IsActive == false`;
- matching `START_BATTLE` task cleared;
- `BattleResolutions.Single().Kind == "NO_RESULT"`;
- `BattleResolutions.Single().WinnerPlayerId == null`;
- surviving attacker / defender lists are empty;
- destroyed object ids include both participants;
- prompts are not stale assignment / declaration prompts.

## Write Scope

允许修改：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the focused test exposes a real runtime gap
- `src/Riftbound.Engine/MatchSession.cs` only if prompt/snapshot metadata genuinely needs adjustment
- 4D-02F audit / evidence docs after implementation

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

- Do not rewrite combat.
- Do not start LayerEngine.
- Do not expand PaymentEngine.
- Do not change frontend.
- Do not update card coverage matrix.
- Do not close P0-004, P0-005, P1, READY, or active goal.

## Verdict

This is a focused no-result lifecycle guard for the natural assignment branch. It should narrow P0-004 without claiming full official battle lifecycle closure.
