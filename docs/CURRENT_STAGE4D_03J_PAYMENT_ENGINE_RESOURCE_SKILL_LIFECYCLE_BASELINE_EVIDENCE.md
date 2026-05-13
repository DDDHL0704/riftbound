# Stage 4D-03J PaymentEngine Resource Skill Lifecycle Baseline Evidence

日期：2026-05-14
结论：**BASELINE GREEN / IMPLEMENTATION NOT STARTED / PROJECT NOT READY**

本文记录 4D-03J 实现前的 A 侧测试基线。当前 HEAD 已完成 4D-03I Malzahar open-main representative，但尚未实现 spell-duel focus timing、reaction prohibition engine 或完整 payment-only lifecycle。本基线只证明实现前 Malzahar / spell-duel / reaction / payment 相邻路径绿色，可作为后续 B 侧实现验收对照。

## 1. Baseline Scope

当前已存在行为：

- Malzahar open-main prompt / command / no-mutation / audit tests 通过。
- Malzahar spell-duel prompt 当前不开放 `ACTIVATE_ABILITY`。
- Malzahar spell-duel direct command 当前 rejected no-mutation。
- Spell-duel focus、reaction counter、priority、ActionPrompt、GameHub 与 payment resource 相邻回归绿色。

当前未实现行为：

- `SPELL_DUEL_OPEN` / `FocusPlayerId` 下公开并执行 Malzahar resource skill。
- Malzahar resource skill 的 reaction prohibition target filter。
- payment-only temporary resource ledger、消费与清理 lifecycle。

## 2. Focused Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~SpellDuel|FullyQualifiedName~Reaction|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill"
```

结果：

```text
Passed! - Failed: 0, Passed: 109, Skipped: 0, Total: 109
```

覆盖含义：

- 4D-03I open-main Malzahar resource skill 仍绿色。
- 当前 spell-duel 不开放 Malzahar resource skill 的旧期望仍绿色。
- Spell-duel / reaction / PaymentEngineUnification / ResourceSkill 相邻代表路径未回退。

## 3. Adjacent Baseline

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~SpellDuel|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 336, Skipped: 0, Total: 336
```

覆盖含义：

- ActivateAbility、SpellDuel、Priority、Reaction、PaymentResource、SpendPower、RunePool、ActionPrompt 与 GameHub 相邻路径当前绿色。
- 该基线适合作为 4D-03J 实现后的 regression 对照。

## 4. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本基线未读取、未修改、未暂存。

## 5. Verdict

4D-03J 可以进入 B 侧实现交接。实现前风险集中在 spell-duel focus timing、reaction target prohibition 与 payment-only temporary resource lifecycle；若 B 只能完成其中一部分，必须在审计中保留 explicit residual risk，不能关闭 P0-005。
