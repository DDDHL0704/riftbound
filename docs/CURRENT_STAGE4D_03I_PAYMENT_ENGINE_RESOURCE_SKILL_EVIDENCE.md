# Stage 4D-03I PaymentEngine Resource Skill Evidence

日期：2026-05-14
结论：**EVIDENCE GREEN / PROJECT NOT READY**

本文记录 4D-03I focused slice 的测试证据。该证据只证明 `OGN·113/298` 玛尔扎哈 open-main representative resource skill 已通过服务端 prompt、命令、审计 payload 与回归测试，不关闭 P0-005 full official PaymentEngine。

## 1. Implementation Evidence

代码证据：

- `P4ActivatedAbilityCatalog` 登记 `MALZAHAR_DESTROY_FRIENDLY_EXHAUST_GAIN_2_PAYMENT_POWER`，标记 `IsResourceSkill=true`、`PaymentOnlyResource=true`、`GeneratedPower=2`、`UsesTargetAsCost=true`。
- `MatchSession` 对 Malzahar source requirement 暴露 `targetChoicesByIndex["0"]`，只包含合法友方单位/装备成本对象，并在 metadata 中写入 resource skill / payment-only / restriction 字段。
- `CoreRuleEngine` 新增 `ResolveMalzaharResourceSkill`，校验 open-main、source、target 和 no optional costs；成功时横置来源、摧毁成本对象、获得 payment-only metadata 的 2 点 power，不创建 stack item。
- `MalzaharResourceSkillTests` 覆盖 prompt、spell-duel 不开放、单位/装备成功成本、无效来源矩阵、无效目标矩阵。
- `PaymentEngineUnificationTests` 补充与支付统一测试同域的 prompt / 成功 / no-mutation 聚焦断言。
- `ConformanceFixtureRunnerTests` 确认 activated ability catalog registry 暴露 Malzahar definition。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Malzahar|FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ResourceSkill"
```

结果：

```text
Passed!  - Failed:     0, Passed:   105, Skipped:     0, Total:   105
```

覆盖含义：

- Malzahar ordinary play 仍绿色。
- Malzahar resource skill prompt / command / rejection / audit focused coverage 通过。
- 既有 ActivateAbility、PaymentEngineUnification 与 ResourceSkill 相邻代表路径绿色。

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~ActivateAbility|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~SpellDuel|FullyQualifiedName~Priority"
```

结果：

```text
Passed!  - Failed:     0, Passed:   317, Skipped:     0, Total:   317
```

覆盖含义：

- ActivateAbility prompt / command surfaces 未回退。
- Payment resource、SpendPower、RunePool、SpellDuel、Priority、ActionPrompt、GameHub 相邻路径未被 4D-03I 破坏。

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed!  - Failed:     0, Passed:  3840, Skipped:     0, Total:  3840
```

## 5. Diff Check

命令：

```sh
git diff --check
```

结果：无输出。

## 6. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件，本切片未读取、未修改、未暂存。`tests/Riftbound.ConformanceTests/MalzaharResourceSkillTests.cs` 为本切片新增测试文件，已纳入本次实现证据。

## 7. Verdict

4D-03I evidence green。该证据接受 Malzahar open-main resource skill representative，不接受完整 payment-only lifecycle、spell-duel / swift timing、reaction prohibition 或 full PaymentEngine completion。项目仍 **NOT READY**。
