# Stage 4D-03O PaymentEngine Crimson Rose Ready Unit Evidence

日期：2026-05-14
结论：**FOCUSED SLICE GREEN / PROJECT NOT READY**

本文记录 4D-03O Crimson Rose ready-unit focused slice 的实现证据。该证据接受 4D-03O focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Implementation Evidence

实现范围：

- `P4ActivatedAbilityCatalog`：
  - 新增 Crimson Rose executable ability constants and definition。
  - `ExperienceCost=3`、`ManaCost=0`、`PowerCost=0`、`RequiredTargetCount=1`、`RequiresBattlefieldSource=false`、`RequiresBaseEquipmentSource=true`、`ExhaustsSourceAsCost=true`、`AppliesSpellshieldTargetTax=true`。
  - `DEFERRED_EXPERIENCE_EXHAUST_READY_UNIT` 从 deferred-only surface 移除，Fluft Poro 与 Shadow deferred surfaces 保留。
- `MatchSession`：
  - open-main prompt 公开 controlled face-up ready base equipment `UNL-109/219` source requirement。
  - source requirement metadata 包含 `experienceCost=3`、`exhaustsSource=true`、`requiresBaseEquipmentSource=true`、`readyTargetCount=1`、`appliesSpellshieldTargetTax=true`、`stackPolicy=ordinary-stack-item-before-ready` 与 `paymentPolicy=payment-plan-experience-and-spellshield-tax`。
  - target choices 覆盖公开 base / battlefield unit；敌方法盾目标需要当前 mana 足以支付 target tax，友方法盾目标不缴税。
  - prompt 不公开 `RECYCLE_RUNE:*` 或 `TEMP_PAYMENT_RESOURCE:*` payment resource choices，并在 experience 不足、source 已横置、source 非 base equipment 等场景隐藏该 ability。
- `CoreRuleEngine`：
  - 新增 Crimson Rose command resolver，成功路径使用 shared `PaymentPlan` / `AuthorizePayment` / `TryCommitPayment`。
  - 成功 activation 支付 3 experience 与必要 enemy Spellshield mana tax，横置 source equipment，创建普通 stack item，不立即 ready target。
  - stack pass-pass 后解析 Crimson Rose stack item，将 target unit 置为 active，并保持 source 在 controller base / exhausted。
  - wrong timing / payload / source / target / unsupported payment resource rejected no-mutation。
- `CrimsonRoseActivatedAbilityTests`：
  - prompt surface、target choices、experience cost 与 source filtering。
  - friendly Spellshield no-tax activation。
  - enemy Spellshield target tax activation。
  - stack pass-pass ready target / keep source exhausted。
  - wrong timing、non-active player、missing / too many / invalid target、face-down / standby target、insufficient experience、insufficient tax mana、unsupported optional cost、recycle rune、temporary resource、invalid source variants no-mutation。
- `ConformanceFixtureRunnerTests`：
  - executable ability catalog 断言 Crimson Rose。
  - deferred surface audit 不再要求 Crimson Rose ready-unit skill deferred。

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Crimson|FullyQualifiedName~Scarlet|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests"
```

结果：

```text
Passed! - Failed: 0, Passed: 169, Skipped: 0, Total: 169
```

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~Crimson|FullyQualifiedName~Scarlet|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Experience|FullyQualifiedName~Spellshield"
```

结果：

```text
Passed! - Failed: 0, Passed: 396, Skipped: 0, Total: 396
```

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 3940, Skipped: 0, Total: 3940
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

4D-03O focused slice accepted. Crimson Rose ready-unit skill is now covered by server-authoritative prompt / command / stack resolution / audit representative tests. P0-005 remains open because the complete activated ability / resource skill family, full Spellshield target-tax propagation and full PaymentEngine quote parity are still not full-official.
