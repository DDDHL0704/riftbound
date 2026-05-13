# Stage 4D-03N PaymentEngine Colored Activated Score Evidence

日期：2026-05-14
结论：**FOCUSED SLICE GREEN / PROJECT NOT READY**

本文记录 4D-03N Renata Glasc colored activated score focused slice 的实现证据。该证据接受 4D-03N focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Implementation Evidence

实现范围：

- `P4ActivatedAbilityCatalog`：
  - 新增 Renata Glasc score executable ability constants and definition。
  - `ManaCost=4`、`PowerCost=0`、`PowerCostByTrait[blue]=4`、`RequiredTargetCount=0`、`RequiresBattlefieldSource=true`、`ExhaustsSourceAsCost=true`。
  - `SFD·088/221` 与 `SFD·088a/221` 通过 source alias helper 支持同功能 score skill。
  - `DEFERRED_PAY_4_BLUE4_EXHAUST_SCORE_1` 从 deferred-only surface 移除。
- `MatchSession`：
  - open-main prompt 公开 Renata score `ACTIVATE_ABILITY` source requirement。
  - source requirement metadata 包含 `powerCostByTrait.blue=4`、typed available-power-with-payment-resources、`exhaustsSource=true`、`scoreAmount=1`、`stackPolicy=ordinary-stack-item-before-score` 与 `paymentPolicy=payment-plan-typed-blue-exhaust-as-cost`。
  - prompt 不公开 target slot、不暴露 `TEMP_PAYMENT_RESOURCE:*`，并在 source 已横置时隐藏 score 但保留 draw。
- `CoreRuleEngine`：
  - 新增 Renata score command resolver，成功路径使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`。
  - 成功 activation 支付 4 mana + 4 blue typed power，横置 source，创建普通 stack item，不立即加分。
  - stack pass-pass 后解析 Renata score stack item，controller 获得 1 分，并沿用既有 winning-score / match-finished 语义。
  - wrong timing / payload / source / payment resource rejected no-mutation。
- `RenataActivatedAbilityTests`：
  - prompt surface 与 exhausted source prompt filtering。
  - `SFD·088/221` 与 `SFD·088a/221` 成功 activation。
  - blue rune recycle shortfall。
  - pass-pass score / win resolution。
  - wrong timing、target、temporary resource、wrong / duplicate / invalid / unnecessary recycle、unsupported optional cost、insufficient mana / blue、base source、face-down、exhausted source、wrong controller、wrong card、non-active player no-mutation。
- `ConformanceFixtureRunnerTests`：
  - executable ability catalog 断言 Renata score。
  - deferred surface audit 不再要求 score skill deferred。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~Malzahar|FullyQualifiedName~DragonSoulSage|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：

```text
Passed! - Failed: 0, Passed: 185, Skipped: 0, Total: 185
```

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Score"
```

结果：

```text
Passed! - Failed: 0, Passed: 369, Skipped: 0, Total: 369
```

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 3914, Skipped: 0, Total: 3914
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

4D-03N focused slice accepted. Renata Glasc colored activated score is now covered by server-authoritative prompt / command / stack resolution / audit representative tests. P0-005 remains open because the complete activated ability / resource skill family and full PaymentEngine quote parity are still not full-official.
