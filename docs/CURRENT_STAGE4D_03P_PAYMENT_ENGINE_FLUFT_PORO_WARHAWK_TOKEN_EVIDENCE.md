# Stage 4D-03P PaymentEngine Fluft Poro Warhawk Token Evidence

日期：2026-05-14
结论：**FOCUSED SLICE GREEN / PROJECT NOT READY**

本文记录 4D-03P Fluft Poro Warhawk token focused slice 的实现证据。该证据接受 4D-03P focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Implementation Evidence

实现范围：

- `P4ActivatedAbilityCatalog`：
  - 新增 Fluft Poro executable ability constants and definition。
  - `SourceCardNo=UNL-160/219`、`EffectKind=FLUFT_PORO_ACTIVATED_CREATE_TWO_WARHAWKS`、`ManaCost=0`、`PowerCost=0`、`ExperienceCost=0`、`RequiredTargetCount=0`、`RequiresBattlefieldSource=true`、`ExhaustsSourceAsCost=true`、`AppliesSpellshieldTargetTax=false`。
  - `DEFERRED_TAP_CREATE_TWO_SPELLSHIELD_WARHAWKS` 从 deferred-only surface 移除，Shadow swift stun deferred surface 保留。
- `MatchSession`：
  - open-main prompt 公开 controlled face-up ready battlefield `UNL-160/219` source requirement。
  - source requirement metadata 包含 `requiresBattlefieldSource=true`、`exhaustsSource=true`、`tokenCardNo=UNL·T02`、`tokenCount=2`、`tokenPower=1`、Warhawk token tags、`stackPolicy=ordinary-stack-item-before-token-create` 与 `paymentPolicy=payment-plan-zero-cost-exhaust-as-cost`。
  - prompt 不公开目标槽或 payment resource choices，并在 source 位于 base、source 已横置、wrong controller、wrong card、face-down、standby 等场景隐藏该 ability。
- `CoreRuleEngine`：
  - 新增 Fluft Poro command resolver，成功路径使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment` 记录零成本 payment audit。
  - 成功 activation 横置 battlefield source，创建普通 stack item，不立即创建 token。
  - stack pass-pass 后解析 Fluft Poro stack item，创建两名 `UNL·T02` Warhawk token 到 controller base，token 为 1 power unit with Spellshield。
  - wrong timing / spell-duel / payload / source / unsupported payment resource variants rejected no-mutation。
- `FluftPoroActivatedAbilityTests`：
  - prompt surface、zero-cost payment metadata、token metadata 与 no-target / no-resource choices。
  - illegal source prompt hiding。
  - activation exhaust source / create stack / no immediate token。
  - pass-pass stack resolution creates two Warhawk tokens in controller base。
  - wrong timing、spell-duel、non-active player、target、unsupported optional cost、recycle rune、temporary resource、missing / invalid source variants no-mutation。
- `ConformanceFixtureRunnerTests`：
  - executable ability catalog 断言 Fluft Poro。
  - deferred surface audit 不再要求 Fluft Poro Warhawk token skill deferred。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Fluft|FullyQualifiedName~Warhawk|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~Token"
```

结果：

```text
Passed! - Failed: 0, Passed: 189, Skipped: 0, Total: 189
```

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Fluft|FullyQualifiedName~Warhawk|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Token|FullyQualifiedName~Spellshield|FullyQualifiedName~Battlefield"
```

结果：

```text
Passed! - Failed: 0, Passed: 685, Skipped: 0, Total: 685
```

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 3962, Skipped: 0, Total: 3962
```

## 5. Whitespace

命令：

```sh
git diff --check
```

结果：no output.

## 6. Worktree Note

`riftbound-dotnet.sln` 仍是既有未跟踪文件；本切片未读取、未修改、未暂存。

## 7. Verdict

4D-03P focused slice accepted. Fluft Poro Warhawk token skill is now covered by server-authoritative prompt / command / stack resolution / audit representative tests. P0-005 remains open because the complete activated ability / resource skill family, full Spellshield target-tax propagation, token-play breadth and full PaymentEngine quote parity are still not full-official.
