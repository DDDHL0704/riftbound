# Stage 4D-02G Battle Response Declaration Context Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02G 实现前的当前 HEAD 基线。4D-02G 目标见 `docs/CURRENT_STAGE4D_02G_BATTLE_RESPONSE_DECLARATION_CONTEXT_HANDOFF.md`。

## Scope

实现前基线确认：

- 4D-02B 到 4D-02F 已有 focused battle lifecycle representative 仍为绿色；
- 当前代码尚未实现 battle-response declaration context preservation；
- 本基线不修改 runtime，不代表 P0-004 关闭。

## Observed Current Runtime Boundary

- `ResolveDeclareBattle` 只在 `COMBAT_ASSIGNMENT` 是唯一 optional cost 且 `BattlefieldTargetObjectIds` 为空时打开 battle-response priority。
- `ResolveBattleResponsePriorityPassed` 重新构造最小 `DeclareBattleCommand`，不会保留原始 declaration context。
- 因此 4D-02G 是下一条自然 P0-004 窄切片，而非已完成能力。

## Validation

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result:

```text
Passed: 428, Failed: 0, Skipped: 0, Total: 428
```

Adjacent baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Battlefield|FullyQualifiedName~SpellDuel|FullyQualifiedName~DeclareBattle|FullyQualifiedName~MoveUnit|FullyQualifiedName~BoardTaskQueue|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineCoverageAuditTests"
```

Result:

```text
Passed: 608, Failed: 0, Skipped: 0, Total: 608
```

Backend full baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

Result:

```text
Passed: 4196, Failed: 0, Skipped: 0, Total: 4196
```

Whitespace:

```sh
git diff --check
```

Result: no output.

Note: an initial parallel adjacent invocation collided with another `dotnet test` build on ASP.NET static web assets cache file `rjsmrazor.dswa.cache.json`. The adjacent command was rerun sequentially and passed 608/608; this baseline uses the sequential passing result.

## Verdict

The current repository is a clean baseline for Raman to implement 4D-02G. Project remains **NOT READY**.
