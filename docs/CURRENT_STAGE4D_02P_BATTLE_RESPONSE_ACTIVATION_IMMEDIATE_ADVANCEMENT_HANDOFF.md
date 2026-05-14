# Stage 4D-02P Battle Response Activation Immediate Advancement Handoff

日期：2026-05-15
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 4D-02O 后继续收窄 P0-004 battle lifecycle 的下一批服务端实现交接。它只锁定 natural battle response 中真实 Shadow activation / stack resolution / return-to-response -> immediate battle close -> next contested battlefield task advancement 的组合护栏，不授权 full combat rewrite、不改前端、不更新卡牌矩阵、不关闭 P0/P1 或 READY。

## 1. Why This Slice

4D-02O 已证明 actual Shadow activation / stack-return 后，若 final battle continuation 进入 `ASSIGN_COMBAT_DAMAGE`，legal assignment close 会推进下一处 contested battlefield。

仍缺 immediate branch parity guard：final response pass 后，如果 resumed `DECLARE_BATTLE` 不打开 assignment window，而是走 one-on-one immediate battle close，也应该沿用 4D-02L 的 next contested advancement 语义。现有 `ShadowActivatedAbilityTests.ShadowActivatesAndResolvesFromNaturalBattleResponseWindow` 只证明 actual activation 后 battle can close，没有带 next contested battlefield；4D-02L 只证明无 response activation 的 immediate branch advancement。

## 2. Target Behavior

最小代表流程：

1. `BF-A` 与 `BF-B` 同时 contested；`BF-A` 已完成 spell duel，当前 active task 是 `task:start-battle:BF-A`。
2. P1 对 `BF-A` 提交 one-on-one `DECLARE_BATTLE + COMBAT_ASSIGNMENT`，防守侧存在同战场合法 Shadow battle-response source，但 Shadow 不作为 declared defender。
3. P2 在 battle response priority 中激活 Shadow：
   - 输出 `ABILITY_ACTIVATED` / `UNIT_EXHAUSTED` / `COST_PAID` / `STACK_ITEM_ADDED`；
   - stack 未结算期间不得推进 `BF-B`。
4. P2 / P1 pass stack priority 后：
   - 输出 `STACK_ITEM_RESOLVED` / `ABILITY_RESOLVED` / `STATUS_EFFECT_APPLIED`；
   - 回到 battle response priority；
   - 仍不得推进 `BF-B`。
5. P2 / P1 final response pass 后：
   - 输出 `BATTLE_RESPONSE_PRIORITY_CLOSED`；
   - resumed immediate battle 直接关闭 `BF-A`，不得打开 `ASSIGN_COMBAT_DAMAGE`；
   - 当前 battle close / control resolution 后推进 `BF-B`；
   - `BF-B` 进入 `SPELL_DUEL_TASKS`，active task 是 `task:start-spell-duel:BF-B`，focus prompt 为 `SpellDuelFocus`。

## 3. Suggested Write Scope

Owner：B 服务端规则 / 协议 / 测试实现（当前 Raman）。

允许写入：

- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`

可选但不首选：

- `tests/Riftbound.ConformanceTests/ShadowActivatedAbilityTests.cs`

仅 runtime gap 时允许写入：

- `src/Riftbound.Engine/CoreRuleEngine.cs`

仅 prompt / snapshot contract gap 时允许写入：

- `src/Riftbound.Engine/MatchSession.cs`

禁止写入：

- frontend。
- PaymentEngine broad rewrite、LayerEngine、card matrix。
- unrelated conformance fixture rewrites。
- `riftbound-dotnet.sln`。

## 4. Implementation Notes

- Prefer a failing guard first; this is likely test-only if `ResolveBattleResponsePriorityPassed(...)` returns to `ResolveDeclareBattle(..., openBattleResponsePriority: false)` and the immediate branch already calls shared advancement after 4D-02L.
- Suggested fixture:
  - `BuildNaturalStartBattleState(includeShadowResponse: true, includeNextContest: true, defenderObjectIds: [BulwarkDefenderObjectId])`
  - declare only `[BulwarkDefenderObjectId]` as defender so final battle is immediate;
  - keep `ShadowObjectId` at same battlefield as a legal response source but not a defender, so exhausting Shadow as cost does not invalidate resumed declaration.
- Reuse existing local helpers where possible:
  - `AssertNextContestedBattlefieldNotAdvanced(...)`
  - `EventIndex(...)`
- Add a dedicated test name such as `NaturalBattleResponseActivationImmediateBattleAdvancesNextContestedBattlefieldTask`.
- Assert no `BF-B` advancement while stack is open and after stack resolution returns to response.
- Assert final response pass result:
  - contains `BATTLE_RESPONSE_PRIORITY_CLOSED`;
  - does not contain `BATTLE_DAMAGE_ASSIGNMENT_OPENED`;
  - has `BATTLE_CLOSED` / `BATTLEFIELD_CONTROL_RESOLVED` before `BF-B` `BATTLEFIELD_CONTESTED` / `SPELL_DUEL_STARTED`;
  - ends in `SPELL_DUEL_TASKS` for `BF-B` with no stale battle declaration / assignment prompt.

## 5. Focused Tests

Recommended focused command:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~PendingTaskQueue|FullyQualifiedName~StartBattle|FullyQualifiedName~DeclareBattle|FullyQualifiedName~TriggerPaymentTests|FullyQualifiedName~ShadowActivatedAbilityTests"
```

Focused acceptance should include:

- activation / stack-open state blocks `BF-B` advancement;
- stack resolution returns to battle response and still blocks `BF-B` advancement;
- final response pass does not open `ASSIGN_COMBAT_DAMAGE`;
- final response pass closes `BF-A` and advances `BF-B` to `SPELL_DUEL_TASKS`;
- resulting prompt is `SpellDuelFocus` for `BF-B`, not stale `AssignCombatDamage` / `BattleDeclaration` for `BF-A`.

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
