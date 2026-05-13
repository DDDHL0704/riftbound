# Stage 4D-03J PaymentEngine Resource Skill Lifecycle Evidence

日期：2026-05-14
结论：**EVIDENCE GREEN / PROJECT NOT READY**

本文记录 4D-03J focused slice 的测试证据。该证据只证明 `OGN·113/298` 玛尔扎哈 `[A A]` resource skill 的 spell-duel focus representative、reaction-prohibited immediate resolution 与 temporary payment-only ledger 已通过服务端 prompt、命令、审计 payload 与回归测试，不关闭 P0-005 full official PaymentEngine。

## 1. Implementation Evidence

代码证据：

- `P4ActivatedAbilityCatalog` 将 Malzahar resource restriction 更新为 `PAY_RUNE_COSTS_ONLY_TEMPORARY_LEDGER_4D_03J`，说明本切片采用 temporary payment-only ledger。
- `PaymentCostRules` 新增 `TEMP_PAYMENT_RESOURCE:*` action id helper 与 `RUNE_COST` payment kind。
- `MatchState` 新增 `TemporaryPaymentResourceState`，snapshot 向资源拥有者 / spectator 暴露 temporary payment resources；`PendingPayment` snapshot 和 prompt metadata 会把合法 temporary resource action 纳入 `paymentResourceActions` / `paymentResourceChoices` / `paymentResourcePowerByChoice`。
- `ActionPromptBuilder.SpellDuelFocusActions` 在 focus player 有合法 Malzahar source 时公开 `ACTIVATE_ABILITY`；source requirement metadata 包含 `resourceLifecycle=temporary-payment-resource-ledger` 与 `allowedPaymentKinds=[RUNE_COST]`。
- `CoreRuleEngine.ResolveMalzaharResourceSkill` 接受 open-main 与 spell-duel focus context，成功时横置来源、摧毁成本对象、创建 `TemporaryPaymentResourceState`，保持立即结算且不创建 ordinary stack item。
- `ResolvePendingPayCost` 接入 temporary payment resource action：只能在 generic rune power cost 缺口中使用，消费后写入 `TEMPORARY_PAYMENT_RESOURCE_SPENT` / `TEMPORARY_PAYMENT_RESOURCE_CLEARED`，并从 state ledger 清除。
- `MalzaharResourceSkillTests` 覆盖 prompt、spell-duel focus 成功、非焦点不开放、wrong timing no-mutation、temporary resource mana-only rejection、generic rune-cost consumption and cleanup。
- `PaymentEngineUnificationTests` 更新 Malzahar resource skill 代表断言，确认成功后 power 不再直接进入 `RunePool.Power`，而是进入 temporary payment resource ledger。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~SpellDuel|FullyQualifiedName~Reaction|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill"
```

结果：

```text
Passed! - Failed: 0, Passed: 116, Skipped: 0, Total: 116
```

覆盖含义：

- Malzahar open-main representative 仍绿色。
- Malzahar spell-duel focus prompt / command / no-stack immediate resolution 通过。
- temporary payment-only ledger、pending `PAY_COST` 消费和 cleanup 通过。
- SpellDuel、Reaction、PaymentEngineUnification 与 ResourceSkill 相邻代表路径未回退。

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~SpellDuel|FullyQualifiedName~Priority|FullyQualifiedName~Reaction|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 340, Skipped: 0, Total: 340
```

覆盖含义：

- ActivateAbility、SpellDuel、Priority、Reaction、PaymentResource、SpendPower、RunePool、ActionPrompt 与 GameHub 相邻路径未被 4D-03J 破坏。
- 新增 temporary payment resource metadata 未破坏 existing prompt / snapshot consumers。

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 3847, Skipped: 0, Total: 3847
```

## 5. Diff Check

命令：

```sh
git diff --check
```

结果：无输出。

## 6. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件，本切片未读取、未修改、未暂存。B worker Raman 初稿未提交；A 收回写入锁后完成复核、修补、测试与文档收口。

## 7. Verdict

4D-03J evidence green。该证据接受 Malzahar resource skill lifecycle representative，但不接受完整 `[A]` / `[C]` family、所有 payment windows、reaction/counter full target-filter model 或 full PaymentEngine completion。项目仍 **NOT READY**。
