# Stage 4D-02AI Battle Response Activation Held-Score Prevention Next Contest Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02AF / 4D-02AG 已验收 activation-returned held-score payment / score / battle close 后推进 BF-B `START_SPELL_DUEL`；4D-02AH 已验收 Brush replacement-aware held-score branch 的 next-contested advancement。它们都覆盖“得分实际发生”的分支。

4D-02AI 转向 prevention 轴，锁定一个更窄的 held-score score-prevention representative：

- BF-A 和 BF-B 同时 contested；
- BF-A 是 `SFD·214/221` held-score battlefield；
- P2 同时控制 `SFD·209/221` 分数延迟 battlefield，且当前 P2 turn ordinal 尚未释放得分；
- BF-A 已完成 spell duel，当前 active `START_BATTLE` 从 BF-A 打开 natural battle response priority；
- P1 declaration optional costs 只包含 `COMBAT_ASSIGNMENT`；
- P2 在 battle response window 中真实 activation Shadow，并通过 stack pass-pass 解析；
- returned response pass-pass 后进入 held-score branch，但 `BATTLEFIELD_SCORE_PREVENTED` 阻止 `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`；
- prevention 分支不得支付 4 power、不得 `SCORE_GAINED`；
- current battle / BF-A task 清理完成后，必须推进 BF-B 的 `START_SPELL_DUEL` task；
- BF-B `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` 必须出现在 score-prevention 和 BF-A `BATTLE_CLOSED` 之后，且不得在 response、stack 或 returned response 阶段提前出现。

This composes existing battlefield score prevention with the activation-returned next-contested advancement axis. Passing either side alone is not enough.

## Owner

B 服务端规则 / 协议 / 测试实现：Raman `019e2257-8d40-7630-9201-28df44dd689a`

## Write Scope

Primary expected write:

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

Allowed only if the new guard exposes a real runtime bug:

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs`

No-go:

- Do not modify frontend.
- Do not broaden PaymentEngine / LayerEngine.
- Do not update card coverage matrix.
- Do not touch `riftbound-dotnet.sln`.
- Do not close P0-004, P0-005, P1, READY, or the active goal.

## Required Guard

Add one focused server guard, proposed name:

```text
NaturalBattleResponseActivationHeldScorePreventionAdvancesNextContestedBattlefieldTask
```

Representative scenario:

1. Add or locally compose a state helper such as `BuildHeldScorePreventionNextContestNaturalStartBattleState()`.
2. Base it on the existing natural battle response / held-score helpers:
   - `BattlefieldObjectId` is `SFD·214/221`;
   - `ShadowObjectId` is a legal response source on BF-A but is not in `DefenderObjectIds`;
   - `NextBattlefieldObjectId`, `NextAttackerObjectId`, and `NextDefenderObjectId` form the waiting BF-B contest.
3. Add a P2-controlled `SFD·209/221` score-delay battlefield object to P2 battlefield zone, and set the state so `PlayerTurnOrdinal(state, "P2")` is below the release ordinal. Existing direct tests use `TurnNumber = 1`.
4. P2 should start with enough resources for Shadow and an otherwise-payable held-score branch, for example `new RunePool(1, 5)`.
5. P1 declares battle at `BattlefieldObjectId`, attackers `[AttackerObjectId]`, defenders `[BulwarkDefenderObjectId]`, optional costs `["COMBAT_ASSIGNMENT"]`.
6. Assert initial declaration / opened response preserve optional costs, create internal declaration context, expose Shadow activation to P2, and do not advance BF-B.
7. P2 activates Shadow targeting `AttackerObjectId`; assert normal activation / cost / stack item, P2 resources are reduced only by Shadow, and no BF-B advancement.
8. P2 / P1 pass stack; assert Shadow resolves, attacker is stunned, battle remains active, returned response priority is P2, context remains redacted, and no BF-B advancement.
9. P2 / P1 pass returned response priority.
10. Assert final BF-A prevention branch:
    - `BATTLE_RESPONSE_PRIORITY_CLOSED.optionalCosts == ["COMBAT_ASSIGNMENT"]`;
    - resumed `BATTLE_DECLARED.optionalCosts == ["COMBAT_ASSIGNMENT"]`;
    - `BATTLEFIELD_HELD` for BF-A appears before score prevention;
    - `BATTLEFIELD_SCORE_PREVENTED.trigger == "BATTLEFIELD_SCORE_DELAY_UNTIL_THIRD_TURN"`;
    - `BATTLEFIELD_SCORE_PREVENTED.preventedReason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
    - `scoreSourceObjectIds` includes the BF-A held-score battlefield object id;
    - no `COST_PAID` with reason `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`;
    - no `SCORE_GAINED` with reason `BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE`;
    - P2 score remains 0;
    - P2 still has 4 power after Shadow because held-score payment was prevented before cost payment;
    - BF-A `BATTLE_CLOSED` happens after `BATTLEFIELD_SCORE_PREVENTED`.
11. Assert final BF-B advancement:
    - `BATTLEFIELD_CONTESTED.battlefieldObjectId == NextBattlefieldObjectId`;
    - `SPELL_DUEL_STARTED.battlefieldObjectId == NextBattlefieldObjectId`;
    - score prevention and BF-A `BATTLE_CLOSED` occur before BF-B `BATTLEFIELD_CONTESTED`;
    - BF-B `BATTLEFIELD_CONTESTED` occurs before BF-B `SPELL_DUEL_STARTED`;
    - final state has `TimingState == SpellDuelOpen`, `FocusPlayerId == "P1"`, `PendingTaskQueue.Phase == "SPELL_DUEL_TASKS"`, and `ActiveTaskId == $"task:start-spell-duel:{NextBattlefieldObjectId}"`;
    - P1 prompt is `SpellDuelFocus` and points at `NextBattlefieldObjectId`.
12. Assert final cleanup:
    - internal declaration context is cleared and still never leaks to player / spectator snapshots or prompt JSON;
    - no stale BF-A assignment or battle declaration prompt remains.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationHeldScorePreventionAdvancesNextContestedBattlefieldTask"
```

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~P79BattlefieldScoreDelay|FullyQualifiedName~P79BattlefieldScorePrevented|FullyQualifiedName~P79BattlefieldHeldScoreCanOnly"
```

Backend full:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Whitespace:

```sh
git diff --check
```

## Acceptance Notes

This slice should be test-first. Runtime changes are allowed only if the new cross-product exposes that score prevention happens after cost payment, consumes resources, allows score gain, fails to advance BF-B after battle close, advances BF-B too early, leaks the internal battle-response declaration context, or leaves stale prompts.

Keep the representative narrow. Do not add broad damage prevention, full LayerEngine, frontend, or card-matrix work.
