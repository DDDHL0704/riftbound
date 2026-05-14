# Stage 4D-02AH Battle Response Activation Brush Next Contest Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

## Purpose

4D-02K 已验收 actual Shadow activation / stack resolution / returned response 后，`BRUSH_USE_REPLACED_BATTLEFIELD:*` declaration context 可进入 replacement-aware held-score payment / score / battle close path。4D-02AF / 4D-02AG 已验收 activation-returned held-score payment branch 完成后可推进 BF-B `START_SPELL_DUEL`，但它们覆盖的是 direct held-score battlefield + `TEMP_PAYMENT_RESOURCE:*` / `RECYCLE_RUNE:*` payment-resource branches。

4D-02AH 锁定一个不同轴的 replacement cross-product：

- BF-A 和 BF-B 同时 contested；
- BF-A 是 Brush battlefield，并替代原始 `SFD·214/221` held-score battlefield；
- BF-A 已完成 spell duel，当前 active `START_BATTLE` 从 BF-A 打开 natural battle response priority；
- P1 declaration optional costs 包含 `COMBAT_ASSIGNMENT` 与 `BRUSH_USE_REPLACED_BATTLEFIELD:<OriginalHeldScoreBattlefieldObjectId>`；
- P2 在 battle response window 中真实 activation Shadow，并通过 stack pass-pass 解析；
- returned response pass-pass 后进入 Brush replacement-aware held-score branch；
- current battle / BF-A task 清理完成后，必须推进 BF-B 的 `START_SPELL_DUEL` task；
- BF-B `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED` 必须出现在 Brush replacement、held-score payment / score 和 BF-A `BATTLE_CLOSED` 之后，且不得在 response、stack 或 returned response 阶段提前出现。

This composes 4D-02K Brush replacement context preservation with the next-contested advancement axis. Passing either side alone is not enough.

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
NaturalBattleResponseActivationBrushReplacementAdvancesNextContestedBattlefieldTask
```

Representative scenario:

1. Extend or locally compose `BuildBrushReplacementActivationNaturalStartBattleState()` so it also includes the existing BF-B next-contest fixtures:
   - `NextBattlefieldObjectId`
   - `NextAttackerObjectId`
   - `NextDefenderObjectId`
2. P2 should start with `new RunePool(1, 5)` so Shadow can spend 1 mana / 1 power and the final replacement-aware held-score branch can still pay 4 power from the current pool.
3. Set:
   - `var brushChoice = $"BRUSH_USE_REPLACED_BATTLEFIELD:{OriginalHeldScoreBattlefieldObjectId}";`
   - `var optionalCosts = new[] { "COMBAT_ASSIGNMENT", brushChoice };`
4. P1 declares battle at `BattlefieldObjectId`, attackers `[AttackerObjectId]`, defenders `[BulwarkDefenderObjectId]`, optional costs `optionalCosts`.
   - Shadow must remain on BF-A as a legal response source, but should not be in `DefenderObjectIds`.
5. Assert initial declaration / opened response preserve `optionalCosts`, create internal declaration context, expose Shadow activation to P2, and do not advance BF-B.
6. P2 activates Shadow targeting `AttackerObjectId`; assert normal activation / cost / stack item and no BF-B advancement.
7. P2 / P1 pass stack; assert Shadow resolves, attacker is stunned, battle remains active, returned response priority is P2, context remains redacted, and no BF-B advancement.
8. P2 / P1 pass returned response priority.
9. Assert final BF-A branch:
   - `BATTLE_RESPONSE_PRIORITY_CLOSED.optionalCosts == optionalCosts`;
   - resumed `BATTLE_DECLARED.optionalCosts == optionalCosts`;
   - `BATTLEFIELD_REPLACEMENT_APPLIED.replacementChoice == brushChoice`;
   - replacement payload points from `BattlefieldObjectId` to `OriginalHeldScoreBattlefieldObjectId`;
   - held-score `BATTLEFIELD_TRIGGER_RESOLVED`, `COST_PAID`, and `SCORE_GAINED` use `OriginalHeldScoreBattlefieldObjectId`;
   - `COST_PAID.reason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
   - `SCORE_GAINED.reason == "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"`;
   - BF-A `BATTLE_CLOSED` happens after Brush replacement and held-score payment / score.
10. Assert final BF-B advancement:
    - `BATTLEFIELD_CONTESTED.battlefieldObjectId == NextBattlefieldObjectId`;
    - `SPELL_DUEL_STARTED.battlefieldObjectId == NextBattlefieldObjectId`;
    - Brush replacement, held-score `SCORE_GAINED`, and BF-A `BATTLE_CLOSED` occur before BF-B `BATTLEFIELD_CONTESTED`;
    - BF-B `BATTLEFIELD_CONTESTED` occurs before BF-B `SPELL_DUEL_STARTED`;
    - final state has `TimingState == SpellDuelOpen`, `FocusPlayerId == "P1"`, `PendingTaskQueue.Phase == "SPELL_DUEL_TASKS"`, and `ActiveTaskId == $"task:start-spell-duel:{NextBattlefieldObjectId}"`;
    - P1 prompt is `SpellDuelFocus` and points at `NextBattlefieldObjectId`.
11. Assert final cleanup:
    - P2 gained 1 score;
    - P2 mana and power are 0 after Shadow plus held-score payment;
    - internal declaration context is cleared and still never leaks to player / spectator snapshots or prompt JSON;
    - no stale BF-A assignment or battle declaration prompt remains.

## Acceptance Commands

Targeted new guard:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~NaturalBattleResponseActivationBrushReplacementAdvancesNextContestedBattlefieldTask"
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

This slice should be test-first. Runtime changes are allowed only if the new cross-product exposes that the Brush replacement-aware held-score branch fails to advance BF-B, advances BF-B too early, leaks the internal battle-response declaration context, leaves stale prompts, or mishandles replacement / score ordering during final resume.

Keep the representative narrow. Do not add broad combat, prevention, LayerEngine, frontend, or card-matrix work.
