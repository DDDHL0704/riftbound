# Stage 4D-02T Battle Response Activation Standby Cleanup Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02S remaining-scope refresh audit 后的下一批服务端实现交接。它只锁定 natural battle response 中真实 Shadow activation / stack resolution / return-to-response 后进入 assignment；合法 assignment 关闭当前 battle 并触发 battlefield-control-driven illegal standby cleanup 时，cleanup 必须先完成，才允许推进下一处 contested battlefield。不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-01C 已证明普通 natural assignment battle close / battlefield control resolve 后，非法 standby cleanup 会先于 next contested battlefield advancement。

4D-02O / 4D-02R 已证明真实 Shadow activation / stack resolution / return-to-response 后，assignment window 与 no-result branch 均不会提前推进 next contested battlefield，legal assignment close 后可推进下一处 battlefield。

仍缺组合护栏：actual battle-response activation 返回 assignment 后，如果 battle close / control resolution 产生 cleanup blocker，服务端必须继续遵守 cleanup-first ordering，不能在 cleanup 前推进下一处 contested battlefield。

## 2. Target Behavior

最小代表流程：

1. `BF-A` 与 `BF-B` 同时 contested；`BF-A` 已完成 spell duel，当前 active task 是 `task:start-battle:BF-A`。
2. `BF-A` 初始 controller 为 P2，且存在 P2 hidden standby object；P1 若赢得 battle control，该 standby 会变成非法 standby cleanup 对象。
3. P1 对 `BF-A` 提交 supported `DECLARE_BATTLE + COMBAT_ASSIGNMENT`，declared defenders 包含 `P2-BULWARK` 与 `P2-SHADOW`，使 Shadow 既是合法 response source 又是 battle participant。
4. P2 在 battle response priority 中激活 Shadow：
   - 输出 `ABILITY_ACTIVATED` / `UNIT_EXHAUSTED` / `COST_PAID` / `STACK_ITEM_ADDED`；
   - stack 未结算期间不得推进 `BF-B`。
5. P2 / P1 pass stack priority 后：
   - stack resolved 并回到 battle response priority；
   - 仍不得推进 `BF-B`。
6. P2 / P1 final response pass 后：
   - 关闭 battle response；
   - 打开 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`；
   - assignment window 打开期间不得推进 `BF-B`。
7. P1 提交 legal assignment，例如复用 `ShadowResponseLegalAssignments()`：
   - Shadow 与 Bulwark 被摧毁，P1 attacker survives；
   - `BF-A` control resolves from P2 to P1;
   - hidden standby cleanup emits `BATTLEFIELD_STANDBY_REMOVED`;
   - cleanup finishes before `BF-B` emits `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`;
   - final state has `BF-B` in `SPELL_DUEL_TASKS` / `SpellDuelFocus`.

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs` only if redaction / prompt contract evidence reveals a real gap.

禁止写入：

- frontend。
- PaymentEngine broad rewrite、LayerEngine。
- `docs/CURRENT_CARD_EFFECT_COVERAGE_MATRIX_SKELETON.json`。
- `riftbound-dotnet.sln`。

## 4. Implementation Notes

- Prefer a failing guard first. This is likely test-only if the activation-returned assignment branch already reuses the same cleanup / advancement ordering as the non-activation branch.
- Suggested test name:
  - `NaturalBattleResponseActivationAssignmentCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask`
- Suggested fixture:
  - start from `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId])`;
  - apply the hidden standby / `BattlefieldObjectId` controller setup from `BuildControlChangeStandbyCleanupNaturalStartBattleState`;
  - use `ShadowResponseLegalAssignments()` so Shadow is destroyed as a declared defender and does not remain as a P2 occupant after battle close.
- Reuse helpers where possible:
  - activation path from `NaturalBattleResponseActivationAssignmentAdvancesNextContestedBattlefieldTask`;
  - cleanup / redaction assertions from `NaturalBattlefieldControlCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask`;
  - `AssertNextContestedBattlefieldNotAdvanced(...)`;
  - `EventIndex(...)`;
  - `AssertOpponentHiddenStandbyRedacted(...)`;
  - `AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(...)`.
- Assert event order:
  - `BATTLE_RESPONSE_PRIORITY_CLOSED` before `BATTLE_DAMAGE_ASSIGNMENT_OPENED`;
  - `BATTLE_CLOSED` before `BATTLEFIELD_CONTROL_RESOLVED`;
  - `BATTLEFIELD_CONTROL_RESOLVED` before `BATTLEFIELD_STANDBY_REMOVED`;
  - `BATTLEFIELD_STANDBY_REMOVED` before `BF-B` `BATTLEFIELD_CONTESTED`;
  - `BF-B` `BATTLEFIELD_CONTESTED` before `SPELL_DUEL_STARTED`.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Focused acceptance should include:

- activation / stack-open state blocks `BF-B` advancement;
- stack resolution returns to battle response and still blocks `BF-B` advancement;
- final response pass opens assignment and still blocks `BF-B`;
- assignment closes current battle and changes `BF-A` control from P2 to P1;
- illegal standby cleanup emits `BATTLEFIELD_STANDBY_REMOVED` before `BF-B` advancement;
- `BF-B` finally advances to `SPELL_DUEL_TASKS`.

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
- Do not broaden Shadow, swift, reaction, PaymentEngine or LayerEngine behavior beyond what this guard reveals.
- Do not modify frontend task UI.
- Do not update card coverage matrix.
- Do not close P0-002, P0-003, P0-004, P0-005, P1 or READY.
- Do not mark active goal complete.
