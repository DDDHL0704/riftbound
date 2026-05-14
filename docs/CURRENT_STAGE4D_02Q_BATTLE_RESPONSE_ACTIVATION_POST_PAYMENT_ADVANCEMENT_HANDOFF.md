# Stage 4D-02Q Battle Response Activation Post-Payment Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02P 后继续收窄 P0-004 battle lifecycle 的下一批服务端实现交接。它只锁定 natural battle response 中真实 Shadow activation / stack resolution / return-to-response 后，final immediate battle close 打开 post-battle trigger payment；支付窗口作为 blocker 阻止 next contested battlefield advancement，并在 accepted payment / accepted decline 关闭后推进下一处 contested battlefield。不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-02M 已证明普通 immediate battle 后的 post-battle `PendingPayment` 会阻止下一处 contested battlefield advancement，并在 accepted payment / decline 后恢复 advancement。

4D-02P 已证明真实 Shadow activation / stack resolution / return-to-response 后，immediate battle close 可推进下一处 contested battlefield。

仍缺组合护栏：如果 actual battle-response activation 后的 final immediate battle close 先打开 trigger payment，`PendingPayment` blocker 必须继续压住 next battlefield；只有 payment window 被接受支付或拒绝关闭后，才允许推进下一处 `SPELL_DUEL_TASKS`。该链路能防止 activation stack-return 后绕过 4D-02M blocker 语义。

## 2. Target Behavior

最小代表流程：

1. `BF-A` 与 `BF-B` 同时 contested；`BF-A` 已完成 spell duel，当前 active task 是 `task:start-battle:BF-A`。
2. P1 用 Icevale Archer 类 attack trigger context 对 `BF-A` 提交 one-on-one `DECLARE_BATTLE + COMBAT_ASSIGNMENT`；防守侧存在同战场合法 Shadow battle-response source，但 Shadow 不作为 declared defender。
3. P2 在 battle response priority 中激活 Shadow：
   - 输出 `ABILITY_ACTIVATED` / `UNIT_EXHAUSTED` / `COST_PAID` / `STACK_ITEM_ADDED`；
   - stack 未结算期间不得推进 `BF-B`。
4. P2 / P1 pass stack priority 后：
   - stack resolved 并回到 battle response priority；
   - 仍不得推进 `BF-B`。
5. P2 / P1 final response pass 后：
   - 关闭 battle response；
   - immediate battle close / control resolution 后打开 `TRIGGER_PAYMENT`，例如 `ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1`；
   - 不得打开 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`；
   - `PendingPayment` 非空期间不得输出 `BF-B` 的 `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`。
6. Accepted payment 后：
   - 支付、trigger resolved、target effect 与 `PAYMENT_WINDOW_CLOSED` 正常审计；
   - 无 blocker 时推进 `BF-B` 到 `SPELL_DUEL_TASKS`。
7. Accepted decline 后也应关闭 blocker 并推进 `BF-B`，但不得产生成本支付或 trigger resolved effect。
8. Rejected payment attempt 保持 no-mutation，`PendingPayment` 仍打开，`BF-B` 仍不推进。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- 可选：`tests/Riftbound.ConformanceTests/TriggerPaymentTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

禁止写入：

- `src/Riftbound.Engine/MatchSession.cs`，除非发现 prompt / snapshot contract gap。
- PaymentEngine broad rewrite、LayerEngine。
- frontend。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- `riftbound-dotnet.sln`。

## 4. Implementation Notes

- Prefer a failing guard first. This is likely test-only if 4D-02M's post-payment advancement hook is already reached by the activation-returned battle path.
- Suggested fixture:
  - start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId])`;
  - mutate `AttackerObjectId` to Icevale Archer card no `UNL-065/219` and provide a legal `BattlefieldTargetObjectIds` target that survives until payment resolution;
  - keep `ShadowObjectId` on the same battlefield as a legal response source but not declared defender, so exhausting Shadow does not invalidate resumed declaration;
  - give P1 enough power / payment choices for the accepted payment branch.
- Reuse helpers where possible:
  - `AssertNextContestedBattlefieldNotAdvanced(...)`
  - `EventIndex(...)`
  - Trigger payment command patterns from `TriggerPaymentTests`.
- Suggested test names:
  - `NaturalBattleResponseActivationPostPaymentBlocksNextContestedBattlefieldUntilAccepted`
  - optional paired guard: `NaturalBattleResponseActivationPostPaymentDeclineAdvancesNextContestedBattlefield`
  - optional rejected guard if not covered by the accepted / decline tests.
- Assert event order:
  - response close before battle close;
  - battle close / control resolution before `PAYMENT_WINDOW_OPENED`;
  - no `BF-B` advancement before payment close;
  - `PAYMENT_WINDOW_CLOSED` before `BF-B` `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Focused acceptance should include:

- activation / stack-open state blocks `BF-B` advancement;
- stack resolution returns to battle response and still blocks `BF-B` advancement;
- final response pass opens trigger payment and does not open `ASSIGN_COMBAT_DAMAGE`;
- trigger payment open blocks `BF-B` advancement while `PendingPayment` is non-null;
- accepted payment closes the window and advances `BF-B` to `SPELL_DUEL_TASKS`;
- accepted decline closes the window and advances `BF-B` without cost / trigger effect events;
- rejected payment attempt keeps the payment window open and does not advance `BF-B`.

## 6. Adjacent Tests

Recommended adjacent command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~BattlefieldContest|FullyQualifiedName~BattlefieldTasks|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~SpellDuel|FullyQualifiedName~MoveUnit|FullyQualifiedName~ObjectLocation|FullyQualifiedName~GameHub|FullyQualifiedName~TriggerPayment|FullyQualifiedName~PaymentEngine|FullyQualifiedName~PAY_COST|FullyQualifiedName~ShadowActivatedAbilityTests"
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
