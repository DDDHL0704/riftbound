# Stage 4D-02I Battle Response Payment Resource Context Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-02I 实现前的当前 HEAD 基线。4D-02I 目标见 `docs/CURRENT_STAGE4D_02I_BATTLE_RESPONSE_PAYMENT_RESOURCE_CONTEXT_HANDOFF.md`。

## Scope

实现前基线确认：

- 4D-02G battle response declaration-context preservation 已验收；
- 4D-02H Brush replacement context preservation 已验收；
- 当前 HEAD 仍未提供 held-score payment-resource context 的 dedicated response-pass guard；
- 本基线不修改 runtime，不代表 P0-004 或 P0-005 关闭。

## Observed Current Runtime Boundary

- `ResolveDeclareBattle` 支持 `RECYCLE_RUNE:*` / `TEMP_PAYMENT_RESOURCE:*` 作为 held-score battle payment optional costs。
- `ResolveBattleResponsePriorityPassed` 依赖 4D-02G 的 server-side declaration context carrier 恢复 original optional costs。
- 目前只有 battlefield target 与 Brush replacement 的 dedicated battle-response preservation tests；payment-resource optional cost 仍缺单独回归护栏。

## Validation

Focused baseline:

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~BattleDamageAssignmentLifecycleTests|FullyQualifiedName~SpellDuelBattleStateMachineTests|FullyQualifiedName~BattlefieldContestBattleTaskGuardTests|FullyQualifiedName~Shadow|FullyQualifiedName~DeclareBattle|FullyQualifiedName~StartBattle|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~Swift|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

Result:

```text
Passed: 430, Failed: 0, Skipped: 0, Total: 430
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
Passed: 4198, Failed: 0, Skipped: 0, Total: 4198
```

Whitespace:

```sh
git diff --check
```

Result: no output.

## Verdict

The current repository is a clean baseline for Raman to implement 4D-02I. Project remains **NOT READY**.
