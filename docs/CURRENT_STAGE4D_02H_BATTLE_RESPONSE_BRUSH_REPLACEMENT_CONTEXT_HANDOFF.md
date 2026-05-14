# Stage 4D-02H Battle Response Brush Replacement Context Handoff

日期：2026-05-14
结论：**HANDOFF READY / PROJECT NOT READY**

本文是 A 侧在 4D-02G 后建立的下一轮 P0-004 交接规格。4D-02G 已证明 natural battle-response 可以保留一条 Icevale Archer battlefield target declaration context。下一条最小相邻缺口是：同一 response preservation carrier 需要锁住 Brush score-time replacement context，防止后续改动重新丢失 `BRUSH_USE_REPLACED_BATTLEFIELD:*` optional cost。

## Owner

- 建议 owner：B / Raman，服务端规则、协议、测试实现。
- A 负责验收 diff、跑 focused / adjacent / backend full、更新审计与 checkpoint。

## Observed Gap

- 4D-02G carrier 会保存 `OptionalCosts`，理论上应覆盖 `BRUSH_USE_REPLACED_BATTLEFIELD:*`。
- 当前 focused test 只证明 `BattlefieldTargetObjectIds` preservation。
- Brush replacement 是 battle-result / score-time replacement 分支，属于 P0-004 剩余 replacement / battle-result ordering 风险的一部分，需要独立 guard。

## Target Slice

实现并验证：natural active `START_BATTLE` 中，攻击方声明带 `COMBAT_ASSIGNMENT + BRUSH_USE_REPLACED_BATTLEFIELD:<originalBattlefieldObjectId>` 的 `DECLARE_BATTLE`，若存在合法 battle response，服务端必须：

- 先开启 `BATTLE_RESPONSE_PRIORITY_OPENED`；
- 在 response window events 中保留 optional costs；
- 双方 pass 后继续用原始 optional costs 结算 battle；
- emit `BATTLEFIELD_REPLACEMENT_APPLIED` for Brush replacement;
- 让 held-score branch 使用替代后的 original battlefield identity；
- 不把 `BATTLE_RESPONSE_DECLARATION_CONTEXT:*` carrier 暴露到 player / spectator snapshot 或 prompt。

## Expected Tests

至少新增 focused test：

- `BattleDamageAssignmentLifecycleTests.NaturalBattleResponsePreservesBrushReplacementContextAfterPass`

建议 fixture shape：

- active contested Brush battlefield object, controlled by defending player;
- Brush battlefield has exactly one `REPLACES_BATTLEFIELD:<original>` tag;
- original battlefield card is the existing held-score payment battlefield representative;
- P1 has one attacking unit at Brush;
- P2 has one defending unit that can hold the battlefield plus a legal Shadow response source at the same battlefield;
- declare uses `OptionalCosts: ["COMBAT_ASSIGNMENT", "BRUSH_USE_REPLACED_BATTLEFIELD:<original>"]`;
- response opens, both players pass, resumed battle resolves held branch and opens / emits the replacement-aware held-score event path.

建议断言：

- initial declare accepted;
- `BATTLE_RESPONSE_PRIORITY_OPENED` emitted before battle close / held score payment;
- `BATTLE_DECLARED.optionalCosts` and `BATTLE_RESPONSE_PRIORITY_OPENED.optionalCosts` include the Brush replacement choice;
- after pass, `BATTLE_RESPONSE_PRIORITY_CLOSED.optionalCosts` includes the Brush replacement choice;
- `BATTLEFIELD_REPLACEMENT_APPLIED.replacementChoice` matches the submitted choice;
- held-score branch uses `effectiveBattlefieldObjectId` / original battlefield id in replacement payload;
- internal carrier filtered from P1 / P2 / spectator snapshots and prompt;
- no stale battle declaration / assignment prompt remains for the selected branch.

## Write Scope

允许修改：

- `src/Riftbound.Engine/CoreRuleEngine.cs` only if the focused test exposes a real runtime gap
- `src/Riftbound.Engine/MatchSession.cs` only if public projection filtering needs adjustment
- `tests/Riftbound.ConformanceTests/BattleDamageAssignmentLifecycleTests.cs`
- 可新增 4D-02H audit / evidence docs after implementation

不要修改：

- front-end files
- PaymentEngine broad refactor
- LayerEngine
- card coverage matrix
- unrelated fixtures
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
- Do not expand PaymentEngine except through the existing held-score branch already used by Brush replacement.
- Do not change frontend.
- Do not update card coverage matrix.
- Do not close P0-004, P0-005, P1, READY, or active goal.

## Verdict

This is a focused replacement-context preservation guard for the natural battle-response branch. It should narrow P0-004 without claiming full official battle lifecycle closure.
