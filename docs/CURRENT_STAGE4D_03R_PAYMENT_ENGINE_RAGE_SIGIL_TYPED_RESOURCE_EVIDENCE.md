# Stage 4D-03R PaymentEngine Rage Sigil Typed Resource Evidence

日期：2026-05-14
结论：**FOCUSED SLICE GREEN / PROJECT NOT READY**

本文记录 4D-03R Rage Sigil typed resource focused slice 的实现证据。该证据接受 4D-03R focused slice，不关闭 P0-005 full official，不升级卡牌矩阵，不改变项目 **NOT READY** 结论。

## 1. Implementation Evidence

实现范围：

- `P4ActivatedAbilityCatalog`：
  - 新增 Rage Sigil executable ability constants and definition。
  - `SourceCardNo=SFD·222/221`、`EffectKind=RAGE_SIGIL_REACTION_TYPED_RESOURCE_GAIN_RED`、`IsResourceSkill=true`、`PaymentOnlyResource=true`、`ReactionSpeed=true`、`RequiresBaseEquipmentSource=true`、`ExhaustsSourceAsCost=true`、`RequiredTargetCount=0`。
  - 新增 `GeneratedPowerByTrait.red=1` 与 `PAY_RUNE_COSTS_ONLY_TYPED_RED_TEMPORARY_LEDGER_4D_03R` restriction。
- `MatchSession`：
  - `TemporaryPaymentResourceState` 支持 typed generated / remaining power，同时保留 generic `GeneratedPower` / `RemainingPower`。
  - snapshot / prompt temporary resource views 暴露 `generatedPowerByTrait`、`remainingPowerByTrait`、resource restriction 和 allowed payment kinds。
  - stack-priority reaction representative window 会为 priority player 构建 Rage Sigil source requirement。
  - source requirement metadata 包含 `resourceSkill`、`reactionSpeed`、`typedPaymentOnlyResource`、`requiresBaseEquipmentSource`、`exhaustsSource`、`generatedPowerByTrait.red=1`、`resourceLifecycle=temporary-payment-resource-ledger` 与 no ordinary stack policy。
  - temporary payment resource prompt quote now accounts for typed temporary power and filters wrong-trait / unnecessary resource choices.
- `CoreRuleEngine`：
  - 新增 Rage Sigil command resolver。
  - success path validates controlled public ready base equipment source, rejects targets / optional costs / payment resource actions, exhausts source, creates typed red temporary payment-only ledger and does not add a stack item.
  - pending `PAY_COST` temporary resource application supports typed temporary resource consumption for typed red and generic rune costs.
  - wrong timing, wrong priority, invalid source, target payload, optional cost payload, wrong trait payment, mana-only payment and no-rune payment misuse variants are rejected no-mutation.
- `RageSigilResourceSkillTests`：
  - catalog executable metadata。
  - prompt surface and metadata。
  - activation creates typed red payment-only ledger without stack item。
  - red typed resource pays red typed and generic rune costs, then cleans up。
  - red typed resource rejects blue typed-only and mana-only payment without mutation。
  - wrong timing, wrong source and invalid payload no-mutation matrix。
- `ConformanceFixtureRunnerTests`：
  - executable ability catalog audit includes Rage Sigil ability id.

## 2. Focused Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentEngineUnificationTests|FullyQualifiedName~RunePool"
```

结果：

```text
Passed! - Failed: 0, Passed: 191, Skipped: 0, Total: 191
```

## 3. Adjacent Regression

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore --filter "FullyQualifiedName~RageSigil|FullyQualifiedName~Sigil|FullyQualifiedName~ActivateAbility|FullyQualifiedName~ResourceSkill|FullyQualifiedName~PaymentResource|FullyQualifiedName~SpendPower|FullyQualifiedName~RunePool|FullyQualifiedName~ActionPrompt|FullyQualifiedName~GameHub|FullyQualifiedName~Reaction|FullyQualifiedName~Priority|FullyQualifiedName~SpellDuel"
```

结果：

```text
Passed! - Failed: 0, Passed: 439, Skipped: 0, Total: 439
```

## 4. Backend Full

命令：

```sh
source scripts/dev-env.sh && dotnet test Riftbound.slnx --no-restore
```

结果：

```text
Passed! - Failed: 0, Passed: 4021, Skipped: 0, Total: 4021
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

4D-03R focused slice accepted. Rage Sigil typed red payment-only resource skill is now covered by server-authoritative prompt / command / temporary payment ledger / audit representative tests. P0-005 remains open because full Sigil family, complete `[A]` / `[C]` resource skills, complete PaymentEngine quote parity and full official matrix closure remain incomplete.
