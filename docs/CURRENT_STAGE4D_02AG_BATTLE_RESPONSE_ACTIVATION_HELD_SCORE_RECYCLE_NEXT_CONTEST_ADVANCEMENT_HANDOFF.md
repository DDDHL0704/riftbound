# Stage 4D-02AG Battle Response Activation Held-Score Recycle Next Contest Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02AF 已验收 BF-A / BF-B 双争夺场景中 activation-returned held-score `TEMP_PAYMENT_RESOURCE:*` branch 的 next-contested advancement。4D-02AG 做对称的 `RECYCLE_RUNE:*` mirror，避免 held-score next-contest evidence 只覆盖 temporary resource。

4D-02AG 锁定一个窄的 next-contested advancement representative：

- BF-A 和 BF-B 同时 contested；
- BF-A 已完成 spell duel，当前 active `START_BATTLE` 从 BF-A 打开 natural battle response priority；
- P1 declaration optional costs 包含 `COMBAT_ASSIGNMENT` 与 `RECYCLE_RUNE:<HeldScoreRecycleRuneObjectId>`；
- P2 在 battle response window 中真实 activation Shadow，并通过 stack pass-pass 解析；
- returned response pass-pass 后进入 held-score branch，消费 recycle rune 并让 P2 得分；
- current battle / BF-A task 清理完成后，必须推进 BF-B 的 `START_SPELL_DUEL` task；
- `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` for BF-B 必须出现在 held-score payment / score / current battle close 之后，且不得在 response、stack 或 returned response 阶段提前出现。

This composes 4D-02AD recycle held-score payment-resource context with 4D-02AF next-contested advancement.

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
NaturalBattleResponseActivationHeldScoreRecyclePaymentAdvancesNextContestedBattlefieldTask
```

Representative scenario:

1. Extend or locally compose `BuildHeldScorePaymentResourceNaturalStartBattleState()` so it also includes the existing BF-B next-contest fixtures:
   - `NextBattlefieldObjectId`
   - `NextAttackerObjectId`
   - `NextDefenderObjectId`
2. P2 should start with `new RunePool(1, 4)` so Shadow can spend 1 mana / 1 power and the final held-score branch can still pay 4 power by recycling `HeldScoreRecycleRuneObjectId`.
3. Set:
   - `var recycleAction = $"RECYCLE_RUNE:{HeldScoreRecycleRuneObjectId}";`
   - `var optionalCosts = new[] { "COMBAT_ASSIGNMENT", recycleAction };`
4. P1 declares battle at `BattlefieldObjectId`, attackers `[AttackerObjectId]`, defenders `[BulwarkDefenderObjectId]`, optional costs `optionalCosts`.
   - Shadow must remain on BF-A as a legal response source, but should not be in `DefenderObjectIds`.
5. Assert initial declaration / opened response preserve `optionalCosts`, create internal declaration context, and do not expose `BF-NEXT` advancement.
6. P2 activates Shadow targeting `AttackerObjectId`; assert normal activation / cost / stack item and no `BF-NEXT` advancement.
7. P2 / P1 pass stack; assert Shadow resolves, attacker is stunned, battle remains active, returned response priority is P2, and no `BF-NEXT` advancement.
8. P2 / P1 pass returned response priority.
9. Assert final BF-A branch:
   - `BATTLE_RESPONSE_PRIORITY_CLOSED.optionalCosts == optionalCosts`;
   - resumed `BATTLE_DECLARED.optionalCosts == optionalCosts`;
   - `BATTLEFIELD_HELD` for BF-A happens before held-score trigger / recycle / payment / score;
   - `RUNE_RECYCLED.sourceObjectId == HeldScoreRecycleRuneObjectId`;
   - `POWER_GAINED.sourceObjectId == HeldScoreRecycleRuneObjectId`;
   - `COST_PAID.reason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
   - `COST_PAID.paymentResourceActions == [recycleAction]`;
   - `COST_PAID.recycledRuneObjectIds == [HeldScoreRecycleRuneObjectId]`;
   - `SCORE_GAINED.reason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
   - `BATTLE_CLOSED` for BF-A happens after held-score payment / score.
10. Assert final BF-B advancement:
    - `BATTLEFIELD_CONTESTED.battlefieldObjectId == NextBattlefieldObjectId`;
    - `SPELL_DUEL_STARTED.battlefieldObjectId == NextBattlefieldObjectId`;
    - held-score `SCORE_GAINED` and BF-A `BATTLE_CLOSED` occur before BF-B `BATTLEFIELD_CONTESTED`;
    - BF-B `BATTLEFIELD_CONTESTED` occurs before BF-B `SPELL_DUEL_STARTED`;
    - final state has `TimingState == SpellDuelOpen`, `FocusPlayerId == "P1"`, `PendingTaskQueue.Phase == "SPELL_DUEL_TASKS"`, and `ActiveTaskId == $"task:start-spell-duel:{NextBattlefieldObjectId}"`;
    - P1 prompt is `SpellDuelFocus` and points at `NextBattlefieldObjectId`.
11. Assert final cleanup:
    - P2 gained 1 score;
    - P2 power is 0 after payment;
    - `HeldScoreRecycleRuneObjectId` moved from P2 base to P2 rune deck;
    - internal declaration context is cleared and still never leaks to player / spectator snapshots or prompt JSON;
    - no stale BF-A assignment or battle declaration prompt remains.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationHeldScoreRecyclePaymentAdvancesNextContestedBattlefieldTask"
```

Focused:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests|FullyQualifiedName~RevealCard"
```

Adjacent:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~BattleResponse|FullyQualifiedName~StackPriority|FullyQualifiedName~RevealCard|FullyQualifiedName~StandbyReaction|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
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

This slice should be test-first. Runtime changes are allowed only if the new cross-product exposes that the recycle held-score branch fails to advance BF-B, advances BF-B too early, leaks the internal battle-response declaration context, leaves stale prompts, or mishandles the recycle action during final resume.

Keep the representative narrow. Do not add broad combat, replacement, prevention, LayerEngine, frontend, or card-matrix work.
