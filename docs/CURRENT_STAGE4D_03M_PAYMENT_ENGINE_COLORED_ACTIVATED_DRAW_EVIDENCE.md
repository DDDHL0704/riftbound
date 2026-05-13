# Stage 4D-03M PaymentEngine Colored Activated Draw Evidence

日期：2026-05-14
结论：**FOCUSED SLICE GREEN / PROJECT NOT READY**

本文记录 4D-03M Renata Glasc colored activated draw focused slice 的实现证据。该证据接受 4D-03M focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Implementation Evidence

实现范围：

- `P4ActivatedAbilityCatalog`：
  - 新增 Renata Glasc executable ability constants and definition。
  - `ManaCost=1`、`PowerCost=0`、`PowerCostByTrait[blue]=1`、`RequiredTargetCount=0`、`RequiresBattlefieldSource=true`、`ExhaustsSourceAsCost=false`。
  - `SFD·088/221` 与 `SFD·088a/221` 通过 source alias helper 支持同功能抽牌技能。
  - `DEFERRED_PAY_1_BLUE_DRAW_1` 从 deferred-only surface 移除；`DEFERRED_PAY_4_BLUE4_EXHAUST_SCORE_1` 仍 deferred。
- `MatchSession`：
  - open-main prompt 公开 Renata `ACTIVATE_ABILITY` source requirement。
  - source requirement metadata 包含 `powerCostByTrait.blue=1`、typed available-power-with-payment-resources、`stackPolicy=ordinary-stack-item-before-draw` 与 `paymentPolicy=payment-plan-typed-blue`。
  - prompt 不公开 target slot、不要求 exhaust、不暴露 `TEMP_PAYMENT_RESOURCE:*`，仅在蓝色 shortfall 可由蓝色 rune 回收补足时公开 `RECYCLE_RUNE:<objectId>`。
- `CoreRuleEngine`：
  - 新增 Renata command resolver，成功路径使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`。
  - 成功 activation 支付 1 mana + 1 blue typed power，创建普通 stack item，不立即抽牌。
  - stack pass-pass 后解析 Renata draw stack item，抽 1 张牌，source 保持 battlefield / unexhausted，score 不变。
  - wrong timing / payload / source / payment resource rejected no-mutation。
- `RenataActivatedAbilityTests`：
  - prompt surface。
  - `SFD·088/221` 与 `SFD·088a/221` 成功 activation。
  - blue rune recycle shortfall。
  - pass-pass draw resolution。
  - wrong timing、target、temporary resource、wrong / duplicate / invalid / unnecessary recycle、unsupported optional cost、insufficient mana / blue、base source、face-down、wrong controller、wrong card、non-active player no-mutation。
- `ConformanceFixtureRunnerTests`：
  - executable ability catalog 断言 Renata draw。
  - deferred surface audit 不再要求 draw skill deferred，仍要求 score skill deferred。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~Malzahar|FullyQualifiedName~DragonSoulSage|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：

```text
Passed! - Failed: 0, Passed: 164, Skipped: 0, Total: 164
```

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Renata|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub"
```

结果：

```text
Passed! - Failed: 0, Passed: 335, Skipped: 0, Total: 335
```

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 3893, Skipped: 0, Total: 3893
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

4D-03M focused slice accepted. Renata Glasc colored activated draw is now covered by server-authoritative prompt / command / audit representative tests. P0-005 remains open because the complete activated ability / resource skill family and full PaymentEngine quote parity are still not full-official.
