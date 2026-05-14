# Stage 4D-02G Battle Response Declaration Context Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 侧在 4D-02F 后建立的下一轮 P0-004 交接规格。4D-02B 到 4D-02F 已证明 natural battle response、damage assignment、post-assignment task advancement 与 assignment no-result 的代表路径，但 battle-response 仍只覆盖最小 `DECLARE_BATTLE + COMBAT_ASSIGNMENT` 命令。

## Owner

- 建议 owner：B / Raman，服务端规则、协议、测试实现。
- A 负责验收 diff、跑 focused / adjacent / backend full、更新审计与 checkpoint。

## Observed Gap

- `ResolveDeclareBattle` 当前只在 `optionalCosts.Count == 1`、包含 `COMBAT_ASSIGNMENT` 且 `BattlefieldTargetObjectIds` 为空时尝试开启 battle-response priority。
- 因此，一旦声明战斗携带服务端候选上下文，例如：
  - `BATTLEFIELD_*` defender choice targets；
  - `BRUSH_USE_REPLACED_BATTLEFIELD:*` score-time replacement choice；
  - `RECYCLE_RUNE:*` / `TEMP_PAYMENT_RESOURCE:*` payment-resource choices for held-score battle payment；
  battle 会跳过 response window，直接进入 immediate resolver 或 assignment branch。
- `ResolveBattleResponsePriorityPassed` 目前用新的 `DeclareBattleCommand(battle.BattlefieldObjectId, attackers, defenders, [COMBAT_ASSIGNMENT])` 重建战斗，无法保留原声明的 battlefield target / optional-cost / payment-resource context。

## Target Slice

实现并验证：natural active `START_BATTLE` 中，只要存在合法 battle-response action，服务端必须先开启 `BATTLE_RESPONSE_PRIORITY_OPENED`，即使原始 `DECLARE_BATTLE` 携带服务端支持的 battle declaration context；双方 pass 后继续使用原始 declaration context 进入 assignment 或 resolver，不丢失官方化选项。

最小推荐路径：

- 用带合法 Shadow response 的 natural contested battle。
- 让攻击方声明 `DECLARE_BATTLE` 时携带一个已存在的 battle declaration context（优先选当前代码已有稳定代表：battlefield target choice 或 Brush replacement choice；若选择 payment-resource choice，必须同时验证 no-mutation 和支付审计）。
- 首次 declare 不应 `BATTLE_CLOSED` / 不应直接 `BATTLE_DAMAGE_ASSIGNMENT_OPENED`，而应打开 `BATTLE_RESPONSE_PRIORITY_OPENED`。
- 双方 `PASS_PRIORITY` 后，后续 battle resolution 必须仍能看到原始 context，并产生对应事件 / metadata。

## Expected Tests

至少新增 focused test：

- `BattleDamageAssignmentLifecycleTests.NaturalBattleResponsePreservesDeclarationContextAfterPass`

建议断言：

- declare accepted;
- `BATTLE_DECLARED.optionalCosts` includes the submitted context;
- `BATTLE_RESPONSE_PRIORITY_OPENED` emitted before damage assignment / close;
- prompt is `STACK_PRIORITY` for the legal response player;
- after both players pass, `BATTLE_RESPONSE_PRIORITY_CLOSED` emitted;
- preserved context is still present in the resumed battle declaration / combat path;
- matching `START_BATTLE` task is cleaned or the assignment prompt remains correctly bound, depending on the chosen context;
- no stale `DECLARE_BATTLE` / wrong battlefield prompt;
- existing 4D-02B / 4D-02C / 4D-02E / 4D-02F behavior remains green.

如果实现需要 state carrier，优先保持服务端权威：

- 前端不得重新提交或 reconstruct preserved context。
- context must be stored in authoritative match state or equivalent server-side battle lifecycle state.
- reconnect snapshots / prompts must not leak hidden information.

## Write Scope

允许修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs`
- `src/Riftbound.Engine/MatchSession.cs` only if battle state / prompt metadata genuinely needs a server-side context carrier
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- 可新增 4D-02G audit / evidence docs after implementation

不要修改：

- front-end files
- PaymentEngine broad refactor
- LayerEngine
- card coverage matrix
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
- Do not expand PaymentEngine beyond what the selected preserved context requires.
- Do not change frontend.
- Do not update card coverage matrix.
- Do not close P0-004, P0-005, P1, READY, or active goal.

## Verdict

This is a focused battle-response declaration-context preservation slice. It narrows P0-004 by removing a known shortcut around legal battle-response windows, while still avoiding a full battle lifecycle rewrite.
